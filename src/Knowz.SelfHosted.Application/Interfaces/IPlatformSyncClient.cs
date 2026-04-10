namespace Knowz.SelfHosted.Application.Interfaces;

using Knowz.Core.Portability;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Infrastructure.Data.Entities;

/// <summary>
/// HTTP client for communicating with the Knowz platform sync API.
/// </summary>
public interface IPlatformSyncClient
{
    /// <summary>
    /// Check platform schema compatibility.
    /// </summary>
    Task<PlatformSchemaResponse> GetSchemaAsync(VaultSyncLink link, CancellationToken ct = default);

    /// <summary>
    /// Register this selfhosted instance as a sync partner.
    /// </summary>
    Task RegisterPartnerAsync(VaultSyncLink link, string partnerName, CancellationToken ct = default);

    /// <summary>
    /// Export a delta from the platform vault (entities changed since cursor).
    /// </summary>
    Task<PortableExportPackage> ExportDeltaAsync(
        VaultSyncLink link, DateTime? since, int page = 1, int pageSize = 500,
        CancellationToken ct = default);

    /// <summary>
    /// Import a delta package into the platform vault.
    /// </summary>
    Task<PlatformImportResponse> ImportDeltaAsync(
        VaultSyncLink link, PortableExportPackage package,
        CancellationToken ct = default);
}
