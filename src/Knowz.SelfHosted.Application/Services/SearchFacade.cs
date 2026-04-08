using Knowz.Core.Interfaces;
using Knowz.Core.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Application.Services;

/// <summary>
/// Facade for search and Q&A operations.
/// Uses DbContext for EF.Functions.Like queries and ISearchService/IOpenAIService for hybrid search.
/// </summary>
public class SearchFacade
{
    private readonly SelfHostedDbContext _db;
    private readonly ISearchService _searchService;
    private readonly IOpenAIService _openAIService;
    private readonly IStreamingOpenAIService _streamingOpenAIService;
    private readonly ILogger<SearchFacade> _logger;

    public SearchFacade(
        SelfHostedDbContext db,
        ISearchService searchService,
        IOpenAIService openAIService,
        IStreamingOpenAIService streamingOpenAIService,
        ILogger<SearchFacade> logger)
    {
        _db = db;
        _searchService = searchService;
        _openAIService = openAIService;
        _streamingOpenAIService = streamingOpenAIService;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchKnowledgeAsync(
        string query, int limit, Guid? vaultId, bool includeChildVaults,
        List<string> tags, bool requireAllTags,
        DateTime? startDate, DateTime? endDate, string? type, CancellationToken ct,
        List<Guid>? accessibleVaultIds = null)
    {
        var embedding = await _openAIService.GenerateEmbeddingAsync(query, ct);

        // Request extra results when type-filtering or vault-filtering client-side
        var fetchLimit = (string.IsNullOrWhiteSpace(type) && accessibleVaultIds == null) ? limit : limit * 3;
        var results = await _searchService.HybridSearchAsync(
            query, embedding, vaultId, includeChildVaults,
            tags.Count > 0 ? tags : null, requireAllTags, startDate, endDate, fetchLimit, ct);

        // Post-filter by vault access
        if (accessibleVaultIds != null)
            results = await FilterByVaultAccessAsync(results, accessibleVaultIds, ct);

        if (!string.IsNullOrWhiteSpace(type))
        {
            results = results
                .Where(r => string.Equals(r.KnowledgeType, type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        results = results.Take(limit).ToList();

        var items = results.Select(r => new SearchResultResponse(
            r.KnowledgeId,
            r.Title,
            Truncate(r.Content, 500),
            Truncate(r.Summary, 500),
            r.VaultName,
            r.TopicName,
            r.Tags,
            r.KnowledgeType,
            r.FilePath,
            r.Score));

        return new SearchResponse(items, results.Count);
    }

    public async Task<AskAnswerResponse> AskQuestionAsync(
        string question, Guid? vaultId, bool researchMode, CancellationToken ct,
        List<Guid>? accessibleVaultIds = null)
    {
        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            question, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        // Post-filter by vault access
        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        var answer = await _openAIService.AnswerQuestionAsync(
            question, searchResults, null, researchMode, ct);

        var sources = answer.SourceKnowledgeIds.Select(id => new SourceRef(id));
        return new AskAnswerResponse(answer.Answer, sources, answer.Confidence);
    }

    public async Task<FilePatternResponse> SearchByFilePatternAsync(
        string pattern, bool countOnly, int limit, CancellationToken ct)
    {
        var likePattern = KnowledgeService.ConvertWildcardToLike(pattern);
        var query = _db.KnowledgeItems
            .Where(k => k.FilePath != null && EF.Functions.Like(k.FilePath, likePattern));

        if (countOnly)
        {
            var count = await query.CountAsync(ct);
            return new FilePatternResponse(pattern, null, count);
        }

        var items = await query
            .OrderByDescending(k => k.UpdatedAt)
            .Take(limit)
            .Select(k => new FilePatternItem(k.Id, k.Title, k.FilePath, k.Type.ToString(), k.CreatedAt))
            .ToListAsync(ct);

        return new FilePatternResponse(pattern, items, items.Count);
    }

    public async Task<FilePatternResponse> SearchByTitlePatternAsync(
        string pattern, bool countOnly, int limit, CancellationToken ct)
    {
        var likePattern = KnowledgeService.ConvertWildcardToLike(pattern);
        var query = _db.KnowledgeItems
            .Where(k => EF.Functions.Like(k.Title, likePattern));

        if (countOnly)
        {
            var count = await query.CountAsync(ct);
            return new FilePatternResponse(pattern, null, count);
        }

        var items = await query
            .OrderByDescending(k => k.UpdatedAt)
            .Take(limit)
            .Select(k => new FilePatternItem(k.Id, k.Title, k.FilePath, k.Type.ToString(), k.CreatedAt))
            .ToListAsync(ct);

        return new FilePatternResponse(pattern, items, items.Count);
    }

    public async Task<StreamingAskResult> AskQuestionStreamingAsync(
        string question, Guid? vaultId, bool researchMode, CancellationToken ct,
        List<Guid>? accessibleVaultIds = null)
    {
        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            question, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        var sources = searchResults
            .Where(r => r.KnowledgeId != Guid.Empty)
            .Select(r => r.KnowledgeId)
            .Distinct()
            .Select(id => new SourceRef(id));

        var confidence = searchResults.Count > 0
            ? Math.Min(1.0, searchResults.Max(r => r.Score))
            : 0;

        var tokenStream = _streamingOpenAIService.AnswerQuestionStreamingAsync(
            question, searchResults, null, researchMode, ct);

        return new StreamingAskResult(sources, confidence, tokenStream);
    }

    public async Task<StreamingChatResult> ChatWithHistoryStreamingAsync(
        string question,
        List<ChatMessageDto>? conversationHistory,
        Guid? vaultId,
        bool researchMode,
        int maxTurns,
        CancellationToken ct,
        List<Guid>? accessibleVaultIds = null)
    {
        var compositeQuestion = BuildCompositeQuestion(question, conversationHistory, maxTurns);

        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            question, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        var sources = searchResults
            .Where(r => r.KnowledgeId != Guid.Empty)
            .Select(r => r.KnowledgeId)
            .Distinct()
            .Select(id => new SourceRef(id));

        var confidence = searchResults.Count > 0
            ? Math.Min(1.0, searchResults.Max(r => r.Score))
            : 0;

        var tokenStream = _streamingOpenAIService.AnswerQuestionStreamingAsync(
            compositeQuestion, searchResults, null, researchMode, ct);

        return new StreamingChatResult(sources, confidence, tokenStream);
    }

    public async Task<ChatResponse> ChatWithHistoryAsync(
        string question,
        List<ChatMessageDto>? conversationHistory,
        Guid? vaultId,
        bool researchMode,
        int maxTurns,
        CancellationToken ct,
        List<Guid>? accessibleVaultIds = null)
    {
        // Build composite question string with conversation history context
        var compositeQuestion = BuildCompositeQuestion(question, conversationHistory, maxTurns);

        // Generate embedding from the current question ONLY (not the composite string)
        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            question, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        // Post-filter by vault access
        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        var answer = await _openAIService.AnswerQuestionAsync(
            compositeQuestion, searchResults, null, researchMode, ct);

        var sources = answer.SourceKnowledgeIds.Select(id => new SourceRef(id));
        return new ChatResponse(answer.Answer, sources, answer.Confidence);
    }

    internal static string BuildCompositeQuestion(
        string currentQuestion,
        List<ChatMessageDto>? history,
        int maxTurns)
    {
        if (history == null || history.Count == 0)
            return currentQuestion;

        // Truncate to last maxTurns * 2 messages (each turn = user + assistant)
        var maxMessages = maxTurns * 2;
        var truncatedHistory = history.Count > maxMessages
            ? history.Skip(history.Count - maxMessages).ToList()
            : history;

        var lines = new List<string> { "Previous conversation:" };
        foreach (var msg in truncatedHistory)
        {
            var role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant";
            lines.Add($"{role}: {msg.Content}");
        }

        lines.Add("");
        lines.Add($"Current question: {currentQuestion}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Post-filters search results to only include items belonging to accessible vaults (or items with no vault).
    /// </summary>
    private async Task<List<SearchResultItem>> FilterByVaultAccessAsync(
        List<SearchResultItem> results, List<Guid> accessibleVaultIds, CancellationToken ct)
    {
        if (results.Count == 0) return results;

        var knowledgeIds = results.Select(r => r.KnowledgeId).Distinct().ToList();

        // Find knowledge IDs that belong to at least one accessible vault
        var accessibleKnowledgeIds = await _db.KnowledgeVaults
            .Where(kv => knowledgeIds.Contains(kv.KnowledgeId) && accessibleVaultIds.Contains(kv.VaultId))
            .Select(kv => kv.KnowledgeId)
            .Distinct()
            .ToListAsync(ct);

        // Also include items that have no vault associations (unassigned items)
        var idsWithVaults = await _db.KnowledgeVaults
            .Where(kv => knowledgeIds.Contains(kv.KnowledgeId))
            .Select(kv => kv.KnowledgeId)
            .Distinct()
            .ToListAsync(ct);

        var idsWithoutVaults = knowledgeIds.Except(idsWithVaults).ToHashSet();

        var allowed = accessibleKnowledgeIds.Concat(idsWithoutVaults).ToHashSet();
        return results.Where(r => allowed.Contains(r.KnowledgeId)).ToList();
    }

    internal static string? Truncate(string? value, int maxLength)
        => value != null && value.Length > maxLength ? value[..maxLength] + "..." : value;
}
