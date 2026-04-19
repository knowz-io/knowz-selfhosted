using Azure.Core;
using Azure.Storage.Blobs;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class StorageExtensions
{
    /// <summary>
    /// Registers file storage provider based on configuration.
    /// Fallback to LocalFileStorageProvider if Azure not configured.
    ///
    /// MI swap (SH_ENTERPRISE_MI_SWAP §2.2): BlobServiceClient now authenticates via
    /// the DI-injected <see cref="TokenCredential"/> against `Storage:Azure:AccountUrl`.
    /// Legacy `Storage:Azure:ConnectionString` is no longer read — leaving it configured
    /// has no effect (and will be removed from bicep by partition α).
    /// </summary>
    public static IServiceCollection AddSelfHostedFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"];
        var accountUrl = configuration["Storage:Azure:AccountUrl"];
        var containerName = configuration["Storage:Azure:ContainerName"];

        // Azure Blob Storage (primary) — requires endpoint URL + MI role assignment
        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(accountUrl) &&
            !string.IsNullOrWhiteSpace(containerName))
        {
            services.AddSingleton(sp =>
            {
                var credential = sp.GetRequiredService<TokenCredential>();
                var logger = sp.GetRequiredService<ILogger<AzureBlobStorageProvider>>();
                try
                {
                    return new BlobServiceClient(new Uri(accountUrl), credential);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize BlobServiceClient with MI. AccountUrl={Url}", accountUrl);
                    throw;
                }
            });

            services.AddScoped<IFileStorageProvider, AzureBlobStorageProvider>();
            return services;
        }

        // Local Filesystem (fallback)
        services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();

        return services;
    }
}
