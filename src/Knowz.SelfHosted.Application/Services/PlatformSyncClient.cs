namespace Knowz.SelfHosted.Application.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Knowz.Core.Portability;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client for communicating with the Knowz platform sync API.
/// All endpoints are under /api/v1/sync/ and authenticated via X-Api-Key header.
/// </summary>
public class PlatformSyncClient : IPlatformSyncClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlatformSyncClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlatformSyncClient(
        IHttpClientFactory httpClientFactory,
        ILogger<PlatformSyncClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PlatformSchemaResponse> GetSchemaAsync(VaultSyncLink link, CancellationToken ct = default)
    {
        using var client = CreateClient(link);
        var response = await client.GetAsync("/api/v1/sync/schema", ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<PlatformApiResponse<PlatformSchemaResponse>>(JsonOptions, ct);
        return envelope?.Data ?? throw new InvalidOperationException("Platform returned null schema response");
    }

    public async Task RegisterPartnerAsync(VaultSyncLink link, string partnerName, CancellationToken ct = default)
    {
        using var client = CreateClient(link);
        var body = new { PartnerName = partnerName };
        var response = await client.PostAsJsonAsync(
            $"/api/v1/sync/vaults/{link.RemoteVaultId}/register", body, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Registered sync partner for vault {RemoteVaultId}", link.RemoteVaultId);
    }

    public async Task<PortableExportPackage> ExportDeltaAsync(
        VaultSyncLink link, DateTime? since, int page = 1, int pageSize = 500,
        CancellationToken ct = default)
    {
        using var client = CreateClient(link);

        var url = $"/api/v1/sync/vaults/{link.RemoteVaultId}/export?page={page}&pageSize={pageSize}";
        if (since.HasValue)
            url += $"&since={since.Value:O}";

        _logger.LogInformation(
            "Pulling delta from platform vault {RemoteVaultId}, since={Since}, page={Page}",
            link.RemoteVaultId, since, page);

        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<PlatformApiResponse<PortableExportPackage>>(JsonOptions, ct);
        return envelope?.Data ?? throw new InvalidOperationException("Platform returned null export package");
    }

    public async Task<PlatformImportResponse> ImportDeltaAsync(
        VaultSyncLink link, PortableExportPackage package,
        CancellationToken ct = default)
    {
        using var client = CreateClient(link);

        _logger.LogInformation(
            "Pushing delta to platform vault {RemoteVaultId}, {KnowledgeCount} knowledge items",
            link.RemoteVaultId, package.Metadata.TotalKnowledgeItems);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/sync/vaults/{link.RemoteVaultId}/import", package, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<PlatformApiResponse<PlatformImportResponse>>(JsonOptions, ct);
        return envelope?.Data ?? throw new InvalidOperationException("Platform returned null import response");
    }

    private HttpClient CreateClient(VaultSyncLink link)
    {
        var client = _httpClientFactory.CreateClient("PlatformSync");
        client.BaseAddress = new Uri(link.PlatformApiUrl.TrimEnd('/'));
        client.DefaultRequestHeaders.Add("X-Api-Key", link.ApiKeyEncrypted); // TODO: decrypt
        client.Timeout = TimeSpan.FromMinutes(5);
        return client;
    }
}
