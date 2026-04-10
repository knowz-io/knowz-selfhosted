# Knowz.MCP

MCP (Model Context Protocol) server for Knowz. This is a **shared project** used by both the Platform and the Self-Hosted deployment — it has no business logic of its own and always delegates to the underlying API.

## How It Works

The MCP server exposes a standard set of MCP tools (`search_knowledge`, `ask_question`, `create_knowledge`, etc.) and delegates execution to an `IToolBackend`. The backend mode is selected by `MCP:BackendMode` configuration:

### Platform Mode (`"proxy"`, default)

True pass-through. All tool calls are forwarded to a single endpoint on the Platform API:

```
POST {Knowz:BaseUrl}/api/v1/mcp/tools/call
Body: { "name": "search_knowledge", "arguments": { ... } }
```

The Platform API's own MCP handler dispatches and returns results. Uses `ProxyToolBackend` → `McpApiProxyService`.

### Self-Hosted Mode (`"selfhosted"`)

Translation layer. Each MCP tool name is mapped to a specific REST endpoint on the self-hosted API with HTTP method routing, path parameter substitution, query string building, and argument renaming:

```
search_knowledge  → GET  /api/v1/search?q=...&limit=...
create_knowledge  → POST /api/v1/knowledge  { content, title, type, ... }
get_knowledge_item → GET /api/v1/knowledge/{id}
ask_question      → POST /api/v1/ask  { question, vaultId, ... }
```

Uses `SelfHostedToolBackend` with a static `ToolMappings` dictionary. All 22 MCP tools are supported in self-hosted mode.

## Key Files

| File | Purpose |
|------|---------|
| `Config/McpStartupExtensions.cs` | DI registration — selects backend mode |
| `Tools/KnowzProxyTools.cs` | MCP tool definitions (shared across both modes) |
| `Services/IToolBackend.cs` | Backend interface |
| `Services/Proxy/ProxyToolBackend.cs` | Platform mode — single-endpoint proxy |
| `Services/Proxy/SelfHostedToolBackend.cs` | Self-hosted mode — REST endpoint mapping |
| `Services/Proxy/McpApiProxyService.cs` | HTTP client for platform proxy calls |
| `Middleware/McpAuthMiddleware.cs` | Auth — extracts API key into HttpContext |

## Configuration

| Key | Purpose |
|-----|---------|
| `Knowz:BaseUrl` | Base URL of the target API (required) |
| `MCP:BackendMode` | `"proxy"` (Platform) or `"selfhosted"` |
| `MCP:ServiceKey` | Shared secret for MCP→API internal calls (password login + SSO resolve). Must match the API's `MCP:ServiceKey` value. |
| `Redis__ConnectionString` | Session store (falls back to in-memory) |
