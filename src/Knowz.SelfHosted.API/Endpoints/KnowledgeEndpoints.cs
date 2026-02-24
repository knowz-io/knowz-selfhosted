using Knowz.SelfHosted.API.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;
using Knowz.SelfHosted.Infrastructure.Interfaces;

namespace Knowz.SelfHosted.API.Endpoints;

public static class KnowledgeEndpoints
{
    public static void MapKnowledgeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/knowledge").WithTags("Knowledge");

        group.MapGet("/", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            int page = 1,
            int pageSize = 20,
            string sort = "created",
            string sortDir = "desc",
            string? type = null,
            string? title = null,
            string? fileName = null,
            string? startDate = null,
            string? endDate = null,
            string? vaultId = null,
            string? createdByUserId = null,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);

            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);

            Guid? filterVaultId = !string.IsNullOrEmpty(vaultId) && Guid.TryParse(vaultId, out var vid) ? vid : null;
            Guid? filterCreatedByUserId = !string.IsNullOrEmpty(createdByUserId) && Guid.TryParse(createdByUserId, out var cid) ? cid : null;

            var result = await svc.ListKnowledgeItemsAsync(
                page, pageSize, sort, sortDir, type, title, fileName, startDate, endDate, ct,
                accessibleVaultIds, filterVaultId, filterCreatedByUserId);
            return Results.Ok(result);
        }).Produces<KnowledgeListResponse>();

        group.MapGet("/creators", async (
            KnowledgeService svc,
            CancellationToken ct) =>
        {
            var creators = await svc.GetKnowledgeCreatorsAsync(ct);
            return Results.Ok(creators);
        }).Produces<List<CreatorRef>>();

        group.MapGet("/{id:guid}", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            Guid id,
            CancellationToken ct) =>
        {
            // Check if user has access to this knowledge item's vaults
            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);
            if (accessibleVaultIds != null)
            {
                var vaultIds = await svc.GetKnowledgeVaultIdsAsync(id, ct);
                if (vaultIds.Count > 0 && !vaultIds.Any(v => accessibleVaultIds.Contains(v)))
                    return Results.Json(new { error = "Access denied to this knowledge item." }, statusCode: 403);
            }

            var result = await svc.GetKnowledgeItemAsync(id, ct);
            return result is null
                ? Results.NotFound(new { error = "Knowledge item not found" })
                : Results.Ok(result);
        }).Produces<KnowledgeItemResponse>().Produces(404);

        group.MapPost("/", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            CreateKnowledgeRequest req,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "content is required" });

            if (req.Content.Length > 1_048_576)
                return Results.BadRequest(new { error = "Content exceeds maximum allowed size of 1MB" });

            // Check write access to target vault
            if (!string.IsNullOrWhiteSpace(req.VaultId) && Guid.TryParse(req.VaultId, out var targetVaultId))
            {
                var hasAccess = await VaultEndpoints.HasVaultAccessAsync(
                    context, vaultAccessService, targetVaultId, requireWrite: true, ct: ct);
                if (!hasAccess)
                    return Results.Json(new { error = "Access denied to the target vault." }, statusCode: 403);
            }

            var userId = VaultEndpoints.GetUserIdFromContext(context);

            var result = await svc.CreateKnowledgeAsync(
                req.Content,
                req.Title ?? (req.Content.Length > 100 ? req.Content[..100] : req.Content),
                req.Type ?? "Note",
                req.VaultId,
                req.Tags ?? new List<string>(),
                req.Source,
                ct,
                userId);
            return Results.Created($"/api/v1/knowledge/{result.Id}", result);
        }).Produces<CreateKnowledgeResult>(201).Produces(400);

        group.MapPut("/{id:guid}", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            Guid id,
            UpdateKnowledgeRequest req,
            CancellationToken ct) =>
        {
            if (req.Content is not null && req.Content.Length > 1_048_576)
                return Results.BadRequest(new { error = "Content exceeds maximum allowed size of 1MB" });

            // Check write access to this knowledge item's vaults
            var vaultIds = await svc.GetKnowledgeVaultIdsAsync(id, ct);
            if (vaultIds.Count > 0)
            {
                var hasWriteAccess = false;
                foreach (var vaultId in vaultIds)
                {
                    if (await VaultEndpoints.HasVaultAccessAsync(context, vaultAccessService, vaultId, requireWrite: true, ct: ct))
                    {
                        hasWriteAccess = true;
                        break;
                    }
                }
                if (!hasWriteAccess)
                    return Results.Json(new { error = "Access denied. Write permission required." }, statusCode: 403);
            }

            // Check write access to destination vault if moving to a different vault
            if (!string.IsNullOrWhiteSpace(req.VaultId) && Guid.TryParse(req.VaultId, out var destVaultId))
            {
                var hasDestAccess = await VaultEndpoints.HasVaultAccessAsync(context, vaultAccessService, destVaultId, requireWrite: true, ct: ct);
                if (!hasDestAccess)
                    return Results.Json(new { error = "Access denied: no write permission to destination vault" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await svc.UpdateKnowledgeAsync(
                id, req.Title, req.Content, req.Source, req.Tags, req.VaultId, ct);
            return result is null
                ? Results.NotFound(new { error = "Knowledge item not found" })
                : Results.Ok(result);
        }).Produces<UpdateKnowledgeResult>().Produces(404).Produces(400);

        group.MapPost("/{id:guid}/amend", async (
            KnowledgeService svc,
            IContentAmendmentService amendmentService,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            Guid id,
            AmendKnowledgeRequest req,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Instruction))
                return Results.BadRequest(new { error = "instruction is required" });

            // Check write access to this knowledge item's vaults
            var vaultIds = await svc.GetKnowledgeVaultIdsAsync(id, ct);
            if (vaultIds.Count > 0)
            {
                var hasWriteAccess = false;
                foreach (var vaultId in vaultIds)
                {
                    if (await VaultEndpoints.HasVaultAccessAsync(context, vaultAccessService, vaultId, requireWrite: true, ct: ct))
                    {
                        hasWriteAccess = true;
                        break;
                    }
                }
                if (!hasWriteAccess)
                    return Results.Json(new { error = "Access denied. Write permission required." }, statusCode: 403);
            }

            // Get the existing knowledge item
            var item = await svc.GetKnowledgeItemAsync(id, ct);
            if (item is null)
                return Results.NotFound(new { error = "Knowledge item not found" });

            if (string.IsNullOrWhiteSpace(item.Content))
                return Results.BadRequest(new { error = "Knowledge item has no content to amend" });

            try
            {
                var amendedContent = await amendmentService.ApplyContentUpdateAsync(item.Content, req.Instruction, ct);
                var result = await svc.UpdateKnowledgeAsync(id, null, amendedContent, null, null, null, ct);
                return result is null
                    ? Results.NotFound(new { error = "Knowledge item not found" })
                    : Results.Ok(new { status = "amended", id = result.Id, title = result.Title });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).Produces<object>().Produces(400).Produces(404);

        group.MapDelete("/{id:guid}", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            Guid id,
            CancellationToken ct) =>
        {
            // Check delete access to this knowledge item's vaults
            var vaultIds = await svc.GetKnowledgeVaultIdsAsync(id, ct);
            if (vaultIds.Count > 0)
            {
                var hasDeleteAccess = false;
                foreach (var vaultId in vaultIds)
                {
                    if (await VaultEndpoints.HasVaultAccessAsync(context, vaultAccessService, vaultId, requireDelete: true, ct: ct))
                    {
                        hasDeleteAccess = true;
                        break;
                    }
                }
                if (!hasDeleteAccess)
                    return Results.Json(new { error = "Access denied. Delete permission required." }, statusCode: 403);
            }

            var result = await svc.DeleteKnowledgeAsync(id, ct);
            return result is null
                ? Results.NotFound(new { error = "Knowledge item not found" })
                : Results.Ok(result);
        }).Produces<DeleteResult>().Produces(404);

        // Fix F: Re-add batch-move endpoint with vault access enforcement
        group.MapPost("/batch-move", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            BatchMoveKnowledgeRequest req,
            CancellationToken ct) =>
        {
            if (req.KnowledgeIds == null || req.KnowledgeIds.Count == 0)
                return Results.BadRequest(new { error = "knowledgeIds is required and must not be empty" });

            // Require write access to the target vault
            var hasAccess = await VaultEndpoints.HasVaultAccessAsync(
                context, vaultAccessService, req.TargetVaultId, requireWrite: true, ct: ct);
            if (!hasAccess)
                return Results.Json(new { error = "Access denied to the target vault." }, statusCode: 403);

            // Require write access to all source vaults containing the knowledge items
            var sourceVaultIds = new HashSet<Guid>();
            foreach (var knowledgeId in req.KnowledgeIds)
            {
                var knowledgeVaultIds = await svc.GetKnowledgeVaultIdsAsync(knowledgeId, ct);
                if (knowledgeVaultIds is { Count: > 0 })
                {
                    foreach (var vaultId in knowledgeVaultIds)
                    {
                        sourceVaultIds.Add(vaultId);
                    }
                }
            }

            if (sourceVaultIds.Count > 0)
            {
                foreach (var vaultId in sourceVaultIds)
                {
                    var hasSourceAccess = await VaultEndpoints.HasVaultAccessAsync(
                        context, vaultAccessService, vaultId, requireWrite: true, ct: ct);
                    if (!hasSourceAccess)
                    {
                        return Results.Json(
                            new { error = "Access denied to one or more source vaults." },
                            statusCode: 403);
                    }
                }
            }

            var result = await svc.BatchMoveToVaultAsync(req.KnowledgeIds, req.TargetVaultId, ct);
            return Results.Ok(result);
        }).Produces<BatchMoveResult>().Produces(400);

        group.MapPost("/{id:guid}/reprocess", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            Guid id,
            CancellationToken ct) =>
        {
            // Check access to this knowledge item's vaults
            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);
            if (accessibleVaultIds != null)
            {
                var vaultIds = await svc.GetKnowledgeVaultIdsAsync(id, ct);
                if (vaultIds.Count > 0 && !vaultIds.Any(v => accessibleVaultIds.Contains(v)))
                    return Results.Json(new { error = "Access denied to this knowledge item." }, statusCode: 403);
            }

            var result = await svc.ReprocessKnowledgeAsync(id, ct);
            return result is null
                ? Results.NotFound(new { error = "Knowledge item not found" })
                : Results.Ok(result);
        }).Produces<ReprocessResult>().Produces(404);

        group.MapGet("/stats", async (
            KnowledgeService svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            CancellationToken ct) =>
        {
            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);
            return Results.Ok(await svc.GetStatisticsAsync(ct, accessibleVaultIds));
        }).Produces<KnowledgeStatsResponse>();
    }
}
