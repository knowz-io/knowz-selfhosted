using Knowz.SelfHosted.API.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;

namespace Knowz.SelfHosted.API.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/search", async (
            SearchFacade svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            string? q,
            int limit = 10,
            Guid? vaultId = null,
            bool includeChildren = true,
            string? tags = null,
            bool requireAllTags = false,
            string? startDate = null,
            string? endDate = null,
            string? type = null,
            CancellationToken ct = default) =>
        {
            limit = Math.Clamp(limit, 1, 100);
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest(new { error = "q (query) parameter is required" });

            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);

            var tagList = string.IsNullOrWhiteSpace(tags)
                ? new List<string>()
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            DateTime? start = null, end = null;
            if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var sd)) start = sd;
            if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var ed)) end = ed;

            var result = await svc.SearchKnowledgeAsync(
                q, limit, vaultId, includeChildren, tagList, requireAllTags, start, end, type, ct, accessibleVaultIds);
            return Results.Ok(result);
        }).WithTags("Search").Produces<SearchResponse>().Produces(400);

        app.MapPost("/api/v1/ask", async (
            SearchFacade svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            AskQuestionRequest req,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question))
                return Results.BadRequest(new { error = "question is required" });

            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);

            Guid? vaultId = null;
            if (!string.IsNullOrWhiteSpace(req.VaultId) && Guid.TryParse(req.VaultId, out var vid))
                vaultId = vid;

            var result = await svc.AskQuestionAsync(req.Question, vaultId, req.ResearchMode, ct, accessibleVaultIds);
            return Results.Ok(result);
        }).WithTags("Search").Produces<AskAnswerResponse>().Produces(400);
    }
}
