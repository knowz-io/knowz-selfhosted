using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Services;

public class NoOpTextEnrichmentService : ITextEnrichmentService
{
    private readonly ILogger<NoOpTextEnrichmentService> _logger;

    public NoOpTextEnrichmentService(ILogger<NoOpTextEnrichmentService> logger)
        => _logger = logger;

    public Task<string?> GenerateTitleAsync(string content, CancellationToken ct = default)
    {
        _logger.LogDebug("OpenAI not configured. Title generation unavailable.");
        return Task.FromResult<string?>(null);
    }

    public Task<string?> SummarizeAsync(string content, int maxWords = 100, CancellationToken ct = default)
    {
        _logger.LogDebug("OpenAI not configured. Summarization unavailable.");
        return Task.FromResult<string?>(null);
    }

    public Task<List<string>> ExtractTagsAsync(string title, string content, int maxTags = 5, CancellationToken ct = default)
    {
        _logger.LogDebug("OpenAI not configured. Tag extraction unavailable.");
        return Task.FromResult(new List<string>());
    }
}
