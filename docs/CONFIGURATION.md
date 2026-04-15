# Configuration Reference

All configuration is done through environment variables in your `.env` file (Docker Compose), user-secrets (Aspire), or `appsettings.Local.json`. This document covers every configurable option organized by category.

## Required

These variables must be set for the application to function. The defaults work for local development but should be changed for production.

| Variable | Default | Description |
|----------|---------|-------------|
| `SA_PASSWORD` | `Knowz_Dev_P@ssw0rd!` | SQL Server SA password. Must be at least 8 characters and include uppercase, lowercase, and a number or symbol. |
| `JWT_SECRET` | `change-this-to-a-random-64-char-string-in-production-please!!` | Secret key used to sign JWT authentication tokens. Use a random string of at least 32 characters. |
| `ADMIN_USERNAME` | `admin` | SuperAdmin username created on first startup. |
| `ADMIN_PASSWORD` | `changeme` | SuperAdmin password created on first startup. Change immediately after first login. |

## AI Services (Three-Tier Fallback)

The selfhosted API uses a three-tier fallback for AI services. Configure **one** of the following:

| Tier | Trigger | AI Operations | Search | Best for |
|------|---------|---------------|--------|----------|
| 1 | `KnowzPlatform:Enabled=true` + BaseUrl + ApiKey | Proxied to Knowz Platform API | Local keyword (SQL LIKE) | Quick setup, no Azure resources |
| 2 | `AzureOpenAI` + `AzureAISearch` configured | Direct Azure OpenAI calls | Vector + keyword hybrid | Full-featured, best search quality |
| 3 | Neither configured (default) | Disabled (NoOp) | Disabled | Auth/admin/CRUD only |

### Tier 1: Knowz Platform Proxy

Delegates AI operations (completions, embeddings, summarization, entity extraction, enrichment) to the Knowz Platform API. No Azure subscription required -- just an API key from your Knowz account.

| Variable | Config Key | Description | Example |
|----------|-----------|-------------|---------|
| -- | `KnowzPlatform__Enabled` | Enable platform proxy | `true` |
| -- | `KnowzPlatform__BaseUrl` | Platform API base URL | `https://api.knowz.io` |
| -- | `KnowzPlatform__ApiKey` | Platform API key | `ukz_...` |

**What works:** All AI operations (chat, summarization, embeddings, entity extraction, enrichment) are fully functional via the platform API. Search uses local keyword matching (SQL LIKE).

**Aspire user-secrets:**
```bash
dotnet user-secrets set "KnowzPlatform:Enabled" "true" --project src/Knowz.SelfHosted.AppHost
dotnet user-secrets set "KnowzPlatform:BaseUrl" "https://api.knowz.io" --project src/Knowz.SelfHosted.AppHost
dotnet user-secrets set "KnowzPlatform:ApiKey" "ukz_your_key" --project src/Knowz.SelfHosted.AppHost
```

### Tier 2: Azure OpenAI (Optional)

Enable AI-powered chat, summarization, and embedding generation with your own Azure OpenAI resource.

| Variable | Config Key | Description | Example |
|----------|-----------|-------------|---------|
| `AZURE_OPENAI_ENDPOINT` | `AzureOpenAI__Endpoint` | Azure OpenAI resource endpoint URL | `https://your-openai.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | `AzureOpenAI__ApiKey` | Azure OpenAI API key | `your-api-key` |
| -- | `AzureOpenAI__DeploymentName` | Chat/completion model deployment name | `gpt-5.2-chat` |
| -- | `AzureOpenAI__EmbeddingDeploymentName` | Embedding model deployment name | `text-embedding-3-small` |

### Tier 2: Azure AI Search (Optional)

Enable semantic vector search across your knowledge base. Requires Azure OpenAI to also be configured (for generating embeddings).

| Variable | Config Key | Description | Example |
|----------|-----------|-------------|---------|
| `AZURE_AI_SEARCH_ENDPOINT` | `AzureAISearch__Endpoint` | Azure AI Search service endpoint | `https://your-search.search.windows.net` |
| `AZURE_AI_SEARCH_API_KEY` | `AzureAISearch__ApiKey` | Azure AI Search admin API key | `your-api-key` |
| -- | `AzureAISearch__IndexName` | Search index name | `knowledge` |

## Storage

Configure where uploaded files are stored. The default local filesystem provider requires no external services.

| Variable | Default | Description |
|----------|---------|-------------|
| `STORAGE_PROVIDER` | `LocalFileSystem` | Storage backend: `LocalFileSystem` or `AzureBlobStorage` |
| `AZURE_STORAGE_CONNECTION_STRING` | -- | Azure Blob Storage connection string (when using Azure provider) |
| `AZURE_STORAGE_CONTAINER` | `selfhosted-files` | Blob container name (when using Azure provider) |

These map to the following compose-level configuration keys:

| Compose Environment Key | Default | Description |
|--------------------------|---------|-------------|
| `Storage__Provider` | `LocalFileSystem` | Storage backend |
| `Storage__Local__RootPath` | `/data/files` | Local filesystem path for file storage (inside the container) |
| `Storage__Azure__ConnectionString` | -- | Azure Blob Storage connection string |
| `Storage__Azure__ContainerName` | `selfhosted-files` | Blob container name |

The default `LocalFileSystem` provider stores files in a Docker volume (`knowz-file-storage`), which persists across container restarts.

## SSO (Single Sign-On)

SSO with Microsoft Entra ID (Azure AD) is configured through the Admin UI after login, not through environment variables. This section is for reference.

The application supports two Entra ID modes:

- **PKCE (public client)** -- Browser-based flow, no client secret required
- **Confidential client** -- Server-side flow with client secret

Settings configured in the Admin UI:

| Setting | Description |
|---------|-------------|
| Client ID | Application (client) ID from Azure portal |
| Client Secret | Client secret value (confidential mode only) |
| Directory (Tenant) ID | Azure AD tenant ID |
| Auto-provision users | Automatically create user accounts on first SSO login |

## MCP (Model Context Protocol)

The MCP server acts as a proxy, allowing AI tools (Claude, Cursor, etc.) to query your knowledge base through the standardized MCP protocol.

| Variable | Default | Description |
|----------|---------|-------------|
| `MCP_PORT` | `3001` | Host port for the MCP server |
| `MCP_API_URL` | `http://api:8080` | Internal API URL that MCP proxies to. Change only if using a custom network setup. |
| `MCP_SERVICE_KEY` | `knowz-mcp-dev-service-key` | Shared secret between MCP server and API for internal service-to-service calls (email/password login, SSO resolve). Must be the same value on both the MCP server and the API. Change to a random string in production. |
| `MCP_VALIDATE_API_KEY` | `true` | Whether MCP validates API keys on incoming requests |

These map to the following compose-level configuration keys:

| Compose Environment Key | Default | Description |
|--------------------------|---------|-------------|
| `Knowz__BaseUrl` | `http://api:8080` | Internal API URL for proxying |
| `Authentication__ValidateApiKey` | `true` | API key validation toggle |
| `MCP__BackendMode` | `selfhosted` | Backend mode (set automatically in compose) |
| `MCP__ServiceKey` | `knowz-mcp-dev-service-key` | Shared secret for MCP→API internal calls |
| `MCP__ApiKeyValidationEndpoint` | `/api/vaults` | Endpoint used to validate API keys (set automatically in compose) |

## Advanced

Simplified environment variables for `.env`:

| Variable | Default | Description |
|----------|---------|-------------|
| `RATE_LIMITING_ENABLED` | `true` | Enable API rate limiting |
| `ALLOWED_ORIGIN` | `http://localhost:3000` | CORS allowed origin. Set to your domain in production. |
| `ENABLE_SWAGGER` | `true` | Enable Swagger UI at `/swagger`. Consider disabling in production. |

All compose-level configuration keys (for fine-grained control, edit `docker-compose.yml` directly):

| Compose Environment Key | Default | Description |
|--------------------------|---------|-------------|
| `Database__AutoMigrate` | `true` | Automatically run EF Core migrations on API startup |
| `SelfHosted__EnableSwagger` | `true` | Enable Swagger UI at `/swagger` |
| `SelfHosted__JwtExpirationMinutes` | `1440` | JWT token expiration in minutes (default: 24 hours) |
| `SelfHosted__AllowedOrigins__0` | `http://localhost:3000` | CORS allowed origin. Set to your domain in production. |
| `SelfHosted__RateLimiting__Enabled` | `true` | Enable API rate limiting |
| `SelfHosted__RateLimiting__Global__PermitLimit` | `100` | Maximum requests per window (global) |
| `SelfHosted__RateLimiting__Global__WindowSeconds` | `60` | Rate limit window duration in seconds (global) |
| `SelfHosted__RateLimiting__Auth__PermitLimit` | `5` | Maximum authentication attempts per window |
| `SelfHosted__RateLimiting__Auth__WindowSeconds` | `15` | Rate limit window for authentication in seconds |
| `AzureKeyVault__Enabled` | `false` | Enable Azure Key Vault for secret management |
| `AzureKeyVault__VaultUri` | -- | Azure Key Vault URI (e.g., `https://your-vault.vault.azure.net/`) |

## Connection Strings

The database connection string is configured internally in the compose file. If you need to customize it:

| Compose Environment Key | Description |
|--------------------------|-------------|
| `ConnectionStrings__McpDb` | SQL Server connection string. Default uses the `db` service with `SA_PASSWORD`. |

The default connection string in compose is:

```
Server=db,1433;Database=KnowzSelfHosted;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=True
```

You generally do not need to change this unless you are using an external SQL Server instance.

## Production Checklist

Before deploying to production:

- [ ] Change `SA_PASSWORD` to a strong, unique password
- [ ] Change `JWT_SECRET` to a random 64+ character string
- [ ] Change `ADMIN_PASSWORD` (or change it immediately after first login)
- [ ] Change `MCP_SERVICE_KEY` to a random string (shared secret between MCP and API)
- [ ] Set `SelfHosted__AllowedOrigins__0` to your actual domain
- [ ] Place the stack behind a reverse proxy with TLS (HTTPS)
- [ ] Consider setting `SelfHosted__EnableSwagger` to `false`
- [ ] Review rate limiting settings for your expected traffic
