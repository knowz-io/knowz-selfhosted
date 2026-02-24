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
    /// Registers search services with three-tier priority:
    /// 1. KnowzPlatform:Enabled → LocalVectorSearchService (cosine similarity on stored embeddings)
    /// 2. AzureAISearch configured → AzureSearchService (vector + keyword)
    /// 3. Neither → NoOpSearchService
    /// </summary>
    public static IServiceCollection AddSelfHostedSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tier 1: Knowz Platform mode → local vector search using embeddings from ContentChunks
        var platformEnabled = string.Equals(
            configuration["KnowzPlatform:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

        if (platformEnabled)
        {
            services.AddScoped<ISearchService, LocalVectorSearchService>();
            return services;
        }

        // Tier 2: Azure AI Search
        var endpoint = configuration["AzureAISearch:Endpoint"];
        var apiKey = configuration["AzureAISearch:ApiKey"];
        var indexName = configuration["AzureAISearch:IndexName"];

        if (!string.IsNullOrWhiteSpace(endpoint) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(indexName))
        {
            var credential = new AzureKeyCredential(apiKey);

            services.AddSingleton(_ => new SearchClient(
                new Uri(endpoint),
                indexName,
                credential));

            services.AddSingleton(_ => new SearchIndexClient(
                new Uri(endpoint),
                credential));

            services.AddScoped<ISearchService, AzureSearchService>();
            return services;
        }

        // Tier 3: NoOp — search features disabled, auth/admin still work
        services.AddScoped<ISearchService, NoOpSearchService>();
        return services;
    }
}
