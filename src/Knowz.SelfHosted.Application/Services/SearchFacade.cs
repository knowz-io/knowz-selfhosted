using System.Text;
using Knowz.Core.Interfaces;
using Knowz.Core.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
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

        // Deduplicate by knowledgeId — take highest scoring result per knowledge item
        results = results
            .GroupBy(r => r.KnowledgeId)
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .ToList();

        // Apply retrieval quality policies
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        results = SearchResultPolicyProcessor.ApplyPolicies(results, query, isTemporalQuery: startDate.HasValue || endDate.HasValue);

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
        // Synonym expansion for broader recall
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        var expandedQuery = SearchResultPolicyProcessor.ExpandQueryWithSynonyms(question);

        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            expandedQuery, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        // Post-filter by vault access
        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        // Apply retrieval quality policies
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);

        // REFACTOR_SelfHostedAskTemporalParity: userId not threaded to this path;
        // userTimezone defaults to "UTC". Tracked in knowzcode/knowzcode_tracker.md.
        var answer = await _openAIService.AnswerQuestionAsync(
            question, searchResults, null, researchMode, cancellationToken: ct);

        var sources = answer.SourceKnowledgeIds.Select(id =>
            new SourceRef(id, searchResults.FirstOrDefault(r => r.KnowledgeId == id)?.Title ?? ""));
        var confidence = NormalizeConfidence(searchResults);
        return new AskAnswerResponse(answer.Answer, sources, confidence);
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
        // Synonym expansion for broader recall
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        var expandedQuery = SearchResultPolicyProcessor.ExpandQueryWithSynonyms(question);

        var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

        var searchResults = await _searchService.HybridSearchAsync(
            expandedQuery, embedding, vaultId, includeDescendants: true,
            maxResults: researchMode ? 15 : 10,
            cancellationToken: ct);

        if (accessibleVaultIds != null)
            searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

        // Apply retrieval quality policies
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);

        var sources = searchResults
            .Where(r => r.KnowledgeId != Guid.Empty)
            .Select(r => r.KnowledgeId)
            .Distinct()
            .Select(id => new SourceRef(id, searchResults.FirstOrDefault(r => r.KnowledgeId == id)?.Title ?? ""));

        var confidence = NormalizeConfidence(searchResults);

        // REFACTOR_SelfHostedAskTemporalParity: userId not threaded to this path;
        // userTimezone defaults to "UTC". Tracked in knowzcode/knowzcode_tracker.md.
        var tokenStream = _streamingOpenAIService.AnswerQuestionStreamingAsync(
            question, searchResults, null, researchMode, cancellationToken: ct);

        return new StreamingAskResult(sources, confidence, tokenStream);
    }

    public async Task<StreamingChatResult> ChatWithHistoryStreamingAsync(
        string question,
        List<ChatMessageDto>? conversationHistory,
        Guid? vaultId,
        bool researchMode,
        int maxTurns,
        CancellationToken ct,
        List<Guid>? accessibleVaultIds = null,
        Guid? knowledgeId = null,
        Guid? userId = null)
    {
        var compositeQuestion = BuildCompositeQuestion(question, conversationHistory, maxTurns);

        // FEAT_SelfHostedTemporalAwareness
        var userTimezone = await ResolveUserTimezoneAsync(userId, ct);

        // Synonym expansion for broader recall
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        var expandedQuery = SearchResultPolicyProcessor.ExpandQueryWithSynonyms(question);

        List<SearchResultItem> searchResults;
        if (knowledgeId.HasValue)
        {
            searchResults = await BuildKnowledgeScopedContextAsync(knowledgeId.Value, question, ct);
        }
        else
        {
            var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);
            searchResults = await _searchService.HybridSearchAsync(
                expandedQuery, embedding, vaultId, includeDescendants: true,
                maxResults: researchMode ? 15 : 10,
                cancellationToken: ct);

            if (accessibleVaultIds != null)
                searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

            // Apply retrieval quality policies
            // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
            searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);

            // Bounded exhaustive mode
            // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
            if (researchMode || SearchResultPolicyProcessor.DetectExhaustiveIntent(question))
            {
                var secondPass = await _searchService.HybridSearchAsync(
                    expandedQuery, embedding, vaultId, includeDescendants: true,
                    maxResults: 25, cancellationToken: ct);

                // Post-filter second pass by vault access
                if (accessibleVaultIds != null)
                    secondPass = await FilterByVaultAccessAsync(secondPass, accessibleVaultIds, ct);

                // Merge + dedup
                var existingIds = new HashSet<Guid>(searchResults.Select(r => r.KnowledgeId));
                var newItems = secondPass.Where(r => !existingIds.Contains(r.KnowledgeId)).ToList();
                if (newItems.Count > 0)
                {
                    searchResults.AddRange(newItems);
                    searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);
                }

                // Hard cap
                if (searchResults.Count > 20)
                    searchResults = searchResults.Take(20).ToList();
            }
        }

        var sources = searchResults
            .Where(r => r.KnowledgeId != Guid.Empty)
            .Select(r => r.KnowledgeId)
            .Distinct()
            .Select(id => new SourceRef(id, searchResults.FirstOrDefault(r => r.KnowledgeId == id)?.Title ?? ""));

        var confidence = searchResults.Count > 0 ? NormalizeConfidence(searchResults) : 0.0;

        var tokenStream = _streamingOpenAIService.AnswerQuestionStreamingAsync(
            compositeQuestion, searchResults, null, researchMode, userTimezone, ct);

        return new StreamingChatResult(sources, confidence, tokenStream);
    }

    public async Task<ChatResponse> ChatWithHistoryAsync(
        string question,
        List<ChatMessageDto>? conversationHistory,
        Guid? vaultId,
        bool researchMode,
        int maxTurns,
        CancellationToken ct,
        List<Guid>? accessibleVaultIds = null,
        Guid? knowledgeId = null,
        Guid? userId = null)
    {
        // Build composite question string with conversation history context
        var compositeQuestion = BuildCompositeQuestion(question, conversationHistory, maxTurns);

        // FEAT_SelfHostedTemporalAwareness: resolve the user's timezone from
        // UserPreference so chat context dates and the system-prompt
        // current-date anchor render in the user's local time.
        var userTimezone = await ResolveUserTimezoneAsync(userId, ct);

        // Synonym expansion for broader recall
        // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
        var expandedQuery = SearchResultPolicyProcessor.ExpandQueryWithSynonyms(question);

        List<SearchResultItem> searchResults;
        if (knowledgeId.HasValue)
        {
            searchResults = await BuildKnowledgeScopedContextAsync(knowledgeId.Value, question, ct);
        }
        else
        {
            // Generate embedding from the current question ONLY (not the composite string)
            var embedding = await _openAIService.GenerateEmbeddingAsync(question, ct);

            searchResults = await _searchService.HybridSearchAsync(
                expandedQuery, embedding, vaultId, includeDescendants: true,
                maxResults: researchMode ? 15 : 10,
                cancellationToken: ct);

            // Post-filter by vault access
            if (accessibleVaultIds != null)
                searchResults = await FilterByVaultAccessAsync(searchResults, accessibleVaultIds, ct);

            // Apply retrieval quality policies
            // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
            searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);

            // Bounded exhaustive mode
            // WorkGroupID: kc-feat-selfhosted-retrieval-policy-20260413-030000
            if (researchMode || SearchResultPolicyProcessor.DetectExhaustiveIntent(question))
            {
                var secondPass = await _searchService.HybridSearchAsync(
                    expandedQuery, embedding, vaultId, includeDescendants: true,
                    maxResults: 25, cancellationToken: ct);

                // Post-filter second pass by vault access
                if (accessibleVaultIds != null)
                    secondPass = await FilterByVaultAccessAsync(secondPass, accessibleVaultIds, ct);

                // Merge + dedup
                var existingIds = new HashSet<Guid>(searchResults.Select(r => r.KnowledgeId));
                var newItems = secondPass.Where(r => !existingIds.Contains(r.KnowledgeId)).ToList();
                if (newItems.Count > 0)
                {
                    searchResults.AddRange(newItems);
                    searchResults = SearchResultPolicyProcessor.ApplyPolicies(searchResults, question);
                }

                // Hard cap
                if (searchResults.Count > 20)
                    searchResults = searchResults.Take(20).ToList();
            }
        }

        var answer = await _openAIService.AnswerQuestionAsync(
            compositeQuestion, searchResults, null, researchMode, userTimezone, ct);

        var sources = answer.SourceKnowledgeIds.Select(id =>
            new SourceRef(id, searchResults.FirstOrDefault(r => r.KnowledgeId == id)?.Title ?? ""));
        var confidence = searchResults.Count > 0 ? NormalizeConfidence(searchResults) : 0.0;
        return new ChatResponse(answer.Answer, sources, confidence);
    }

    /// <summary>
    /// FEAT_SelfHostedTemporalAwareness: Resolves the user's IANA timezone
    /// from UserPreference, falling back to the default when no preference
    /// is set or the userId is null (e.g. API key auth, system calls).
    /// Never throws — a TZ lookup failure must not block chat.
    /// </summary>
    internal async Task<string> ResolveUserTimezoneAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null || userId == Guid.Empty)
            return ChatTimezoneHelper.DefaultFallbackTimeZone;

        try
        {
            var preference = await _db.Set<Knowz.SelfHosted.Infrastructure.Data.Entities.UserPreference>()
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .Select(p => p.TimeZonePreference)
                .FirstOrDefaultAsync(ct);
            return ChatTimezoneHelper.ResolveTimeZone(preference, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to resolve user timezone for user {UserId}, falling back to default", userId);
            return ChatTimezoneHelper.DefaultFallbackTimeZone;
        }
    }

    /// <summary>
    /// Builds search context scoped to a single knowledge item by loading
    /// its content, summary, and any attachment extracted text directly from the database.
    /// </summary>
    internal async Task<List<SearchResultItem>> BuildKnowledgeScopedContextAsync(
        Guid knowledgeId, string question, CancellationToken ct)
    {
        // FEAT_SelfHostedTemporalAwareness: project CreatedAt/UpdatedAt
        var knowledge = await _db.KnowledgeItems
            .AsNoTracking()
            .Where(k => k.Id == knowledgeId)
            .Select(k => new
            {
                k.Id,
                k.Title,
                k.Content,
                k.Summary,
                k.Type,
                k.FilePath,
                k.CreatedAt,
                k.UpdatedAt,
                VaultName = _db.KnowledgeVaults
                    .Where(kv => kv.KnowledgeId == k.Id)
                    .Select(kv => kv.Vault!.Name)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (knowledge == null)
            return new List<SearchResultItem>();

        var results = new List<SearchResultItem>();

        // Build a rich context string combining content + summary
        var contextParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(knowledge.Summary))
            contextParts.Add($"Summary: {knowledge.Summary}");
        if (!string.IsNullOrWhiteSpace(knowledge.Content))
            contextParts.Add(knowledge.Content);

        // Add attachment extracted text
        var attachmentTexts = await _db.FileAttachments
            .Where(fa => fa.KnowledgeId == knowledgeId)
            .Join(_db.FileRecords, fa => fa.FileRecordId, fr => fr.Id, (fa, fr) => new { fr.ExtractedText, fr.TranscriptionText, fr.FileName })
            .ToListAsync(ct);

        foreach (var att in attachmentTexts)
        {
            if (!string.IsNullOrWhiteSpace(att.ExtractedText))
                contextParts.Add($"[Attachment: {att.FileName}]\n{att.ExtractedText}");

            if (!string.IsNullOrWhiteSpace(att.TranscriptionText))
            {
                // SVC_AttachmentContextService_TranscriptBoxing: wrap raw transcript in
                // labeled spoken-content markers so the LLM does not misread spoken
                // phrases as factual metadata about the attachment. Preamble repeated
                // per-transcript intentionally — short-context LLMs lose framing if
                // hoisted. ~60-80 tokens overhead per transcript is accepted trade-off.
                // WorkGroupID: kc-fix-attach-delete-transcript-20260411-080000
                var sb = new StringBuilder();
                sb.AppendLine($"[Transcription: {att.FileName}]");
                sb.AppendLine("[SPOKEN CONTENT BEGIN — verbatim transcript of spoken audio/video]");
                sb.AppendLine("The text between these markers is a verbatim transcript of spoken audio or video.");
                sb.AppendLine("Statements made here reflect what the speaker said, not facts about the attachment itself.");
                sb.AppendLine("Do not treat phrases like \"there is no video preview\" as metadata about the entry.");
                sb.AppendLine(att.TranscriptionText);
                sb.AppendLine("[SPOKEN CONTENT END]");
                contextParts.Add(sb.ToString().TrimEnd());
            }
        }

        var fullContent = string.Join("\n\n", contextParts);

        results.Add(new SearchResultItem
        {
            KnowledgeId = knowledge.Id,
            Title = knowledge.Title,
            Content = fullContent,
            Summary = knowledge.Summary,
            VaultName = knowledge.VaultName,
            KnowledgeType = knowledge.Type.ToString(),
            FilePath = knowledge.FilePath,
            Score = 1.0, // Max relevance since user explicitly selected this item
            // FEAT_SelfHostedTemporalAwareness
            CreatedAt = knowledge.CreatedAt,
            UpdatedAt = knowledge.UpdatedAt,
        });

        return results;
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

    /// <summary>
    /// Normalizes search scores to a 0-1 confidence range.
    /// Azure AI Search RRF hybrid scores are typically 0.01-0.05; local cosine similarity scores are 0-1.
    /// </summary>
    internal static double NormalizeConfidence(List<SearchResultItem> searchResults)
    {
        if (searchResults.Count == 0) return 0;
        var maxScore = searchResults.Max(r => r.Score);
        // Scores below 0.1 are likely RRF-based (Azure AI Search hybrid) — scale to 0-1 range
        return maxScore < 0.1 ? Math.Min(1.0, maxScore * 10) : Math.Min(1.0, maxScore);
    }

    internal static string? Truncate(string? value, int maxLength)
        => value != null && value.Length > maxLength ? value[..maxLength] + "..." : value;
}
