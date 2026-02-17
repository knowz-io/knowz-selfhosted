using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class SearchExtensions
{
    /// <summary>
    /// Registers AzureSearchService with Azure AI Search SDK client.
    /// If credentials are not configured, registers NoOpSearchService instead.
    /// </summary>
    public static IServiceCollection AddSelfHostedSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["AzureAISearch:Endpoint"];
        var apiKey = configuration["AzureAISearch:ApiKey"];
        var indexName = configuration["AzureAISearch:IndexName"];

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(indexName))
        {
            // Register no-op service — search features disabled, auth/admin still work
            services.AddScoped<ISearchService, NoOpSearchService>();
            return services;
        }

        var credential = new AzureKeyCredential(apiKey);

        // Register SearchClient as singleton (thread-safe, connection pooled)
        services.AddSingleton(_ => new SearchClient(
            new Uri(endpoint),
            indexName,
            credential));

        // Register SearchIndexClient for index management (create/update schema)
        services.AddSingleton(_ => new SearchIndexClient(
            new Uri(endpoint),
            credential));

        // Register AzureSearchService as scoped (uses ITenantProvider for per-request tenant)
        services.AddScoped<ISearchService, AzureSearchService>();

        return services;
    }
}
