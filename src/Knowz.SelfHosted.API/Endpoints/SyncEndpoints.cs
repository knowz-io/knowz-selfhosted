namespace Knowz.SelfHosted.API.Endpoints;

using Knowz.SelfHosted.API.Helpers;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/sync")
            .WithTags("Sync");

        // GET /api/v1/sync/links — List all sync links
        group.MapGet("/links", async (
            HttpContext context,
            IVaultSyncOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            var links = await orchestrator.ListLinksAsync(ct);
            return Results.Ok(links);
        }).Produces<List<VaultSyncStatusDto>>();

        // GET /api/v1/sync/links/{localVaultId} — Get sync status for a vault
        group.MapGet("/links/{localVaultId:guid}", async (
            Guid localVaultId,
            HttpContext context,
            IVaultSyncOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            var status = await orchestrator.GetStatusAsync(localVaultId, ct);
            return status != null ? Results.Ok(status) : Results.NotFound();
        }).Produces<VaultSyncStatusDto>();

        // POST /api/v1/sync/links — Establish a new sync link
        group.MapPost("/links", async (
            HttpContext context,
            [FromBody] EstablishSyncLinkRequest request,
            IVaultSyncOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            try
            {
                var link = await orchestrator.EstablishLinkAsync(request, ct);
                return Results.Created($"/api/v1/sync/links/{link.LocalVaultId}", link);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).Produces<VaultSyncStatusDto>(StatusCodes.Status201Created);

        // DELETE /api/v1/sync/links/{localVaultId} — Remove a sync link
        group.MapDelete("/links/{localVaultId:guid}", async (
            Guid localVaultId,
            HttpContext context,
            IVaultSyncOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            var removed = await orchestrator.RemoveLinkAsync(localVaultId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        // POST /api/v1/sync/run/{localVaultId} — Trigger a sync operation
        group.MapPost("/run/{localVaultId:guid}", async (
            Guid localVaultId,
            HttpContext context,
            [FromBody] TriggerSyncRequest? request,
            IVaultSyncOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            var direction = request?.Direction ?? SyncDirection.Full;
            var result = await orchestrator.SyncAsync(localVaultId, direction, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.UnprocessableEntity(result);
        }).Produces<VaultSyncResult>()
          .Produces<VaultSyncResult>(StatusCodes.Status422UnprocessableEntity);
    }
}
