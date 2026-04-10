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
    /// Pattern: matches SearchExtensions.cs conditional registration.
    /// </summary>
    public static IServiceCollection AddSelfHostedFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"];
        var azureConnectionString = configuration["Storage:Azure:ConnectionString"];
        var azureContainerName = configuration["Storage:Azure:ContainerName"];

        // Azure Blob Storage (primary)
        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(azureConnectionString) &&
            !string.IsNullOrWhiteSpace(azureContainerName))
        {
            // Register BlobServiceClient as singleton (thread-safe, connection pooled)
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<AzureBlobStorageProvider>>();
                try
                {
                    return new BlobServiceClient(azureConnectionString);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize BlobServiceClient. Check connection string.");
                    throw;
                }
            });

            // Register AzureBlobStorageProvider as scoped
            services.AddScoped<IFileStorageProvider, AzureBlobStorageProvider>();

            return services;
        }

        // Local Filesystem (fallback)
        services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();

        return services;
    }
}
