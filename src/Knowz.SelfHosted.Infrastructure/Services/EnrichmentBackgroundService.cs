using System.Text.Json;
using System.Threading.Channels;
using Knowz.Core.Entities;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Data.Entities;
using Knowz.SelfHosted.Infrastructure.Extensions;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Services;

public class EnrichmentBackgroundService : BackgroundService
{
    private readonly Channel<EnrichmentWorkItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrichmentBackgroundService> _logger;

    private static readonly HashSet<string> PlaceholderTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "untitled", "untitled content", "media file", "document", "file", "n/a", "unknown"
    };

    public EnrichmentBackgroundService(
        Channel<EnrichmentWorkItem> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<EnrichmentBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EnrichmentBackgroundService started");

        var channelTask = ProcessChannelAsync(stoppingToken);
        var recoveryTask = RecoveryTimerAsync(stoppingToken);

        await Task.WhenAll(channelTask, recoveryTask);
    }

    internal async Task ProcessChannelAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessWorkItemAsync(workItem, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Enrichment failed for KnowledgeId {Id}", workItem.KnowledgeId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }

    internal async Task ProcessWorkItemAsync(EnrichmentWorkItem workItem, CancellationToken ct)
    {
        TenantContext.CurrentTenantId = workItem.TenantId;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            var enrichmentService = scope.ServiceProvider.GetRequiredService<ITextEnrichmentService>();
            var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
            var openAIService = scope.ServiceProvider.GetRequiredService<IOpenAIService>();
            var chunkingService = scope.ServiceProvider.GetRequiredService<ISelfHostedChunkingService>();

            // Load outbox item
            var outboxItem = await db.EnrichmentOutbox
                .FirstOrDefaultAsync(e => e.KnowledgeId == workItem.KnowledgeId &&
                    (e.Status == EnrichmentStatus.Pending || e.Status == EnrichmentStatus.Processing), ct);

            if (outboxItem != null)
            {
                outboxItem.Status = EnrichmentStatus.Processing;
                await db.SaveChangesAsync(ct);
            }

            // Load knowledge entity
            var knowledge = await db.KnowledgeItems
                .Include(k => k.Tags)
                .FirstOrDefaultAsync(k => k.Id == workItem.KnowledgeId, ct);

            if (knowledge == null)
            {
                _logger.LogWarning("Knowledge {Id} not found for enrichment", workItem.KnowledgeId);
                if (outboxItem != null)
                {
                    outboxItem.Status = EnrichmentStatus.Failed;
                    outboxItem.ErrorMessage = "Knowledge item not found";
                    outboxItem.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                return;
            }

            try
            {
                // 1. Title generation (if placeholder)
                if (IsPlaceholderTitle(knowledge.Title, knowledge.Content))
                {
                    var newTitle = await enrichmentService.GenerateTitleAsync(knowledge.Content, ct);
                    if (newTitle != null)
                    {
                        knowledge.Title = newTitle;
                        _logger.LogDebug("Generated title for knowledge {Id}: {Title}", knowledge.Id, newTitle);
                    }
                }

                // 2. Gather attachment + comment text for enrichment
                var attachmentText = string.Empty;
                try
                {
                    attachmentText = await GetAllAttachmentTextAsync(db, knowledge.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load attachment text for knowledge {Id}", knowledge.Id);
                }

                var contentForEnrichment = string.IsNullOrEmpty(attachmentText)
                    ? knowledge.Content
                    : $"{knowledge.Content}\n\n{attachmentText}";

                // 3. Summarize (with attachment context)
                // Short-circuit: For very brief content with no attachments, use content as-is
                var wordCount = contentForEnrichment.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= 5 && string.IsNullOrEmpty(attachmentText))
                {
                    knowledge.Summary = contentForEnrichment.Trim();
                    _logger.LogDebug("Short-circuit summary for knowledge {Id}: content has {Words} words", knowledge.Id, wordCount);
                }
                else
                {
                    var summary = await enrichmentService.SummarizeAsync(contentForEnrichment, 100, ct);
                    if (summary != null)
                    {
                        knowledge.Summary = summary;
                        _logger.LogDebug("Generated summary for knowledge {Id}", knowledge.Id);
                    }
                }

                // 4. Extract tags (with attachment context)
                var tags = await enrichmentService.ExtractTagsAsync(knowledge.Title, contentForEnrichment, 5, ct);
                if (tags.Count > 0)
                {
                    await LinkTagsAsync(db, knowledge, tags, workItem.TenantId, ct);
                    _logger.LogDebug("Extracted {Count} tags for knowledge {Id}", tags.Count, knowledge.Id);
                }

                knowledge.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // Re-index in search (non-critical)
                try
                {
                    await ReindexAsync(db, knowledge, searchService, openAIService, chunkingService, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to re-index knowledge {Id} after enrichment", knowledge.Id);
                }

                // Mark completed
                if (outboxItem != null)
                {
                    outboxItem.Status = EnrichmentStatus.Completed;
                    outboxItem.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }

                _logger.LogInformation("Enrichment completed for knowledge {Id}", knowledge.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Enrichment processing error for knowledge {Id}", workItem.KnowledgeId);
                if (outboxItem != null)
                {
                    outboxItem.RetryCount++;
                    outboxItem.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                    if (outboxItem.RetryCount >= outboxItem.MaxRetries)
                    {
                        outboxItem.Status = EnrichmentStatus.Failed;
                    }
                    else
                    {
                        outboxItem.Status = EnrichmentStatus.Pending;
                        outboxItem.NextRetryAt = DateTime.UtcNow + TimeSpan.FromMinutes(Math.Pow(2, outboxItem.RetryCount));
                    }
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }

    internal async Task RecoveryTimerAsync(CancellationToken stoppingToken)
    {
        // Short initial delay to let DI and DB context initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();

                    var now = DateTime.UtcNow;
                    var stuckThreshold = now.AddMinutes(-10);

                    var items = await db.EnrichmentOutbox
                        .Where(e =>
                            (e.Status == EnrichmentStatus.Pending && (e.NextRetryAt == null || e.NextRetryAt <= now)) ||
                            (e.Status == EnrichmentStatus.Processing && e.CreatedAt < stuckThreshold))
                        .Take(50)
                        .ToListAsync(stoppingToken);

                    foreach (var item in items)
                    {
                        _channel.Writer.TryWrite(new EnrichmentWorkItem(item.KnowledgeId, item.TenantId));
                    }

                    if (items.Count > 0)
                        _logger.LogInformation("Recovery: re-enqueued {Count} outbox items", items.Count);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Recovery timer error");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }

    internal static bool IsPlaceholderTitle(string? title, string? content)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        if (PlaceholderTitles.Contains(title.Trim()))
            return true;

        // Title equals first 80 chars of content (InboxService pattern)
        if (content != null && content.Length >= 80 &&
            title.Trim() == content[..80].Trim())
            return true;

        return false;
    }

    private static async Task LinkTagsAsync(SelfHostedDbContext db, Knowledge knowledge,
        List<string> tagNames, Guid tenantId, CancellationToken ct)
    {
        var existingTags = await db.Tags
            .Where(t => tagNames.Contains(t.Name))
            .ToListAsync(ct);

        foreach (var tagName in tagNames)
        {
            // Skip if already linked
            if (knowledge.Tags.Any(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                continue;

            var tag = existingTags.FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));
            if (tag == null)
            {
                tag = new Tag { TenantId = tenantId, Name = tagName };
                db.Tags.Add(tag);
            }
            knowledge.Tags.Add(tag);
        }
    }

    public static async Task<string> GetAllAttachmentTextAsync(
        SelfHostedDbContext db, Guid knowledgeId, CancellationToken ct)
    {
        var parts = new List<string>();
        var totalChars = 0;
        const int maxChars = 50_000;

        // 1. Direct knowledge-level attachments
        var knowledgeAttachments = await db.FileAttachments
            .IgnoreQueryFilters()
            .Where(fa => fa.KnowledgeId == knowledgeId)
            .Join(
                db.FileRecords.IgnoreQueryFilters().Where(fr => !fr.IsDeleted && fr.ExtractedText != null),
                fa => fa.FileRecordId,
                fr => fr.Id,
                (fa, fr) => new { fr.FileName, fr.ExtractedText, fa.CreatedAt })
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        foreach (var att in knowledgeAttachments)
        {
            if (totalChars >= maxChars) break;
            var section = $"--- Attachment: {att.FileName} ---\n{att.ExtractedText}";
            parts.Add(section);
            totalChars += section.Length;
        }

        // 2. Comments and their attachments
        var comments = await db.Comments
            .IgnoreQueryFilters()
            .Where(c => c.KnowledgeId == knowledgeId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.AuthorName, c.Body })
            .ToListAsync(ct);

        foreach (var comment in comments)
        {
            if (totalChars >= maxChars) break;

            // Comment body
            if (!string.IsNullOrWhiteSpace(comment.Body))
            {
                var commentSection = $"--- Comment by {comment.AuthorName} ---\n{comment.Body}";
                parts.Add(commentSection);
                totalChars += commentSection.Length;
            }

            // Comment attachments
            var commentAttachments = await db.FileAttachments
                .IgnoreQueryFilters()
                .Where(fa => fa.CommentId == comment.Id)
                .Join(
                    db.FileRecords.IgnoreQueryFilters().Where(fr => !fr.IsDeleted && fr.ExtractedText != null),
                    fa => fa.FileRecordId,
                    fr => fr.Id,
                    (fa, fr) => new { fr.FileName, fr.ExtractedText })
                .ToListAsync(ct);

            foreach (var att in commentAttachments)
            {
                if (totalChars >= maxChars) break;
                var section = $"--- Comment Attachment: {att.FileName} ---\n{att.ExtractedText}";
                parts.Add(section);
                totalChars += section.Length;
            }
        }

        return parts.Count > 0 ? string.Join("\n\n", parts) : string.Empty;
    }

    internal static async Task ReindexAsync(SelfHostedDbContext db, Knowledge knowledge,
        ISearchService searchService, IOpenAIService openAIService,
        ISelfHostedChunkingService chunkingService, CancellationToken ct)
    {
        var primaryKv = await db.KnowledgeVaults
            .Include(kv => kv.Vault)
            .Where(kv => kv.KnowledgeId == knowledge.Id)
            .OrderByDescending(kv => kv.IsPrimary)
            .FirstOrDefaultAsync(ct);

        string? vaultName = primaryKv?.Vault?.Name;
        Guid? vaultId = primaryKv?.VaultId;

        List<Guid>? ancestorVaultIds = null;
        if (vaultId.HasValue)
        {
            ancestorVaultIds = await db.VaultAncestors
                .Where(va => va.DescendantVaultId == vaultId.Value)
                .Select(va => va.AncestorVaultId)
                .ToListAsync(ct);
        }

        var topicName = await db.KnowledgeItems
            .Where(k => k.Id == knowledge.Id)
            .Select(k => k.Topic != null ? k.Topic.Name : null)
            .FirstOrDefaultAsync(ct);

        var currentTags = knowledge.Tags.Select(t => t.Name).ToList();

        // Load existing chunks for embedding cache
        var existingChunks = await db.ContentChunks
            .Where(c => c.KnowledgeId == knowledge.Id)
            .ToListAsync(ct);
        var existingHashMap = existingChunks
            .Where(c => c.EmbeddingVectorJson != null)
            .ToDictionary(c => c.ContentHash, c => c.EmbeddingVectorJson!);

        // Gather attachment text for indexing
        var attachmentText = string.Empty;
        try
        {
            attachmentText = await GetAllAttachmentTextAsync(db, knowledge.Id, ct);
        }
        catch (Exception ex)
        {
            // Non-critical: proceed with knowledge content only
            _ = ex; // suppress unused variable warning in static context
        }

        var contentForChunking = string.IsNullOrEmpty(attachmentText)
            ? knowledge.Content
            : $"{knowledge.Content}\n\n{attachmentText}";

        await searchService.DeleteDocumentWithChunksAsync(knowledge.Id,
            existingChunks.Select(c => c.Position), ct);

        var strategy = chunkingService.DetermineStrategy(knowledge.Type);
        var chunks = chunkingService.ChunkWithContext(
            contentForChunking, knowledge.Title, knowledge.Summary,
            currentTags, strategy);

        var chunkData = new Dictionary<int, (string Hash, string? EmbeddingJson)>();

        foreach (var chunk in chunks)
        {
            var hash = ContentHasher.Hash(chunk.Content);
            float[]? embedding;
            string? embeddingJson;

            if (existingHashMap.TryGetValue(hash, out var cachedVector))
            {
                embeddingJson = cachedVector;
                try
                {
                    embedding = JsonSerializer.Deserialize<float[]>(cachedVector);
                }
                catch (JsonException)
                {
                    embedding = await openAIService.GenerateEmbeddingAsync(chunk.EmbeddingText, ct);
                    embeddingJson = embedding != null ? JsonSerializer.Serialize(embedding) : null;
                }
            }
            else
            {
                embedding = await openAIService.GenerateEmbeddingAsync(chunk.EmbeddingText, ct);
                embeddingJson = embedding != null ? JsonSerializer.Serialize(embedding) : null;
            }

            chunkData[chunk.Position] = (hash, embeddingJson);

            await searchService.IndexDocumentAsync(
                knowledge.Id, knowledge.Title, chunk.Content, knowledge.Summary,
                vaultName, vaultId, ancestorVaultIds, topicName,
                currentTags, knowledge.Type.ToString(),
                knowledge.FilePath, embedding,
                chunkIndex: chunks.Count > 1 ? chunk.Position : null,
                cancellationToken: ct);
        }

        // Replace persisted chunks (now includes freshly generated embeddings)
        try
        {
            db.ContentChunks.RemoveRange(existingChunks);
            foreach (var chunk in chunks)
            {
                var (hash, embeddingJson) = chunkData[chunk.Position];
                db.ContentChunks.Add(new ContentChunk
                {
                    TenantId = knowledge.TenantId,
                    KnowledgeId = knowledge.Id,
                    Position = chunk.Position,
                    Content = chunk.Content,
                    ContentHash = hash,
                    EmbeddingVectorJson = embeddingJson,
                    EmbeddedAt = embeddingJson != null ? DateTime.UtcNow : null
                });
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // Chunk persistence is optional; indexing already completed
        }

        knowledge.IsIndexed = true;
        knowledge.IndexedAt = DateTime.UtcNow;
    }
}
