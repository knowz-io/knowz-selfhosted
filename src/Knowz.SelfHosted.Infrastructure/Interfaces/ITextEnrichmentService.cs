namespace Knowz.SelfHosted.Infrastructure.Interfaces;

/// <summary>
/// AI-powered text enrichment operations for knowledge items.
/// Generates titles, summaries, and tags from content.
/// </summary>
public interface ITextEnrichmentService
{
    /// <summary>
    /// Generates a concise, descriptive title from content.
    /// Returns null if generation fails or AI is unavailable.
    /// </summary>
    Task<string?> GenerateTitleAsync(string content, CancellationToken ct = default);

    /// <summary>
    /// Generates a plain-text summary of the content.
    /// Returns null if generation fails or AI is unavailable.
    /// </summary>
    /// <param name="content">Content to summarize</param>
    /// <param name="maxWords">Target summary length in words (default 100)</param>
    /// <param name="ct">Cancellation token</param>
    Task<string?> SummarizeAsync(string content, int maxWords = 100, CancellationToken ct = default);

    /// <summary>
    /// Extracts relevant tags/keywords from content.
    /// Returns empty list if extraction fails or AI is unavailable.
    /// </summary>
    /// <param name="title">Title for context (may be empty)</param>
    /// <param name="content">Content to extract tags from</param>
    /// <param name="maxTags">Maximum number of tags (default 5)</param>
    /// <param name="ct">Cancellation token</param>
    Task<List<string>> ExtractTagsAsync(string title, string content, int maxTags = 5, CancellationToken ct = default);
}
