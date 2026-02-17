using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Knowz.SelfHosted.Infrastructure.Services;

/// <summary>
/// AI-powered text enrichment using Azure OpenAI.
/// Generates titles, summaries, and tags from content.
/// </summary>
public class TextEnrichmentService : ITextEnrichmentService
{
    private readonly AzureOpenAIClient _client;
    private readonly string _chatDeployment;
    private readonly ILogger<TextEnrichmentService> _logger;

    internal const int MaxContentChars = 12_000;

    public TextEnrichmentService(
        AzureOpenAIClient client,
        IConfiguration configuration,
        ILogger<TextEnrichmentService> logger)
    {
        _client = client;
        _chatDeployment = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required");
        _logger = logger;
    }

    public async Task<string?> GenerateTitleAsync(string content, CancellationToken ct = default)
    {
        try
        {
            var truncated = TruncateContent(content);
            var chatClient = _client.GetChatClient(_chatDeployment);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(TitleSystemPrompt),
                ChatMessage.CreateUserMessage(truncated)
            };

            var options = new ChatCompletionOptions();
            if (!IsUnsupportedMaxTokens())
                options.MaxOutputTokenCount = 50;

            var result = await chatClient.CompleteChatAsync(messages, options, ct);
            var title = result.Value.Content[0].Text.Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate title from content");
            return null;
        }
    }

    public async Task<string?> SummarizeAsync(string content, int maxWords = 100, CancellationToken ct = default)
    {
        try
        {
            var truncated = TruncateContent(content);
            var chatClient = _client.GetChatClient(_chatDeployment);

            var systemPrompt = string.Format(SummarizeSystemPrompt, maxWords);
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(systemPrompt),
                ChatMessage.CreateUserMessage(truncated)
            };

            var options = new ChatCompletionOptions();
            if (!IsUnsupportedMaxTokens())
                options.MaxOutputTokenCount = maxWords * 2;

            var result = await chatClient.CompleteChatAsync(messages, options, ct);
            var summary = result.Value.Content[0].Text.Trim();
            return string.IsNullOrWhiteSpace(summary) ? null : summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to summarize content");
            return null;
        }
    }

    public async Task<List<string>> ExtractTagsAsync(string title, string content, int maxTags = 5, CancellationToken ct = default)
    {
        try
        {
            var truncated = TruncateContent(content);
            var chatClient = _client.GetChatClient(_chatDeployment);

            var systemPrompt = string.Format(TagsSystemPrompt, maxTags);
            var userPrompt = string.IsNullOrWhiteSpace(title)
                ? truncated
                : $"Title: {title}\n\n{truncated}";

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(systemPrompt),
                ChatMessage.CreateUserMessage(userPrompt)
            };

            var options = new ChatCompletionOptions();
            if (!IsUnsupportedMaxTokens())
                options.MaxOutputTokenCount = 200;

            var result = await chatClient.CompleteChatAsync(messages, options, ct);
            var responseText = result.Value.Content[0].Text.Trim();

            return ParseTagsJson(responseText, maxTags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract tags from content");
            return new List<string>();
        }
    }

    // --- Internal helpers ---

    internal static string TruncateContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
        return content.Length > MaxContentChars ? content[..MaxContentChars] : content;
    }

    internal static List<string> ParseTagsJson(string responseText, int maxTags)
    {
        // Strip markdown code block wrapper if present
        var jsonText = responseText;
        var codeBlockMatch = Regex.Match(responseText, @"```(?:json)?\s*([\s\S]*?)\s*```");
        if (codeBlockMatch.Success)
            jsonText = codeBlockMatch.Groups[1].Value.Trim();

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(jsonText);
            if (tags != null)
                return tags.Where(t => !string.IsNullOrWhiteSpace(t)).Take(maxTags).ToList();
        }
        catch (JsonException)
        {
            // Response wasn't valid JSON — return empty
        }

        return new List<string>();
    }

    private bool IsUnsupportedMaxTokens()
    {
        return _chatDeployment.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
               _chatDeployment.Contains("o3", StringComparison.OrdinalIgnoreCase) ||
               _chatDeployment.Contains("o4-mini", StringComparison.OrdinalIgnoreCase) ||
               _chatDeployment.Contains("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    internal const string TitleSystemPrompt =
        "You are a title generator. Given the content below, generate a single concise, descriptive title of 5 to 10 words. Return ONLY the title text, nothing else. Do not include quotes, prefixes, or explanations.";

    internal const string SummarizeSystemPrompt =
        "You are a summarization assistant. Summarize the content below in {0} words or fewer. Write a clear, factual summary capturing the key points. Return ONLY the summary text, nothing else. " +
        "BREVITY MATCHING: If content is under 5 words, echo it or provide a single brief phrase. " +
        "If under 20 words, 1-2 sentences max. NEVER pad short content with filler or meta-commentary. " +
        "NEVER describe what the content 'is' or 'consists of' — just convey its meaning.";

    internal const string TagsSystemPrompt =
        "You are a tag extraction assistant. Extract up to {0} relevant tags or keywords from the content below. Return ONLY a JSON array of lowercase strings. Example: [\"machine-learning\", \"python\", \"data-analysis\"]";
}
