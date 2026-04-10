using Knowz.SelfHosted.API.Helpers;
using Knowz.SelfHosted.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knowz.SelfHosted.API.Endpoints;

public static class GitSyncEndpoints
{
    public static void MapGitSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/vaults/{vaultId:guid}/git-sync")
            .WithTags("GitSync");

        // POST /api/v1/vaults/{vaultId}/git-sync — Configure git repo for vault
        group.MapPost("/", async (
            HttpContext context,
            Guid vaultId,
            [FromBody] ConfigureGitSyncRequest request,
            GitSyncService gitSyncService,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAdminOrAbove(context))
                return AuthorizationHelpers.Forbidden("Admin access required for git sync configuration.");

            try
            {
                var result = await gitSyncService.ConfigureAsync(
                    vaultId, request.Url, request.Branch, request.Pat, request.FilePatterns, ct);
                return Results.Ok(new GitSyncStatusDto
                {
                    Id = result.Id,
                    VaultId = result.VaultId,
                    RepositoryUrl = result.RepositoryUrl,
                    Branch = result.Branch,
                    LastSyncCommitSha = result.LastSyncCommitSha,
                    LastSyncAt = result.LastSyncAt,
                    Status = result.Status,
                    FilePatterns = result.FilePatterns,
                    ErrorMessage = result.ErrorMessage,
                    CreatedAt = result.CreatedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).Produces<GitSyncStatusDto>();

        // GET /api/v1/vaults/{vaultId}/git-sync — Get config + status
        group.MapGet("/", async (
            HttpContext context,
            Guid vaultId,
            GitSyncService gitSyncService,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAdminOrAbove(context))
                return AuthorizationHelpers.Forbidden("Admin access required for git sync configuration.");

            var status = await gitSyncService.GetStatusAsync(vaultId, ct);
            return status != null ? Results.Ok(status) : Results.NotFound();
        }).Produces<GitSyncStatusDto>();

        // POST /api/v1/vaults/{vaultId}/git-sync/trigger — Trigger manual sync
        group.MapPost("/trigger", async (
            HttpContext context,
            Guid vaultId,
            GitSyncService gitSyncService,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAdminOrAbove(context))
                return AuthorizationHelpers.Forbidden("Admin access required for git sync operations.");

            try
            {
                await gitSyncService.TriggerSyncAsync(vaultId, ct);
                return Results.Ok(new { message = "Sync queued successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // GET /api/v1/vaults/{vaultId}/git-sync/history — Sync history
        group.MapGet("/history", async (
            HttpContext context,
            Guid vaultId,
            GitSyncService gitSyncService,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAdminOrAbove(context))
                return AuthorizationHelpers.Forbidden("Admin access required for git sync operations.");

            var history = await gitSyncService.GetHistoryAsync(vaultId, ct);
            return Results.Ok(history);
        }).Produces<List<GitSyncHistoryDto>>();

        // DELETE /api/v1/vaults/{vaultId}/git-sync — Remove config
        group.MapDelete("/", async (
            HttpContext context,
            Guid vaultId,
            GitSyncService gitSyncService,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsAdminOrAbove(context))
                return AuthorizationHelpers.Forbidden("Admin access required for git sync configuration.");

            var removed = await gitSyncService.RemoveAsync(vaultId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        });
    }
}
