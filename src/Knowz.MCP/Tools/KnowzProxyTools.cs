using System.ComponentModel;
using System.Text.Json;
using Knowz.MCP.Middleware;
using Knowz.MCP.Services;
using Knowz.MCP.Services.Session;
using ModelContextProtocol.Server;

namespace Knowz.MCP.Tools;

/// <summary>
/// MCP tools that delegate to IToolBackend.
/// The backend forwards all requests to the Knowz API (proxy mode).
/// </summary>
[McpServerToolType]
public class KnowzProxyTools
{
    private readonly IToolBackend _backend;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<KnowzProxyTools> _logger;

    public KnowzProxyTools(
        IToolBackend backend,
        IHttpContextAccessor httpContextAccessor,
        ILogger<KnowzProxyTools> logger)
    {
        _backend = backend;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private HttpContext GetHttpContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogError("HttpContext is null in tool execution");
            throw new UnauthorizedAccessException("HttpContext not available");
        }
        return httpContext;
    }

    [McpServerTool(Name = "search_knowledge")]
    [Description("Find, search, look up, locate, or retrieve any information, code, documentation, specs, notes, files, transcripts, or content in the knowledge base. Use this for any query about what exists, what was written, what was saved, or to find something by keyword, topic, or concept. Supports filtering by tags and date range.")]
    public async Task<string> SearchKnowledge(
        [Description("Natural language search query")] string query,
        [Description("Maximum number of results (default: 10)")] int limit = 10,
        [Description("Optional vault ID to limit search scope (may be overridden by connection settings)")] string? vaultId = null,
        [Description("When searching within a vault, also include results from child vaults (default: true)")] bool includeChildVaults = true,
        [Description("Optional array of tag names to filter results")] string[]? tags = null,
        [Description("If true, require all tags (AND); if false, match any tag (OR). Default: false")] bool requireAllTags = false,
        [Description("Optional start date for filtering (ISO 8601 format, e.g., 2026-01-08T22:52:47+00:00)")] string? startDate = null,
        [Description("Optional end date for filtering (ISO 8601 format, e.g., 2026-01-08T22:52:47+00:00)")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var effectiveVaultId = httpContext.ResolveVaultId(vaultId);

        _logger.LogInformation("SDK Tool: search_knowledge - query='{Query}', limit={Limit}, vault={VaultId}, includeChildren={IncludeChildren}, tags={Tags}",
            query, limit, effectiveVaultId ?? "all", includeChildVaults, tags != null ? string.Join(",", tags) : "none");

        var args = new Dictionary<string, object> { ["query"] = query, ["limit"] = limit, ["includeChildVaults"] = includeChildVaults };
        if (!string.IsNullOrEmpty(effectiveVaultId))
        {
            args["vaultId"] = effectiveVaultId;
        }
        if (tags != null && tags.Length > 0)
        {
            args["tags"] = tags;
            args["requireAllTags"] = requireAllTags;
        }
        if (!string.IsNullOrEmpty(startDate))
        {
            args["startDate"] = startDate;
        }
        if (!string.IsNullOrEmpty(endDate))
        {
            args["endDate"] = endDate;
        }

        return await _backend.ExecuteToolAsync("search_knowledge", args, cancellationToken);
    }

    [McpServerTool(Name = "get_knowledge_item")]
    [Description("Get, fetch, read, open, view, or retrieve the full details of a specific knowledge item by its ID. Use when you have an ID and need the complete content, metadata, or related items. Also use to drill into a search result.")]
    public async Task<string> GetKnowledgeItem(
        [Description("Knowledge item GUID")] string id,
        [Description("Include related knowledge items (default: false)")] bool includeRelated = false,
        [Description("Maximum number of related items to return (default: 10)")] int relatedLimit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: get_knowledge_item - id={Id}, includeRelated={IncludeRelated}", id, includeRelated);

        var args = new Dictionary<string, object> { ["id"] = id };
        if (includeRelated)
        {
            args["includeRelated"] = true;
            args["relatedLimit"] = relatedLimit;
        }
        return await _backend.ExecuteToolAsync("get_knowledge_item", args, cancellationToken);
    }

    [McpServerTool(Name = "list_topics")]
    [Description("List, show, browse, display, or see all topics, categories, themes, or subject areas in the knowledge base. Use when asking 'what topics exist', 'show me categories', 'what's organized', or exploring the knowledge structure. Timestamps are in ISO 8601 format.")]
    public async Task<string> ListTopics(
        [Description("Maximum number of topics (default: 50)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: list_topics - limit={Limit}", limit);

        var args = new Dictionary<string, object> { ["limit"] = limit };
        return await _backend.ExecuteToolAsync("list_topics", args, cancellationToken);
    }

    [McpServerTool(Name = "get_topic_details")]
    [Description("Get, view, explore, or retrieve detailed information about a specific topic, category, or theme including all related knowledge items. Use when drilling into a topic, exploring a category, or seeing everything related to a subject.")]
    public async Task<string> GetTopicDetails(
        [Description("Topic GUID")] string id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: get_topic_details - id={Id}", id);

        var args = new Dictionary<string, object> { ["id"] = id };
        return await _backend.ExecuteToolAsync("get_topic_details", args, cancellationToken);
    }

    [McpServerTool(Name = "list_vaults")]
    [Description("List, show, browse, display, or see all vaults, workspaces, projects, or collections available. Use when asking 'what vaults exist', 'show my projects', 'which workspaces', or exploring what's accessible. May be filtered if connection is scoped to a specific vault. Timestamps are in ISO 8601 format.")]
    public async Task<string> ListVaults(
        [Description("Include item counts (default: false)")] bool includeStats = false,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var sandboxVaultId = httpContext.GetSandboxVaultId();

        _logger.LogInformation("SDK Tool: list_vaults - includeStats={IncludeStats}, sandboxed={IsSandboxed}",
            includeStats, sandboxVaultId != null);

        var args = new Dictionary<string, object> { ["includeStats"] = includeStats };

        if (!string.IsNullOrEmpty(sandboxVaultId))
        {
            args["vaultId"] = sandboxVaultId;
        }

        return await _backend.ExecuteToolAsync("list_vaults", args, cancellationToken);
    }

    [McpServerTool(Name = "list_vault_contents")]
    [Description("List, show, browse, display, view, or see what's inside a vault, workspace, project, or collection. Use when asking 'what's in this vault', 'show me everything', 'list all items', 'recent additions', or browsing contents. Supports filtering by tags, type (Note, Document, Transcript, Image, Video, Audio), and date range. Vault ID may be overridden by connection settings.")]
    public async Task<string> ListVaultContents(
        [Description("Vault GUID (may be overridden by connection settings)")] string? vaultId = null,
        [Description("Also include items from child/sub-vaults (default: true)")] bool includeChildVaults = true,
        [Description("Maximum number of items (default: 100)")] int limit = 100,
        [Description("Optional array of tag names to filter results")] string[]? tags = null,
        [Description("If true, require all tags (AND); if false, match any tag (OR). Default: false")] bool requireAllTags = false,
        [Description("Optional knowledge type filter (Note, Document, Transcript, Image, Video, Audio)")] string? knowledgeType = null,
        [Description("Optional start date for filtering (ISO 8601 format, e.g., 2026-01-08T22:52:47+00:00)")] string? startDate = null,
        [Description("Optional end date for filtering (ISO 8601 format, e.g., 2026-01-08T22:52:47+00:00)")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var effectiveVaultId = httpContext.ResolveVaultId(vaultId);

        if (string.IsNullOrEmpty(effectiveVaultId))
        {
            return JsonSerializer.Serialize(new { error = "Vault ID is required. Specify vaultId parameter or configure defaultVaultId in connection." });
        }

        _logger.LogInformation("SDK Tool: list_vault_contents - vaultId={VaultId}, includeChildren={IncludeChildren}, limit={Limit}, tags={Tags}",
            effectiveVaultId, includeChildVaults, limit, tags != null ? string.Join(",", tags) : "none");

        var args = new Dictionary<string, object> { ["vaultId"] = effectiveVaultId, ["includeChildVaults"] = includeChildVaults, ["limit"] = limit };
        if (tags != null && tags.Length > 0)
        {
            args["tags"] = tags;
            args["requireAllTags"] = requireAllTags;
        }
        if (!string.IsNullOrEmpty(knowledgeType))
        {
            args["knowledgeType"] = knowledgeType;
        }
        if (!string.IsNullOrEmpty(startDate))
        {
            args["startDate"] = startDate;
        }
        if (!string.IsNullOrEmpty(endDate))
        {
            args["endDate"] = endDate;
        }
        return await _backend.ExecuteToolAsync("list_vault_contents", args, cancellationToken);
    }

    [McpServerTool(Name = "find_entities")]
    [Description("Find, list, discover, or look up people, names, authors, contributors, places, locations, companies, organizations, events, dates, or any named entities mentioned anywhere in the knowledge base. Use when asking 'who', 'where', 'when', or looking for specific named things.")]
    public async Task<string> FindEntities(
        [Description("Type of entity to find (person, location, event)")] string entityType,
        [Description("Optional search query to filter entity names")] string? query = null,
        [Description("Maximum results (default: 50)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: find_entities - entityType={EntityType}, query={Query}", entityType, query);

        var args = new Dictionary<string, object> { ["entityType"] = entityType, ["limit"] = limit };
        if (!string.IsNullOrEmpty(query))
        {
            args["query"] = query;
        }
        return await _backend.ExecuteToolAsync("find_entities", args, cancellationToken);
    }

    [McpServerTool(Name = "ask_question")]
    [Description("Answer any question, explain something, help understand, analyze, summarize, compare, or reason about code, documentation, architecture, decisions, or knowledge. Use for 'what is', 'how does', 'why', 'explain', 'tell me about', 'help me understand', or any inquiry requiring AI-powered reasoning with source references. Enable researchMode for complex questions needing thorough analysis.")]
    public async Task<string> AskQuestion(
        [Description("The question to ask about your knowledge base")] string question,
        [Description("Optional vault name to limit search scope (may be overridden by connection settings)")] string? vaultName = null,
        [Description("Optional vault ID to limit search scope (may be overridden by connection settings)")] string? vaultId = null,
        [Description("When scoped to a vault, also search child vaults (default: true)")] bool includeChildVaults = true,
        [Description("Optional conversation ID for multi-turn dialogue")] string? conversationId = null,
        [Description("Enable creative/adaptive reasoning mode")] bool creativeMode = false,
        [Description("Enable research mode for comprehensive, detailed answers with higher token limits (8000+). Use this for complex questions requiring thorough analysis.")] bool researchMode = false,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var effectiveVaultId = httpContext.ResolveVaultId(vaultId);

        _logger.LogInformation("SDK Tool: ask_question - question length={Length}, vaultName={VaultName}, vaultId={VaultId}, includeChildren={IncludeChildren}, researchMode={ResearchMode}",
            question.Length, vaultName, effectiveVaultId, includeChildVaults, researchMode);

        var args = new Dictionary<string, object>
        {
            ["question"] = question,
            ["creativeMode"] = creativeMode,
            ["researchMode"] = researchMode,
            ["includeChildVaults"] = includeChildVaults
        };

        if (!string.IsNullOrEmpty(effectiveVaultId))
        {
            args["vaultId"] = effectiveVaultId;
        }
        else if (!string.IsNullOrEmpty(vaultName))
        {
            args["vaultName"] = vaultName;
        }

        if (!string.IsNullOrEmpty(conversationId))
        {
            args["conversationId"] = conversationId;
        }
        return await _backend.ExecuteToolAsync("ask_question", args, cancellationToken);
    }

    [McpServerTool(Name = "create_knowledge")]
    [Description("Save, store, create, add, remember, document, record, write, persist, or capture any information, notes, decisions, learnings, specs, documentation, or content for later retrieval. Use when the user wants to save something, document a decision, create a note, or store information. AI processing (summarization, entity extraction) runs automatically.")]
    public async Task<string> CreateKnowledge(
        [Description("Content of the knowledge item (optional if file attachments will be added separately)")] string? content = null,
        [Description("Title for the knowledge item (optional)")] string? title = null,
        [Description("Knowledge type: Note, Document, Transcript, Image, Video, Audio (default: Note)")] string knowledgeType = "Note",
        [Description("Optional vault ID to store the item (defaults to tenant's default vault)")] string? vaultId = null,
        [Description("Optional topic ID to associate with")] string? topicId = null,
        [Description("Optional array of tag names to apply")] string[]? tags = null,
        [Description("Optional source/reference for the content")] string? source = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var effectiveVaultId = httpContext.ResolveVaultId(vaultId);

        _logger.LogInformation("SDK Tool: create_knowledge - title={Title}, type={Type}, vault={VaultId}",
            title ?? "(untitled)", knowledgeType, effectiveVaultId ?? "default");

        var args = new Dictionary<string, object>
        {
            ["knowledgeType"] = knowledgeType
        };

        if (!string.IsNullOrEmpty(content))
        {
            args["content"] = content;
        }
        if (!string.IsNullOrEmpty(title))
        {
            args["title"] = title;
        }
        if (!string.IsNullOrEmpty(effectiveVaultId))
        {
            args["vaultId"] = effectiveVaultId;
        }
        if (!string.IsNullOrEmpty(topicId))
        {
            args["topicId"] = topicId;
        }
        if (tags != null && tags.Length > 0)
        {
            args["tags"] = tags;
        }
        if (!string.IsNullOrEmpty(source))
        {
            args["source"] = source;
        }

        return await _backend.ExecuteToolAsync("create_knowledge", args, cancellationToken);
    }

    [McpServerTool(Name = "create_vault")]
    [Description("Create a new vault, workspace, project, or collection to organize knowledge. Supports creating child vaults under an existing parent vault for hierarchical organization.")]
    public async Task<string> CreateVault(
        [Description("Name for the new vault (required)")] string name,
        [Description("Optional description of the vault's purpose")] string? description = null,
        [Description("Optional parent vault ID to create a child vault")] string? parentVaultId = null,
        [Description("Optional vault type: GeneralKnowledge, Business, Product, CodeBase, DailyDiary, QuestionAnswer, PersonBound, LocationBound")] string? vaultType = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var sandboxVaultId = httpContext.GetSandboxVaultId();

        if (!string.IsNullOrEmpty(sandboxVaultId) && string.IsNullOrEmpty(parentVaultId))
        {
            parentVaultId = sandboxVaultId;
        }

        _logger.LogInformation("SDK Tool: create_vault - name={Name}, parentVaultId={ParentVaultId}, vaultType={VaultType}",
            name, parentVaultId ?? "none", vaultType ?? "default");

        var args = new Dictionary<string, object> { ["name"] = name };
        if (!string.IsNullOrEmpty(description))
        {
            args["description"] = description;
        }
        if (!string.IsNullOrEmpty(parentVaultId))
        {
            args["parentVaultId"] = parentVaultId;
        }
        if (!string.IsNullOrEmpty(vaultType))
        {
            args["vaultType"] = vaultType;
        }

        return await _backend.ExecuteToolAsync("create_vault", args, cancellationToken);
    }

    [McpServerTool(Name = "update_knowledge")]
    [Description("Replace content, title, tags, or metadata of an existing knowledge item. Use when overwriting a document with entirely new content, changing tags, or moving items between vaults. All fields except ID are optional for partial updates. AI re-processing is triggered if content changes.")]
    public async Task<string> UpdateKnowledge(
        [Description("Knowledge item ID (required)")] string id,
        [Description("Updated content (optional)")] string? content = null,
        [Description("Updated title (optional)")] string? title = null,
        [Description("Move to different vault (optional)")] string? vaultId = null,
        [Description("Change associated topic (optional)")] string? topicId = null,
        [Description("Replace tags (optional)")] string[]? tags = null,
        [Description("Update source/reference (optional)")] string? source = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var effectiveVaultId = !string.IsNullOrEmpty(vaultId) ? httpContext.ResolveVaultId(vaultId) : null;

        _logger.LogInformation("SDK Tool: update_knowledge - id={Id}, hasContent={HasContent}, hasTitle={HasTitle}",
            id, content != null, title != null);

        var args = new Dictionary<string, object> { ["id"] = id };

        if (!string.IsNullOrEmpty(content))
        {
            args["content"] = content;
        }
        if (!string.IsNullOrEmpty(title))
        {
            args["title"] = title;
        }
        if (!string.IsNullOrEmpty(effectiveVaultId))
        {
            args["vaultId"] = effectiveVaultId;
        }
        if (!string.IsNullOrEmpty(topicId))
        {
            args["topicId"] = topicId;
        }
        if (tags != null)
        {
            args["tags"] = tags;
        }
        if (!string.IsNullOrEmpty(source))
        {
            args["source"] = source;
        }

        return await _backend.ExecuteToolAsync("update_knowledge", args, cancellationToken);
    }

    [McpServerTool(Name = "amend_knowledge")]
    [Description("Apply a natural language edit instruction to an existing knowledge item. Use when you want to add a section, update part of the content, remove a paragraph, fix information, or make any incremental change — without replacing the entire content. The AI reads the existing content and applies your instruction.")]
    public async Task<string> AmendKnowledge(
        [Description("Knowledge item ID (required)")] string id,
        [Description("Natural language instruction describing what to change, add, or remove from the existing content")] string instruction,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: amend_knowledge - id={Id}, instructionLength={Length}",
            id, instruction.Length);

        var args = new Dictionary<string, object>
        {
            ["id"] = id,
            ["instruction"] = instruction
        };

        return await _backend.ExecuteToolAsync("amend_knowledge", args, cancellationToken);
    }

    [McpServerTool(Name = "bulk_get_knowledge_items")]
    [Description("Get, fetch, or retrieve multiple knowledge items at once by their IDs. Use when you need to load several items efficiently, compare multiple documents, or gather context from multiple sources. Maximum 100 IDs per request.")]
    public async Task<string> BulkGetKnowledgeItems(
        [Description("Array of knowledge item GUIDs to fetch (required, max 100)")] string[] ids,
        [Description("Include related knowledge items for each (default: false)")] bool includeRelated = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: bulk_get_knowledge_items - count={Count}, includeRelated={IncludeRelated}",
            ids.Length, includeRelated);

        var args = new Dictionary<string, object>
        {
            ["ids"] = ids,
            ["includeRelated"] = includeRelated
        };

        return await _backend.ExecuteToolAsync("bulk_get_knowledge_items", args, cancellationToken);
    }

    [McpServerTool(Name = "create_inbox_item")]
    [Description("Save something to the inbox for later review. Inbox items are staging content that hasn't been committed to a knowledge vault yet -- useful for recommendations, things to follow up on, uncertain items, or quick captures that need processing later. Supports optional file attachments via pre-uploaded FileRecord IDs.")]
    public async Task<string> CreateInboxItem(
        [Description("Text content for the inbox item (required)")] string body,
        [Description("Optional array of FileRecord GUIDs for pre-uploaded files to attach")] string[]? fileRecordIds = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: create_inbox_item - bodyLength={Length}, fileCount={FileCount}",
            body.Length, fileRecordIds?.Length ?? 0);

        var args = new Dictionary<string, object>
        {
            ["body"] = body
        };
        if (fileRecordIds != null && fileRecordIds.Length > 0)
        {
            args["fileRecordIds"] = fileRecordIds;
        }

        return await _backend.ExecuteToolAsync("create_inbox_item", args, cancellationToken);
    }

    [McpServerTool(Name = "attach_files")]
    [Description("Attach one or more pre-uploaded files to an existing knowledge item or inbox item. Files must be uploaded first via the file upload API. Triggers AI processing (OCR, transcription, entity extraction) for knowledge item attachments.")]
    public async Task<string> AttachFiles(
        [Description("ID of the knowledge item or inbox item to attach files to (required)")] string targetId,
        [Description("Type of target: 'knowledge' or 'inbox' (required)")] string targetType,
        [Description("Array of FileRecord GUIDs to attach (required, 1-100 files)")] string[] fileRecordIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: attach_files - targetType={TargetType}, targetId={TargetId}, fileCount={FileCount}",
            targetType, targetId, fileRecordIds.Length);

        var args = new Dictionary<string, object>
        {
            ["targetId"] = targetId,
            ["targetType"] = targetType,
            ["fileRecordIds"] = fileRecordIds
        };

        return await _backend.ExecuteToolAsync("attach_files", args, cancellationToken);
    }

    // Additional non-tool-attributed tools that were added after initial implementation

    [McpServerTool(Name = "count_knowledge")]
    [Description("Count how many knowledge items match specific criteria. Use for analytics, progress tracking, or understanding the scope of content. Supports filtering by type, title pattern, file pattern, and date range.")]
    public async Task<string> CountKnowledge(
        [Description("Optional knowledge type filter")] string? knowledgeType = null,
        [Description("Optional title pattern (supports * and ? wildcards)")] string? titlePattern = null,
        [Description("Optional file name pattern (supports * and ? wildcards)")] string? fileNamePattern = null,
        [Description("Optional start date (ISO 8601)")] string? startDate = null,
        [Description("Optional end date (ISO 8601)")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: count_knowledge");

        var args = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(knowledgeType)) args["knowledgeType"] = knowledgeType;
        if (!string.IsNullOrEmpty(titlePattern)) args["titlePattern"] = titlePattern;
        if (!string.IsNullOrEmpty(fileNamePattern)) args["fileNamePattern"] = fileNamePattern;
        if (!string.IsNullOrEmpty(startDate)) args["startDate"] = startDate;
        if (!string.IsNullOrEmpty(endDate)) args["endDate"] = endDate;

        return await _backend.ExecuteToolAsync("count_knowledge", args, cancellationToken);
    }

    [McpServerTool(Name = "get_statistics")]
    [Description("Get aggregate statistics about the knowledge base including total items, breakdown by type, breakdown by vault, and date range of content.")]
    public async Task<string> GetStatistics(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: get_statistics");
        return await _backend.ExecuteToolAsync("get_statistics", new Dictionary<string, object>(), cancellationToken);
    }

    [McpServerTool(Name = "search_by_file_pattern")]
    [Description("Search for knowledge items by file path pattern. Supports wildcards: * matches any characters, ? matches a single character. Example: '*.cs' finds all C# files, 'src/**/*.ts' finds TypeScript files under src.")]
    public async Task<string> SearchByFilePattern(
        [Description("File path pattern with wildcards (* and ?)")] string pattern,
        [Description("If true, return only the count (default: false)")] bool countOnly = false,
        [Description("Maximum results (default: 50, max: 100)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: search_by_file_pattern - pattern={Pattern}", pattern);

        var args = new Dictionary<string, object>
        {
            ["pattern"] = pattern,
            ["countOnly"] = countOnly,
            ["limit"] = limit
        };

        return await _backend.ExecuteToolAsync("search_by_file_pattern", args, cancellationToken);
    }

    [McpServerTool(Name = "search_by_title_pattern")]
    [Description("Search for knowledge items by title pattern. Supports wildcards: * matches any characters, ? matches a single character.")]
    public async Task<string> SearchByTitlePattern(
        [Description("Title pattern with wildcards (* and ?)")] string pattern,
        [Description("If true, return only the count (default: false)")] bool countOnly = false,
        [Description("Maximum results (default: 50, max: 100)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: search_by_title_pattern - pattern={Pattern}", pattern);

        var args = new Dictionary<string, object>
        {
            ["pattern"] = pattern,
            ["countOnly"] = countOnly,
            ["limit"] = limit
        };

        return await _backend.ExecuteToolAsync("search_by_title_pattern", args, cancellationToken);
    }

    [McpServerTool(Name = "add_comment")]
    [Description("Add a comment or contribution to a knowledge item. Comments are included in the knowledge item's AI summary and search index. Use this to add notes, corrections, additional context, or answers to a knowledge item.")]
    public async Task<string> AddComment(
        [Description("Knowledge item GUID to comment on (required)")] string knowledgeItemId,
        [Description("Comment text/body (required)")] string body,
        [Description("Author name (defaults to 'AI Assistant')")] string? authorName = null,
        [Description("Optional parent comment ID for threaded replies")] string? parentCommentId = null,
        [Description("Optional sentiment label (e.g., 'positive', 'negative', 'neutral')")] string? sentiment = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: add_comment - knowledgeItemId={Id}, bodyLength={Length}",
            knowledgeItemId, body.Length);

        var args = new Dictionary<string, object>
        {
            ["knowledgeItemId"] = knowledgeItemId,
            ["body"] = body,
            ["authorName"] = authorName ?? "AI Assistant"
        };

        if (!string.IsNullOrEmpty(parentCommentId))
            args["parentCommentId"] = parentCommentId;
        if (!string.IsNullOrEmpty(sentiment))
            args["sentiment"] = sentiment;

        return await _backend.ExecuteToolAsync("add_comment", args, cancellationToken);
    }

    [McpServerTool(Name = "list_comments")]
    [Description("List all comments and contributions on a knowledge item. Returns threaded comments with replies, author names, and attachment counts. Use this to see what others have contributed or to check for existing answers before adding a new comment.")]
    public async Task<string> ListComments(
        [Description("Knowledge item GUID to list comments for (required)")] string knowledgeItemId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: list_comments - knowledgeItemId={Id}", knowledgeItemId);

        var args = new Dictionary<string, object>
        {
            ["knowledgeItemId"] = knowledgeItemId
        };

        return await _backend.ExecuteToolAsync("list_comments", args, cancellationToken);
    }

    [McpServerTool(Name = "list_knowledge_items")]
    [Description("List knowledge items with pagination and sorting. Supports filtering by type, title/file patterns, and date range. Use for browsing or paginating through large result sets.")]
    public async Task<string> ListKnowledgeItems(
        [Description("Page number (1-based, default: 1)")] int page = 1,
        [Description("Items per page (1-100, default: 20)")] int pageSize = 20,
        [Description("Sort by: created, updated, title (default: created)")] string sortBy = "created",
        [Description("Sort direction: asc, desc (default: desc)")] string sortDirection = "desc",
        [Description("Optional knowledge type filter")] string? knowledgeType = null,
        [Description("Optional title pattern")] string? titlePattern = null,
        [Description("Optional file name pattern")] string? fileNamePattern = null,
        [Description("Optional start date (ISO 8601)")] string? startDate = null,
        [Description("Optional end date (ISO 8601)")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SDK Tool: list_knowledge_items - page={Page}, pageSize={PageSize}", page, pageSize);

        var args = new Dictionary<string, object>
        {
            ["page"] = page,
            ["pageSize"] = pageSize,
            ["sortBy"] = sortBy,
            ["sortDirection"] = sortDirection
        };
        if (!string.IsNullOrEmpty(knowledgeType)) args["knowledgeType"] = knowledgeType;
        if (!string.IsNullOrEmpty(titlePattern)) args["titlePattern"] = titlePattern;
        if (!string.IsNullOrEmpty(fileNamePattern)) args["fileNamePattern"] = fileNamePattern;
        if (!string.IsNullOrEmpty(startDate)) args["startDate"] = startDate;
        if (!string.IsNullOrEmpty(endDate)) args["endDate"] = endDate;

        return await _backend.ExecuteToolAsync("list_knowledge_items", args, cancellationToken);
    }
}
