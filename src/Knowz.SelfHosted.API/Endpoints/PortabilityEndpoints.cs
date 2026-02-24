namespace Knowz.SelfHosted.API.Endpoints;

using Knowz.Core.Portability;
using Knowz.Core.Schema;
using Knowz.SelfHosted.API.Helpers;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;
using Microsoft.AspNetCore.Mvc;

public static class PortabilityEndpoints
{
    private const long MaxImportBodySize = 50 * 1024 * 1024; // 50MB

    public static void MapPortabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/portability")
            .WithTags("Portability");

        // GET /api/portability/export
        group.MapGet("/export", async (
            HttpContext context,
            IPortableExportService exportService,
            ILogger<PortableExportService> logger,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            try
            {
                var package = await exportService.ExportAsync(ct);
                return Results.Ok(package);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Export failed");
                return Results.Problem(
                    detail: ex.Message,
                    title: "Export failed",
                    statusCode: 500);
            }
        }).Produces<PortableExportPackage>();

        // POST /api/portability/import/validate
        group.MapPost("/import/validate", async (
            HttpContext context,
            IPortableImportService importService,
            ILogger<PortableImportService> logger,
            PortableExportPackage package,
            CancellationToken ct) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            try
            {
                var result = await importService.ValidateAsync(package, ct);
                return result.IsValid
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import validation failed");
                return Results.Problem(
                    detail: ex.Message,
                    title: "Import validation failed",
                    statusCode: 500);
            }
        }).Produces<ImportValidationResult>()
          .Produces<ImportValidationResult>(422);

        // POST /api/portability/import
        group.MapPost("/import", async (
            HttpContext context,
            IPortableImportService importService,
            ILogger<PortableImportService> logger,
            PortableExportPackage package,
            string strategy = "skip",
            CancellationToken ct = default) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            if (!Enum.TryParse<ImportConflictStrategy>(strategy, true, out var conflictStrategy))
                return Results.BadRequest(new { error = $"Invalid strategy: {strategy}. Use: skip, overwrite, merge" });

            try
            {
                var result = await importService.ImportAsync(package, conflictStrategy, ct);
                return result.Success
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import failed");
                return Results.Problem(
                    detail: ex.Message,
                    title: "Import failed",
                    statusCode: 500);
            }
        }).Produces<PortableImportResult>()
          .Produces<PortableImportResult>(422);

        // GET /api/portability/schema
        group.MapGet("/schema", (HttpContext context) =>
        {
            if (!AuthorizationHelpers.IsSuperAdmin(context))
                return AuthorizationHelpers.Forbidden();

            return Results.Ok(new
            {
                version = CoreSchema.Version,
                minReadableVersion = CoreSchema.MinReadableVersion,
                compatibility = CoreSchema.GetCompatibilityInfo()
            });
        });
    }
}
