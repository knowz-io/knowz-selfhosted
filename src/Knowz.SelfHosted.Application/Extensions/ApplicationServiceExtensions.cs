using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;
using Knowz.SelfHosted.Application.Validators;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knowz.SelfHosted.Application.Extensions;

public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all self-hosted application services and the generic repository.
    /// </summary>
    public static IServiceCollection AddSelfHostedApplication(this IServiceCollection services)
    {
        // Generic repository — one line covers all entities
        services.AddScoped(typeof(ISelfHostedRepository<>), typeof(SelfHostedRepository<>));

        // Chunking service
        services.AddScoped<ISelfHostedChunkingService, SelfHostedChunkingService>();
        services.AddScoped<IVaultAccessService, VaultAccessService>();

        // Versioning and audit
        services.AddScoped<IVersioningService, VersioningService>();
        services.AddScoped<VersioningService>();

        // Application services
        services.AddScoped<KnowledgeService>();
        services.AddScoped<SearchFacade>();
        services.AddScoped<VaultService>();
        services.AddScoped<TopicService>();
        services.AddScoped<EntityService>();
        services.AddScoped<TagService>();
        services.AddScoped<InboxService>();
        services.AddScoped<FileStorageService>();
        services.AddScoped<CommentService>();

        // Portability services
        services.AddScoped<IPortableExportService, PortableExportService>();
        services.AddScoped<IPortableImportService, PortableImportService>();

        // Vault sync services
        services.AddScoped<IVaultSyncOrchestrator, VaultSyncOrchestrator>();
        services.AddScoped<IPlatformSyncClient, PlatformSyncClient>();
        services.AddScoped<IPlatformAuditLog, PlatformAuditLogService>();
        // Per-tenant platform credential store (NodeID: PlatformSyncConnection).
        services.AddScoped<IPlatformConnectionService, PlatformConnectionService>();
        services.AddSingleton<IUrlValidator, PlatformUrlValidator>();
        // Rate limiter is a singleton — sliding-window state must persist across requests
        // (V-SEC-09). NodeID PlatformSyncItemOps.
        services.AddSingleton<IPlatformSyncRateLimiter, PlatformSyncRateLimiter>();
        services.AddScoped<VaultScopedExportService>();
        services.AddScoped<FileSyncService>();

        // Content extraction — composite pattern (routes by content type)
        // Native extractors handle common formats directly via OpenXml/PdfPig
        services.AddScoped<TextFileContentExtractor>();
        services.AddScoped<PdfContentExtractor>();
        services.AddScoped<DocxContentExtractor>();
        services.AddScoped<ExcelContentExtractor>();
        services.AddScoped<PowerPointContentExtractor>();
        services.AddScoped<ImageContentExtractor>();
        // DocumentIntelligenceContentExtractor is registered conditionally by AddDocumentIntelligence()

        services.AddScoped<IFileContentExtractor>(sp =>
        {
            var extractors = new List<IFileContentExtractor>
            {
                sp.GetRequiredService<TextFileContentExtractor>(),
                sp.GetRequiredService<PdfContentExtractor>(),
                sp.GetRequiredService<DocxContentExtractor>(),
                sp.GetRequiredService<ExcelContentExtractor>(),
                sp.GetRequiredService<PowerPointContentExtractor>(),
                sp.GetRequiredService<ImageContentExtractor>()
            };

            // Document Intelligence as fallback for types not handled natively (e.g. scanned PDFs, legacy formats)
            var diExtractor = sp.GetService<DocumentIntelligenceContentExtractor>();
            if (diExtractor != null)
                extractors.Add(diExtractor);

            return new CompositeContentExtractor(extractors);
        });

        // Enrichment outbox writer
        services.AddScoped<IEnrichmentOutboxWriter, EnrichmentOutboxWriter>();

        // Prompt resolution (singleton — owns its own in-memory cache with 5-min TTL)
        services.AddSingleton<PromptResolutionService>();
        services.AddScoped<PromptManagementService>();

        // Configuration management
        services.AddScoped<IConfigurationManagementService, ConfigurationManagementService>();

        // Git sync service (implements IGitSyncService for background service resolution)
        services.AddScoped<GitSyncService>();
        services.AddScoped<IGitSyncService>(sp => sp.GetRequiredService<GitSyncService>());

        return services;
    }
}
