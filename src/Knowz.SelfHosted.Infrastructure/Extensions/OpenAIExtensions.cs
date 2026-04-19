using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Knowz.SelfHosted.Infrastructure.Services.GitCommitHistory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class OpenAIExtensions
{
    /// <summary>
    /// Registers AI services with three-tier priority:
    /// 1. KnowzPlatform:Enabled → PlatformAIService + PlatformTextEnrichmentService
    /// 2. AzureOpenAI configured → AzureOpenAIService + TextEnrichmentService
    /// 3. Neither → NoOpOpenAIService + NoOpTextEnrichmentService
    /// </summary>
    public static IServiceCollection AddSelfHostedOpenAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Tier 1: Knowz Platform AI Proxy
        var platformEnabled = string.Equals(
            configuration["KnowzPlatform:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

        if (platformEnabled)
        {
            var baseUrl = configuration["KnowzPlatform:BaseUrl"];
            var platformApiKey = configuration["KnowzPlatform:ApiKey"];

            if (!string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(platformApiKey))
            {
                // Register named HttpClient for platform API calls
                services.AddHttpClient("KnowzPlatformClient", client =>
                {
                    client.BaseAddress = new Uri(baseUrl);
                    client.DefaultRequestHeaders.Add("X-Api-Key", platformApiKey);
                    client.Timeout = TimeSpan.FromSeconds(120);
                });

                services.AddScoped<IOpenAIService, PlatformAIService>();
                services.AddScoped<IContentAmendmentService, PlatformAIService>();
                services.AddScoped<IStreamingOpenAIService, PlatformAIService>();
                services.AddScoped<ITextEnrichmentService, PlatformTextEnrichmentService>();
                // NODE-4: commit-history elaboration uses the platform completion endpoint
                services.AddScoped<ICommitElaborationLlmClient, PlatformCommitElaborationLlmClient>();
                return services;
            }
        }

        // Tier 2: Azure OpenAI. MI-first per SH_ENTERPRISE_MI_SWAP §2.3; however,
        // `AzureOpenAI:ApiKey` takes precedence when present so external-mode
        // deploys (pointing at a shared/third-party OpenAI resource where the
        // UAMI has no data-plane role) can still authenticate.
        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            services.AddSingleton(sp =>
            {
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                }
                var credential = sp.GetRequiredService<TokenCredential>();
                return new AzureOpenAIClient(new Uri(endpoint), credential);
            });

            services.AddScoped<IOpenAIService, AzureOpenAIService>();
            services.AddScoped<IContentAmendmentService, AzureOpenAIService>();
            services.AddScoped<IStreamingOpenAIService, AzureOpenAIService>();
            services.AddScoped<ITextEnrichmentService, TextEnrichmentService>();
            // NODE-4: Azure OpenAI tier also uses the NoOp commit LLM client for now.
            // Commit-history elaboration is a platform-only feature today; Azure OpenAI
            // direct callers get metadata-only stubs. Debt item: add an Azure-backed impl.
            services.AddScoped<ICommitElaborationLlmClient, NoOpCommitElaborationLlmClient>();
            return services;
        }

        // Tier 3: NoOp — AI features disabled, auth/admin still work
        services.AddScoped<IOpenAIService, NoOpOpenAIService>();
        services.AddScoped<IContentAmendmentService, NoOpOpenAIService>();
        services.AddScoped<IStreamingOpenAIService, NoOpOpenAIService>();
        services.AddScoped<ITextEnrichmentService, NoOpTextEnrichmentService>();
        services.AddScoped<ICommitElaborationLlmClient, NoOpCommitElaborationLlmClient>();
        return services;
    }
}
