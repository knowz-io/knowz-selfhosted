using Azure;
using Azure.AI.OpenAI;
using Knowz.Core.Interfaces;
using Knowz.Core.Models;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Knowz.SelfHosted.Infrastructure.Services;

/// <summary>
/// Azure OpenAI implementation of IOpenAIService and IContentAmendmentService.
/// Three operations: embedding generation, context-based Q&A, and AI content editing.
/// </summary>
public class AzureOpenAIService : IOpenAIService, IContentAmendmentService
{
    private readonly AzureOpenAIClient _client;
    private readonly string _chatDeployment;
    private readonly string _embeddingDeployment;
    private readonly string _defaultSystemPrompt;
    private readonly ILogger<AzureOpenAIService> _logger;

    internal const int DefaultTokenBudget = 4000;
    internal const int ResearchTokenBudget = 8000;
    internal const int ApproxCharsPerToken = 4;

    public AzureOpenAIService(
        AzureOpenAIClient client,
        IConfiguration configuration,
        ILogger<AzureOpenAIService> logger)
    {
        _client = client;
        _chatDeployment = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName is required");
        _embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:EmbeddingDeploymentName is required");
        _defaultSystemPrompt = configuration["SelfHosted:SystemPrompt"]
            ?? DefaultSystemPrompt;
        _logger = logger;
    }

    /// <inheritdoc />
    public virtual async Task<float[]?> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_embeddingDeployment);
            var result = await embeddingClient.GenerateEmbeddingAsync(
                text, cancellationToken: cancellationToken);

            return result.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to generate embedding, vector search will be unavailable for this query");
            return null;
        }
    }

    /// <inheritdoc />
    public virtual async Task<AnswerResponse> AnswerQuestionAsync(
        string question,
        List<SearchResultItem> searchResults,
        string? vaultSystemPrompt = null,
        bool researchMode = false,
        CancellationToken cancellationToken = default)
    {
        var tokenBudget = researchMode ? ResearchTokenBudget : DefaultTokenBudget;
        var contextText = BuildContext(searchResults, tokenBudget);

        if (string.IsNullOrWhiteSpace(contextText))
        {
            return new AnswerResponse
            {
                Answer = "I don't have enough information in the knowledge base to answer this question.",
                SourceKnowledgeIds = new List<Guid>(),
                Confidence = 0
            };
        }

        var systemPrompt = vaultSystemPrompt ?? _defaultSystemPrompt;

        var chatClient = _client.GetChatClient(_chatDeployment);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(
                $"Context from knowledge base:\n\n{contextText}\n\n---\n\nQuestion: {question}")
        };

        var options = new ChatCompletionOptions();
        // O-series and GPT-5 models don't support max_tokens parameter via Azure SDK
        var isUnsupportedMaxTokens = _chatDeployment.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("o3", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("o4-mini", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("gpt-5", StringComparison.OrdinalIgnoreCase);
        if (!isUnsupportedMaxTokens)
        {
            options.MaxOutputTokenCount = researchMode ? 4000 : 2000;
        }

        _logger.LogInformation(
            "Answering question: '{Question}' with {SourceCount} sources, research={Research}",
            question.Length > 50 ? question[..50] + "..." : question,
            searchResults.Count, researchMode);

        var result = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

        var sourceIds = searchResults
            .Where(r => r.KnowledgeId != Guid.Empty)
            .Select(r => r.KnowledgeId)
            .Distinct()
            .ToList();

        return new AnswerResponse
        {
            Answer = result.Value.Content[0].Text,
            SourceKnowledgeIds = sourceIds,
            Confidence = searchResults.Count > 0
                ? Math.Min(1.0, searchResults.Average(r => r.Score))
                : 0
        };
    }

    /// <inheritdoc />
    public virtual async Task<string> ApplyContentUpdateAsync(
        string existingContent,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var chatClient = _client.GetChatClient(_chatDeployment);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(
                "You are a document editor. Apply the user's instruction to modify the document. " +
                "Return ONLY the updated document content — no explanations, no markdown fences."),
            ChatMessage.CreateUserMessage(
                $"Document:\n\n{existingContent}\n\n---\n\nInstruction: {instruction}")
        };

        var options = new ChatCompletionOptions();
        var isUnsupportedMaxTokens = _chatDeployment.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("o3", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("o4-mini", StringComparison.OrdinalIgnoreCase) ||
                                     _chatDeployment.Contains("gpt-5", StringComparison.OrdinalIgnoreCase);
        if (!isUnsupportedMaxTokens)
        {
            options.MaxOutputTokenCount = 4000;
        }

        _logger.LogInformation(
            "Amending content: instructionLength={InstructionLength}, contentLength={ContentLength}",
            instruction.Length, existingContent.Length);

        var result = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        return result.Value.Content[0].Text;
    }

    // --- Internal helpers (visible for testing) ---

    internal static string BuildContext(List<SearchResultItem> results, int tokenBudget)
    {
        var charBudget = tokenBudget * ApproxCharsPerToken;
        var sb = new System.Text.StringBuilder();
        var currentLength = 0;

        foreach (var result in results.OrderByDescending(r => r.Score))
        {
            var block = FormatSourceBlock(result);
            if (currentLength + block.Length > charBudget)
            {
                var remaining = charBudget - currentLength;
                if (remaining > 200)
                {
                    sb.AppendLine(block[..remaining] + "...");
                }
                break;
            }
            sb.AppendLine(block);
            sb.AppendLine();
            currentLength += block.Length;
        }

        return sb.ToString().TrimEnd();
    }

    internal static string FormatSourceBlock(SearchResultItem result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"### {result.Title} [{result.KnowledgeId}]");

        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            sb.AppendLine($"Summary: {result.Summary}");
        }

        var content = result.Content;
        if (content.Length > 2000 && !string.IsNullOrWhiteSpace(result.Summary))
        {
            content = result.Summary + "\n\n" + content[..1500] + "...";
        }
        sb.AppendLine(content);

        if (!string.IsNullOrWhiteSpace(result.VaultName))
        {
            sb.AppendLine($"Source: {result.VaultName}");
        }

        return sb.ToString();
    }

    internal const string DefaultSystemPrompt = """
        You are a helpful knowledge assistant. Answer questions based on the provided context from the knowledge base.

        Guidelines:
        - Answer based on the provided context. If the context doesn't contain enough information, say so.
        - Reference specific sources by their title when citing information.
        - Be concise but thorough. Provide specific details from the sources.
        - If multiple sources provide different perspectives, present them.
        - Format your response using markdown for readability.
        """;
}
