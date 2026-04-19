using Azure.Core;
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
    /// Registers search services with four-tier priority:
    /// 1. KnowzPlatform:Enabled → PlatformSearchService (proxies to Knowz Platform API)
    /// 2. AzureAISearch configured → AzureSearchService (vector + keyword)
    /// 3. AzureOpenAI configured → LocalVectorSearchService (local vector + keyword)
    /// 4. Fallback → DatabaseSearchService (SQL LIKE keyword search)
    /// </summary>
    public static IServiceCollection AddSelfHostedSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tier 1: Knowz Platform mode → proxy search to the platform API
        var platformEnabled = string.Equals(
            configuration["KnowzPlatform:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

        if (platformEnabled)
        {
            services.AddScoped<ISearchService, PlatformSearchService>();
            return services;
        }

        // Tier 2: Azure AI Search (MI swap SH_ENTERPRISE_MI_SWAP §2.4 — endpoint+index only;
        // TokenCredential from DI, AzureAISearch:ApiKey no longer consulted).
        var endpoint = configuration["AzureAISearch:Endpoint"];
        var indexName = configuration["AzureAISearch:IndexName"];

        if (!string.IsNullOrWhiteSpace(endpoint) &&
            !string.IsNullOrWhiteSpace(indexName))
        {
            services.AddSingleton(sp =>
            {
                var credential = sp.GetRequiredService<TokenCredential>();
                return new SearchClient(new Uri(endpoint), indexName, credential);
            });

            services.AddSingleton(sp =>
            {
                var credential = sp.GetRequiredService<TokenCredential>();
                return new SearchIndexClient(new Uri(endpoint), credential);
            });

            services.AddScoped<ISearchService, AzureSearchService>();
            return services;
        }

        // Tier 3: Local vector search (if OpenAI/embedding config present)
        var openAiEndpoint = configuration["AzureOpenAI:Endpoint"];
        var openAiDeployment = configuration["AzureOpenAI:DeploymentName"];

        if (!string.IsNullOrWhiteSpace(openAiEndpoint) || !string.IsNullOrWhiteSpace(openAiDeployment))
        {
            services.AddScoped<ISearchService, LocalVectorSearchService>();
            return services;
        }

        // Tier 4: Database fallback — SQL LIKE keyword search (no vector/semantic)
        services.AddScoped<ISearchService, DatabaseSearchService>();
        return services;
    }
}
