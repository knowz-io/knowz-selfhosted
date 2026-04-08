namespace Knowz.SelfHosted.Application.Services;

using System.Diagnostics;
using Knowz.Core.Interfaces;
using Knowz.Core.Schema;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates bidirectional vault sync between selfhosted and platform.
/// Flow: Pull (remote → local) → Push (local → remote) → Update cursors.
/// Selfhosted always initiates; platform is passive.
/// </summary>
public class VaultSyncOrchestrator : IVaultSyncOrchestrator
{
    private readonly SelfHostedDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPlatformSyncClient _platformClient;
    private readonly VaultScopedExportService _exportService;
    private readonly FileSyncService _fileSyncService;
    private readonly ILogger<VaultSyncOrchestrator> _logger;

    public VaultSyncOrchestrator(
        SelfHostedDbContext db,
        ITenantProvider tenantProvider,
        IPlatformSyncClient platformClient,
        VaultScopedExportService exportService,
        FileSyncService fileSyncService,
        ILogger<VaultSyncOrchestrator> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _platformClient = platformClient;
        _exportService = exportService;
        _fileSyncService = fileSyncService;
        _logger = logger;
    }

    public async Task<VaultSyncResult> SyncAsync(
        Guid localVaultId, SyncDirection direction = SyncDirection.Full,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new VaultSyncResult { Direction = direction };

        // Find sync link
        var link = await _db.VaultSyncLinks
            .FirstOrDefaultAsync(l => l.LocalVaultId == localVaultId, ct);

        if (link == null)
        {
            result.Success = false;
            result.Error = $"No sync link found for vault {localVaultId}";
            result.Duration = sw.Elapsed;
            return result;
        }

        if (!link.SyncEnabled)
        {
            result.Success = false;
            result.Error = "Sync is disabled for this vault";
            result.Duration = sw.Elapsed;
            return result;
        }

        // Prevent concurrent sync
        if (link.LastSyncStatus == VaultSyncStatus.InProgress)
        {
            result.Success = false;
            result.Error = "Sync is already in progress";
            result.Duration = sw.Elapsed;
            return result;
        }

        // Mark as in progress
        link.LastSyncStatus = VaultSyncStatus.InProgress;
        link.LastSyncError = null;
        link.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Step 0: Schema compatibility check
            var schema = await _platformClient.GetSchemaAsync(link, ct);
            if (!CoreSchema.CanRead(schema.Version))
            {
                throw new InvalidOperationException(
                    $"Platform schema v{schema.Version} is not compatible. Local supports {CoreSchema.GetCompatibilityInfo()}.");
            }

            // Step 1: Pull (remote → local)
            if (direction is SyncDirection.Full or SyncDirection.PullOnly)
            {
                var pullResult = await PullAsync(link, ct);
                result.PullAccepted = pullResult.Accepted;
                result.PullSkipped = pullResult.Skipped;
                result.TombstonesApplied += pullResult.TombstonesApplied;
                result.Details.AddRange(pullResult.Details);
            }

            // Step 2: Push (local → remote)
            if (direction is SyncDirection.Full or SyncDirection.PushOnly)
            {
                var pushResult = await PushAsync(link, ct);
                result.PushAccepted = pushResult.Accepted;
                result.PushSkipped = pushResult.Skipped;
                result.Details.AddRange(pushResult.Details);
            }

            // Step 3: File sync (after entity sync)
            if (direction == SyncDirection.Full)
            {
                var fileResult = await _fileSyncService.SyncFilesAsync(link, ct);
                result.Details.Add($"Files: {fileResult.Downloaded} downloaded, {fileResult.Uploaded} uploaded, {fileResult.Skipped} skipped");
                if (fileResult.Errors.Count > 0)
                    result.Details.AddRange(fileResult.Errors.Select(e => $"File error: {e}"));
            }

            // Step 4: Finalize
            link.LastSyncStatus = VaultSyncStatus.Succeeded;
            link.LastSyncCompletedAt = DateTime.UtcNow;
            link.LastSyncError = null;
            link.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            result.Success = true;
            _logger.LogInformation(
                "Vault sync completed for {VaultId}: pull={PullAccepted}/{PullSkipped}, push={PushAccepted}/{PushSkipped}",
                localVaultId, result.PullAccepted, result.PullSkipped, result.PushAccepted, result.PushSkipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault sync failed for {VaultId}", localVaultId);

            link.LastSyncStatus = VaultSyncStatus.Failed;
            link.LastSyncError = ex.Message;
            link.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            result.Success = false;
            result.Error = ex.Message;
        }

        result.Duration = sw.Elapsed;
        return result;
    }

    private async Task<SyncDeltaImportResult> PullAsync(VaultSyncLink link, CancellationToken ct)
    {
        var totalResult = new SyncDeltaImportResult { Success = true };
        var page = 1;
        DateTime? newCursor = null;

        // Paginated pull
        while (true)
        {
            var package = await _platformClient.ExportDeltaAsync(link, link.LastPullCursor, page, 500, ct);

            if (package.Data.KnowledgeItems.Count == 0 &&
                (package.Tombstones == null || package.Tombstones.Count == 0) &&
                page > 1)
                break;

            // Import using last-write-wins
            var importResult = await ImportSyncDeltaLocallyAsync(package, link.LocalVaultId, ct);
            totalResult.Accepted += importResult.Accepted;
            totalResult.Skipped += importResult.Skipped;
            totalResult.TombstonesApplied += importResult.TombstonesApplied;
            totalResult.Details.AddRange(importResult.Details);

            // Track cursor from first page
            if (page == 1 && package.SyncCursor.HasValue)
                newCursor = package.SyncCursor;

            // If we got fewer items than page size, we're done
            if (package.Data.KnowledgeItems.Count < 500)
                break;

            page++;
        }

        // Update pull cursor
        if (newCursor.HasValue)
        {
            link.LastPullCursor = newCursor;
            await _db.SaveChangesAsync(ct);
        }

        totalResult.Details.Add($"Pull complete: {totalResult.Accepted} accepted, {totalResult.Skipped} skipped");
        return totalResult;
    }

    private async Task<SyncDeltaImportResult> PushAsync(VaultSyncLink link, CancellationToken ct)
    {
        var result = new SyncDeltaImportResult { Success = true };

        // Export local delta
        var package = await _exportService.ExportDeltaAsync(link.LocalVaultId, link.LastPushCursor, link, ct);

        if (package.Data.KnowledgeItems.Count == 0 &&
            (package.Tombstones == null || package.Tombstones.Count == 0))
        {
            result.Details.Add("Push: nothing to push");
            return result;
        }

        // Push to platform
        var platformResponse = await _platformClient.ImportDeltaAsync(link, package, ct);
        result.Accepted = platformResponse.Accepted;
        result.Skipped = platformResponse.Skipped;

        // Mark tombstones as propagated
        var unpropagated = await _db.SyncTombstones
            .Where(st => st.VaultSyncLinkId == link.Id && !st.Propagated)
            .ToListAsync(ct);

        foreach (var st in unpropagated)
        {
            st.Propagated = true;
            st.PropagatedAt = DateTime.UtcNow;
        }

        // Update push cursor
        link.LastPushCursor = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        result.Details.Add($"Push complete: {result.Accepted} accepted, {result.Skipped} skipped");
        return result;
    }

    /// <summary>
    /// Import a sync delta package using last-write-wins conflict resolution.
    /// </summary>
    private async Task<SyncDeltaImportResult> ImportSyncDeltaLocallyAsync(
        global::Knowz.Core.Portability.PortableExportPackage package, Guid localVaultId, CancellationToken ct)
    {
        var result = new SyncDeltaImportResult { Success = true };
        var tenantId = _tenantProvider.TenantId;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Import reference entities (last-write-wins)
            result.Accepted += await SyncEntities(package.Data.Topics, _db.Topics, tenantId,
                (pt, existing) => { existing.Name = pt.Name; existing.Description = pt.Description; }, ct);
            result.Accepted += await SyncEntities(package.Data.Tags, _db.Tags, tenantId,
                (pt, existing) => { existing.Name = pt.Name; }, ct);
            result.Accepted += await SyncEntities(package.Data.Persons, _db.Persons, tenantId,
                (pp, existing) => { existing.Name = pp.Name; }, ct);
            result.Accepted += await SyncEntities(package.Data.Locations, _db.Locations, tenantId,
                (pl, existing) => { existing.Name = pl.Name; }, ct);
            result.Accepted += await SyncEntities(package.Data.Events, _db.Events, tenantId,
                (pe, existing) => { existing.Name = pe.Name; }, ct);

            await _db.SaveChangesAsync(ct);

            // Import knowledge items
            foreach (var pk in package.Data.KnowledgeItems)
            {
                var existing = await _db.KnowledgeItems
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Id == pk.Id, ct);

                if (existing != null)
                {
                    if (pk.UpdatedAt > existing.UpdatedAt)
                    {
                        existing.Title = pk.Title;
                        existing.Content = pk.Content;
                        existing.Summary = pk.Summary;
                        existing.Source = pk.Source;
                        existing.TopicId = pk.TopicId;
                        existing.UpdatedAt = pk.UpdatedAt;
                        existing.IsDeleted = false;
                        result.Accepted++;
                    }
                    else result.Skipped++;
                }
                else
                {
                    var newItem = new global::Knowz.Core.Entities.Knowledge
                    {
                        Id = pk.Id,
                        TenantId = tenantId,
                        Title = pk.Title,
                        Content = pk.Content,
                        Summary = pk.Summary,
                        Source = pk.Source,
                        TopicId = pk.TopicId,
                        CreatedAt = pk.CreatedAt,
                        UpdatedAt = pk.UpdatedAt,
                    };
                    _db.KnowledgeItems.Add(newItem);

                    // Create vault junction
                    var junctionExists = await _db.KnowledgeVaults
                        .IgnoreQueryFilters()
                        .AnyAsync(kv => kv.KnowledgeId == pk.Id && kv.VaultId == localVaultId, ct);
                    if (!junctionExists)
                    {
                        _db.KnowledgeVaults.Add(new global::Knowz.Core.Entities.KnowledgeVault
                        {
                            KnowledgeId = pk.Id,
                            VaultId = localVaultId,
                            IsPrimary = true,
                        });
                    }

                    result.Accepted++;
                }

                // Sync person junctions
                if (pk.PersonLinks != null)
                {
                    foreach (var link in pk.PersonLinks)
                    {
                        var exists = await _db.KnowledgePersons
                            .IgnoreQueryFilters()
                            .AnyAsync(kp => kp.KnowledgeId == pk.Id && kp.PersonId == link.EntityId, ct);
                        if (!exists)
                        {
                            _db.KnowledgePersons.Add(new global::Knowz.Core.Entities.KnowledgePerson
                            {
                                KnowledgeId = pk.Id,
                                PersonId = link.EntityId,
                                RelationshipContext = link.RelationshipContext,
                                Role = link.Role,
                                Mentions = link.Mentions,
                                ConfidenceScore = link.ConfidenceScore ?? 0.0,
                            });
                        }
                    }
                }
            }

            // Import comments
            foreach (var pc in package.Data.Comments)
            {
                var existing = await _db.Comments
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == pc.Id, ct);

                if (existing != null)
                {
                    if (pc.UpdatedAt > existing.UpdatedAt)
                    {
                        existing.Body = pc.Body;
                        existing.AuthorName = pc.AuthorName;
                        existing.UpdatedAt = pc.UpdatedAt;
                        result.Accepted++;
                    }
                    else result.Skipped++;
                }
                else
                {
                    _db.Comments.Add(new global::Knowz.Core.Entities.KnowledgeComment
                    {
                        Id = pc.Id,
                        TenantId = tenantId,
                        KnowledgeId = pc.KnowledgeId,
                        ParentCommentId = pc.ParentCommentId,
                        AuthorName = pc.AuthorName,
                        Body = pc.Body,
                        CreatedAt = pc.CreatedAt,
                        UpdatedAt = pc.UpdatedAt,
                    });
                    result.Accepted++;
                }
            }

            // Process tombstones
            if (package.Tombstones != null)
            {
                foreach (var tombstone in package.Tombstones)
                {
                    if (await ApplyTombstoneAsync(tenantId, tombstone, ct))
                        result.TombstonesApplied++;
                }
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Sync delta import failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Generic last-write-wins sync for reference entities (Topic, Tag, Person, Location, Event).
    /// </summary>
    private async Task<int> SyncEntities<TPortable, TEntity>(
        List<TPortable> incoming,
        DbSet<TEntity> dbSet,
        Guid tenantId,
        Action<TPortable, TEntity> updateAction,
        CancellationToken ct)
        where TPortable : class
        where TEntity : class, global::Knowz.Core.Interfaces.ISelfHostedEntity
    {
        var count = 0;
        foreach (var item in incoming)
        {
            var id = (Guid)item.GetType().GetProperty("Id")!.GetValue(item)!;
            var updatedAt = (DateTime)item.GetType().GetProperty("UpdatedAt")!.GetValue(item)!;

            var existing = await dbSet.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

            if (existing != null)
            {
                if (updatedAt > existing.UpdatedAt)
                {
                    updateAction(item, existing);
                    existing.UpdatedAt = updatedAt;
                    count++;
                }
            }
            else
            {
                var name = (string?)item.GetType().GetProperty("Name")?.GetValue(item) ?? "";
                var createdAt = (DateTime)item.GetType().GetProperty("CreatedAt")!.GetValue(item)!;
                var newEntity = (TEntity)Activator.CreateInstance(typeof(TEntity))!;
                newEntity.Id = id;
                newEntity.TenantId = tenantId;
                newEntity.CreatedAt = createdAt;
                newEntity.UpdatedAt = updatedAt;
                // Set name via reflection
                newEntity.GetType().GetProperty("Name")?.SetValue(newEntity, name);
                // Set description if available
                var desc = item.GetType().GetProperty("Description")?.GetValue(item);
                newEntity.GetType().GetProperty("Description")?.SetValue(newEntity, desc);
                dbSet.Add(newEntity);
                count++;
            }
        }
        return count;
    }

    private async Task<bool> ApplyTombstoneAsync(Guid tenantId, global::Knowz.Core.Portability.SyncTombstoneDto tombstone, CancellationToken ct)
    {
        // ISelfHostedEntity has no DeletedAt — only IsDeleted + UpdatedAt
        switch (tombstone.EntityType)
        {
            case "Knowledge":
                var k = await _db.KnowledgeItems.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tombstone.EntityId, ct);
                if (k != null && !k.IsDeleted && tombstone.DeletedAt > k.UpdatedAt)
                {
                    k.IsDeleted = true; k.UpdatedAt = tombstone.DeletedAt;
                    return true;
                }
                break;
            case "Topic":
                var t = await _db.Topics.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tombstone.EntityId, ct);
                if (t != null && !t.IsDeleted && tombstone.DeletedAt > t.UpdatedAt)
                {
                    t.IsDeleted = true; t.UpdatedAt = tombstone.DeletedAt;
                    return true;
                }
                break;
            case "Person":
                var p = await _db.Persons.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tombstone.EntityId, ct);
                if (p != null && !p.IsDeleted && tombstone.DeletedAt > p.UpdatedAt)
                {
                    p.IsDeleted = true; p.UpdatedAt = tombstone.DeletedAt;
                    return true;
                }
                break;
            case "Location":
                var l = await _db.Locations.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tombstone.EntityId, ct);
                if (l != null && !l.IsDeleted && tombstone.DeletedAt > l.UpdatedAt)
                {
                    l.IsDeleted = true; l.UpdatedAt = tombstone.DeletedAt;
                    return true;
                }
                break;
            case "Event":
                var e = await _db.Events.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tombstone.EntityId, ct);
                if (e != null && !e.IsDeleted && tombstone.DeletedAt > e.UpdatedAt)
                {
                    e.IsDeleted = true; e.UpdatedAt = tombstone.DeletedAt;
                    return true;
                }
                break;
        }
        return false;
    }

    public async Task<VaultSyncStatusDto?> GetStatusAsync(Guid localVaultId, CancellationToken ct = default)
    {
        var link = await _db.VaultSyncLinks
            .FirstOrDefaultAsync(l => l.LocalVaultId == localVaultId, ct);

        if (link == null) return null;

        var vault = await _db.Vaults.FindAsync([localVaultId], ct);

        return new VaultSyncStatusDto
        {
            LinkId = link.Id,
            LocalVaultId = link.LocalVaultId,
            LocalVaultName = vault?.Name ?? "Unknown",
            RemoteVaultId = link.RemoteVaultId,
            PlatformApiUrl = link.PlatformApiUrl,
            Status = link.LastSyncStatus.ToString(),
            LastSyncError = link.LastSyncError,
            LastSyncCompletedAt = link.LastSyncCompletedAt,
            LastPullCursor = link.LastPullCursor,
            LastPushCursor = link.LastPushCursor,
            SyncEnabled = link.SyncEnabled,
        };
    }

    public async Task<List<VaultSyncStatusDto>> ListLinksAsync(CancellationToken ct = default)
    {
        var links = await _db.VaultSyncLinks.ToListAsync(ct);
        var vaultIds = links.Select(l => l.LocalVaultId).ToList();
        var vaults = await _db.Vaults
            .Where(v => vaultIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        return links.Select(link => new VaultSyncStatusDto
        {
            LinkId = link.Id,
            LocalVaultId = link.LocalVaultId,
            LocalVaultName = vaults.TryGetValue(link.LocalVaultId, out var v) ? v.Name : "Unknown",
            RemoteVaultId = link.RemoteVaultId,
            PlatformApiUrl = link.PlatformApiUrl,
            Status = link.LastSyncStatus.ToString(),
            LastSyncError = link.LastSyncError,
            LastSyncCompletedAt = link.LastSyncCompletedAt,
            LastPullCursor = link.LastPullCursor,
            LastPushCursor = link.LastPushCursor,
            SyncEnabled = link.SyncEnabled,
        }).ToList();
    }

    public async Task<VaultSyncStatusDto> EstablishLinkAsync(EstablishSyncLinkRequest request, CancellationToken ct = default)
    {
        // Verify vault exists
        var vault = await _db.Vaults.FindAsync([request.LocalVaultId], ct);
        if (vault == null)
            throw new InvalidOperationException($"Local vault {request.LocalVaultId} not found");

        // Check for existing link
        var existing = await _db.VaultSyncLinks
            .FirstOrDefaultAsync(l => l.LocalVaultId == request.LocalVaultId, ct);
        if (existing != null)
            throw new InvalidOperationException($"Vault {request.LocalVaultId} already has a sync link");

        var link = new VaultSyncLink
        {
            LocalVaultId = request.LocalVaultId,
            RemoteVaultId = request.RemoteVaultId,
            RemoteTenantId = Guid.Empty, // Will be populated on first sync
            PlatformApiUrl = request.PlatformApiUrl.TrimEnd('/'),
            ApiKeyEncrypted = request.ApiKey, // TODO: encrypt
        };

        _db.VaultSyncLinks.Add(link);

        // Register as sync partner on platform
        try
        {
            await _platformClient.RegisterPartnerAsync(link, $"selfhosted-{vault.Name}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register sync partner on platform (will retry on first sync)");
        }

        await _db.SaveChangesAsync(ct);

        return new VaultSyncStatusDto
        {
            LinkId = link.Id,
            LocalVaultId = link.LocalVaultId,
            LocalVaultName = vault.Name,
            RemoteVaultId = link.RemoteVaultId,
            PlatformApiUrl = link.PlatformApiUrl,
            Status = link.LastSyncStatus.ToString(),
            SyncEnabled = link.SyncEnabled,
        };
    }

    public async Task<bool> RemoveLinkAsync(Guid localVaultId, CancellationToken ct = default)
    {
        var link = await _db.VaultSyncLinks
            .FirstOrDefaultAsync(l => l.LocalVaultId == localVaultId, ct);

        if (link == null) return false;

        // Remove associated tombstones
        var tombstones = await _db.SyncTombstones
            .Where(st => st.VaultSyncLinkId == link.Id)
            .ToListAsync(ct);
        _db.SyncTombstones.RemoveRange(tombstones);

        _db.VaultSyncLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
