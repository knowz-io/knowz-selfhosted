using Azure;
using Azure.AI.OpenAI;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class OpenAIExtensions
{
    /// <summary>
    /// Registers AzureOpenAIService with Azure OpenAI SDK client.
    /// If credentials are not configured, registers NoOpOpenAIService instead.
    /// </summary>
    public static IServiceCollection AddSelfHostedOpenAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            // Register no-op services — AI features disabled, auth/admin still work
            services.AddScoped<IOpenAIService, NoOpOpenAIService>();
            services.AddScoped<IContentAmendmentService, NoOpOpenAIService>();
            services.AddScoped<ITextEnrichmentService, NoOpTextEnrichmentService>();
            return services;
        }

        // Register AzureOpenAIClient as singleton (thread-safe, connection pooled)
        services.AddSingleton(_ => new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey)));

        // Register AzureOpenAIService as scoped (implements both IOpenAIService and IContentAmendmentService)
        services.AddScoped<IOpenAIService, AzureOpenAIService>();
        services.AddScoped<IContentAmendmentService, AzureOpenAIService>();

        // Register TextEnrichmentService as scoped
        services.AddScoped<ITextEnrichmentService, TextEnrichmentService>();

        return services;
    }
}
