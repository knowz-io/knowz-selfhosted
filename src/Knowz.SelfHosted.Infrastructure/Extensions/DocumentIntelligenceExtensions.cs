using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class DocumentIntelligenceExtensions
{
    /// <summary>
    /// Registers Azure Document Intelligence client and extractor with three-tier priority:
    /// 1. Endpoint + ApiKey → AzureKeyCredential (dev machine, docker-compose)
    /// 2. Endpoint only    → DefaultAzureCredential (managed identity)
    /// 3. No endpoint      → no registration (PdfPig handles text PDFs as fallback)
    /// </summary>
    public static IServiceCollection AddDocumentIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["AzureDocumentIntelligence:Endpoint"];
        var apiKey = configuration["AzureDocumentIntelligence:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
            return services;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Explicit API key auth (dev machine, docker-compose)
            services.AddSingleton(_ => new DocumentIntelligenceClient(
                new Uri(endpoint), new AzureKeyCredential(apiKey)));
        }
        else
        {
            // Managed identity / DefaultAzureCredential (Azure App Service)
            var managedIdentityClientId = configuration["AZURE_CLIENT_ID"];
            var credential = new DefaultAzureCredential(
                new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentityClientId });
            services.AddSingleton(_ => new DocumentIntelligenceClient(
                new Uri(endpoint), credential));
        }

        services.AddScoped<DocumentIntelligenceContentExtractor>();

        return services;
    }
}
