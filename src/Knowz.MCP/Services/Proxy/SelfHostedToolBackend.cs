using System.Text;
using System.Text.Json;
using System.Web;

namespace Knowz.MCP.Services.Proxy;

/// <summary>
/// Self-hosted mode tool backend: maps MCP tool names to self-hosted REST API endpoints.
/// Unlike ProxyToolBackend (which calls a single /api/v1/mcp/tools/call endpoint),
/// this backend calls individual REST endpoints on the self-hosted API.
/// </summary>
public class SelfHostedToolBackend : IToolBackend
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SelfHostedToolBackend> _logger;

    private static readonly Dictionary<string, ToolMapping> ToolMappings = new()
    {
        ["search_knowledge"] = new(HttpMethod.Get, "/api/v1/search", null,
            new[] { "query", "limit", "vaultId", "includeChildVaults", "tags", "requireAllTags", "startDate", "endDate" },
            new Dictionary<string, string> { ["query"] = "q", ["includeChildVaults"] = "includeChildren" }),

        ["get_knowledge_item"] = new(HttpMethod.Get, "/api/v1/knowledge/{id}",
            new[] { "id" }, null, null),

        ["list_topics"] = new(HttpMethod.Get, "/api/v1/topics", null,
            new[] { "limit" }, null),

        ["get_topic_details"] = new(HttpMethod.Get, "/api/v1/topics/{id}",
            new[] { "id" }, null, null),

        ["list_vaults"] = new(HttpMethod.Get, "/api/v1/vaults", null,
            new[] { "includeStats" }, null),

        ["list_vault_contents"] = new(HttpMethod.Get, "/api/v1/vaults/{vaultId}/contents",
            new[] { "vaultId" },
            new[] { "includeChildVaults", "limit" },
            new Dictionary<string, string> { ["includeChildVaults"] = "includeChildren" }),

        ["find_entities"] = new(HttpMethod.Get, "/api/v1/entities", null,
            new[] { "entityType", "query", "limit" },
            new Dictionary<string, string> { ["entityType"] = "type", ["query"] = "q" }),

        ["ask_question"] = new(HttpMethod.Post, "/api/v1/ask", null, null,
            null, new[] { "question", "vaultId", "researchMode" }),

        ["create_knowledge"] = new(HttpMethod.Post, "/api/v1/knowledge", null, null,
            new Dictionary<string, string> { ["knowledgeType"] = "type" },
            new[] { "content", "title", "knowledgeType", "vaultId", "tags", "source" }),

        ["update_knowledge"] = new(HttpMethod.Put, "/api/v1/knowledge/{id}",
            new[] { "id" }, null, null,
            new[] { "content", "title", "tags", "source" }),

        ["create_vault"] = new(HttpMethod.Post, "/api/v1/vaults", null, null, null,
            new[] { "name", "description", "parentVaultId", "vaultType" }),

        ["create_inbox_item"] = new(HttpMethod.Post, "/api/v1/inbox", null, null, null,
            new[] { "body" }),

        ["count_knowledge"] = new(HttpMethod.Get, "/api/v1/knowledge/stats", null, null, null),

        ["get_statistics"] = new(HttpMethod.Get, "/api/v1/knowledge/stats", null, null, null),

        ["list_knowledge_items"] = new(HttpMethod.Get, "/api/v1/knowledge", null,
            new[] { "page", "pageSize", "sortBy", "sortDirection", "knowledgeType", "titlePattern", "fileNamePattern", "startDate", "endDate" },
            new Dictionary<string, string>
            {
                ["sortBy"] = "sort", ["sortDirection"] = "sortDir",
                ["knowledgeType"] = "type", ["titlePattern"] = "title", ["fileNamePattern"] = "fileName"
            }),

        ["search_by_file_pattern"] = new(HttpMethod.Get, "/api/v1/knowledge", null,
            new[] { "pattern", "limit" },
            new Dictionary<string, string> { ["pattern"] = "fileName", ["limit"] = "pageSize" }),

        ["search_by_title_pattern"] = new(HttpMethod.Get, "/api/v1/knowledge", null,
            new[] { "pattern", "limit" },
            new Dictionary<string, string> { ["pattern"] = "title", ["limit"] = "pageSize" }),

        ["add_comment"] = new(HttpMethod.Post, "/api/v1/knowledge/{knowledgeItemId}/comments",
            new[] { "knowledgeItemId" }, null, null,
            new[] { "body", "authorName", "parentCommentId", "sentiment" }),

        ["list_comments"] = new(HttpMethod.Get, "/api/v1/knowledge/{knowledgeItemId}/comments",
            new[] { "knowledgeItemId" }, null, null),

        // DEPRECATED — legacy synchronous path. Now a deprecation shim on the API side
        // that enqueues an async request. Removal no earlier than 2026-08-01.
        // Migrate to amend_knowledge_async. Spec: MCP_AmendKnowledge §1 Rule 2.
        ["amend_knowledge"] = new(HttpMethod.Post, "/api/v1/knowledge/{id}/amend",
            new[] { "id" }, null, null,
            new[] { "instruction" }),

        // Async enqueue — returns { status, amendRequestId, knowledgeId, pollUrl, message }.
        // Idempotency key (amendRequestId) dedupes retries per (tenant, key).
        // Spec: MCP_AmendKnowledge §1 Rule 1, §2.
        ["amend_knowledge_async"] = new(HttpMethod.Post, "/api/v1/knowledge/{id}/amend-requests",
            new[] { "id" }, null, null,
            new[] { "instruction", "amendRequestId" }),

        // Request-scoped polling — clients poll a specific amendRequestId rather than
        // racing against latestAmendRequest on get_knowledge_item.
        // Spec: MCP_AmendKnowledge §1 Rule 10, §2.
        ["get_amend_request_status"] = new(HttpMethod.Get, "/api/v1/knowledge/{knowledgeId}/amend-requests/{amendRequestId}",
            new[] { "knowledgeId", "amendRequestId" }, null, null),

        ["get_version_history"] = new(HttpMethod.Get, "/api/v1/knowledge/{knowledgeId}/versions",
            new[] { "knowledgeId" }, null, null),
    };

    public SelfHostedToolBackend(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SelfHostedToolBackend> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        // Handle special-case tools that require custom logic
        if (toolName == "bulk_get_knowledge_items")
            return await ExecuteBulkGetAsync(arguments, cancellationToken);

        if (toolName == "attach_files")
            return await ExecuteAttachFilesAsync(arguments, cancellationToken);

        if (!ToolMappings.TryGetValue(toolName, out var mapping))
            return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });

        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available");
        var apiKey = context.Items["ApiKey"] as string
            ?? throw new InvalidOperationException("No API key available for self-hosted mode");

        var baseUrl = _configuration["Knowz:BaseUrl"]
            ?? throw new InvalidOperationException("Knowz:BaseUrl is not configured");

        var client = _httpClientFactory.CreateClient("McpApiClient");

        // Build the URL with path parameters substituted
        var path = mapping.PathTemplate;
        if (mapping.PathParams != null)
        {
            foreach (var param in mapping.PathParams)
            {
                if (arguments.TryGetValue(param, out var value))
                    path = path.Replace($"{{{param}}}", Uri.EscapeDataString(value?.ToString() ?? ""));
            }
        }

        var url = $"{baseUrl.TrimEnd('/')}{path}";

        // Build query string for GET requests
        if (mapping.Method == HttpMethod.Get && mapping.QueryParams != null)
        {
            var queryParts = new List<string>();
            foreach (var param in mapping.QueryParams)
            {
                if (arguments.TryGetValue(param, out var value) && value != null)
                {
                    var queryName = mapping.ArgRenames?.GetValueOrDefault(param) ?? param;
                    var stringValue = value is JsonElement je ? je.ToString() : value.ToString();
                    if (!string.IsNullOrEmpty(stringValue))
                        queryParts.Add($"{HttpUtility.UrlEncode(queryName)}={HttpUtility.UrlEncode(stringValue)}");
                }
            }
            if (queryParts.Count > 0)
                url += "?" + string.Join("&", queryParts);
        }

        var request = new HttpRequestMessage(mapping.Method, url);
        request.Headers.Add("X-Api-Key", apiKey);

        // Build JSON body for POST/PUT requests
        if (mapping.Method != HttpMethod.Get && mapping.BodyParams != null)
        {
            var body = new Dictionary<string, object>();
            foreach (var param in mapping.BodyParams)
            {
                if (arguments.TryGetValue(param, out var value) && value != null)
                {
                    var bodyName = mapping.ArgRenames?.GetValueOrDefault(param) ?? param;
                    body[bodyName] = value;
                }
            }
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        _logger.LogInformation("SelfHostedToolBackend: {Method} {Url} for tool {ToolName}",
            mapping.Method, url, toolName);

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SelfHostedToolBackend: {ToolName} returned {StatusCode}: {Body}",
                    toolName, response.StatusCode, responseBody);
                return JsonSerializer.Serialize(new { error = $"API returned {(int)response.StatusCode}", details = responseBody });
            }

            return string.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "SelfHostedToolBackend: {ToolName} timed out", toolName);
            return JsonSerializer.Serialize(new { error = $"Request for {toolName} timed out" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "SelfHostedToolBackend: {ToolName} failed", toolName);
            return JsonSerializer.Serialize(new { error = $"Request for {toolName} failed: {ex.Message}" });
        }
    }

    private async Task<string> ExecuteBulkGetAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available");
        var apiKey = context.Items["ApiKey"] as string
            ?? throw new InvalidOperationException("No API key available for self-hosted mode");
        var baseUrl = (_configuration["Knowz:BaseUrl"]
            ?? throw new InvalidOperationException("Knowz:BaseUrl is not configured")).TrimEnd('/');

        if (!arguments.TryGetValue("ids", out var idsObj) || idsObj == null)
            return JsonSerializer.Serialize(new { error = "ids parameter is required" });

        // Parse the ids - could be a JsonElement array or a List
        var ids = new List<string>();
        if (idsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
                ids.Add(item.GetString() ?? item.ToString());
        }
        else if (idsObj is IEnumerable<object> enumerable)
        {
            ids.AddRange(enumerable.Select(o => o.ToString()!));
        }
        else
        {
            ids.Add(idsObj.ToString()!);
        }

        // Limit to 100 items
        if (ids.Count > 100)
            ids = ids.Take(100).ToList();

        var client = _httpClientFactory.CreateClient("McpApiClient");
        var results = new List<object>();

        // Process in batches of 10 (parallel within each batch)
        foreach (var batch in ids.Chunk(10))
        {
            var tasks = batch.Select(async id =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{baseUrl}/api/v1/knowledge/{Uri.EscapeDataString(id)}");
                request.Headers.Add("X-Api-Key", apiKey);

                try
                {
                    var response = await client.SendAsync(request, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
                        return JsonSerializer.Deserialize<object>(body);
                    return (object)new { id, error = $"HTTP {(int)response.StatusCode}" };
                }
                catch (Exception ex)
                {
                    return (object)new { id, error = ex.Message };
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults.Where(r => r != null)!);
        }

        return JsonSerializer.Serialize(results);
    }

    private async Task<string> ExecuteAttachFilesAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available");
        var apiKey = context.Items["ApiKey"] as string
            ?? throw new InvalidOperationException("No API key available for self-hosted mode");
        var baseUrl = (_configuration["Knowz:BaseUrl"]
            ?? throw new InvalidOperationException("Knowz:BaseUrl is not configured")).TrimEnd('/');

        if (!arguments.TryGetValue("targetId", out var targetIdObj) || targetIdObj == null)
            return JsonSerializer.Serialize(new { error = "targetId parameter is required" });

        if (!arguments.TryGetValue("targetType", out var targetTypeObj) || targetTypeObj == null)
            return JsonSerializer.Serialize(new { error = "targetType parameter is required" });

        var targetId = targetIdObj is JsonElement teId ? teId.GetString() : targetIdObj.ToString();
        var targetType = targetTypeObj is JsonElement teTy ? teTy.GetString() : targetTypeObj.ToString();

        if (targetType != "knowledge")
            return JsonSerializer.Serialize(new { error = $"targetType '{targetType}' is not supported. Only 'knowledge' is supported." });

        if (!arguments.TryGetValue("fileRecordIds", out var idsObj) || idsObj == null)
            return JsonSerializer.Serialize(new { error = "fileRecordIds parameter is required" });

        var fileRecordIds = new List<string>();
        if (idsObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
                fileRecordIds.Add(item.GetString() ?? item.ToString());
        }
        else if (idsObj is IEnumerable<object> enumerable)
        {
            fileRecordIds.AddRange(enumerable.Select(o => o.ToString()!));
        }
        else
        {
            fileRecordIds.Add(idsObj.ToString()!);
        }

        if (fileRecordIds.Count > 100)
            fileRecordIds = fileRecordIds.Take(100).ToList();

        var client = _httpClientFactory.CreateClient("McpApiClient");
        var results = new List<object>();
        var errors = new List<object>();
        var attachedCount = 0;

        foreach (var fileRecordId in fileRecordIds)
        {
            var url = $"{baseUrl}/api/v1/knowledge/{Uri.EscapeDataString(targetId!)}/attachments";
            var body = JsonSerializer.Serialize(new { fileRecordId });

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-Api-Key", apiKey);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    attachedCount++;
                    results.Add(new { fileRecordId, status = "attached" });
                }
                else
                {
                    errors.Add(new { fileRecordId, error = $"HTTP {(int)response.StatusCode}", details = responseBody });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new { fileRecordId, error = ex.Message });
            }
        }

        return JsonSerializer.Serialize(new
        {
            status = errors.Count == 0 ? "success" : "partial",
            targetType,
            targetId,
            attachedCount,
            results,
            errors
        });
    }
}

internal record ToolMapping(
    HttpMethod Method,
    string PathTemplate,
    string[]? PathParams,
    string[]? QueryParams,
    Dictionary<string, string>? ArgRenames,
    string[]? BodyParams = null);
