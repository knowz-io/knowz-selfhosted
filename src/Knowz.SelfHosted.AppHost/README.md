# Knowz Self-Hosted AppHost (Aspire)

Dedicated .NET Aspire orchestrator for the self-hosted scenario. Starts everything you need for local development with a single command.

## What it starts

| Resource | Description | Port |
|----------|-------------|------|
| `sql` | SQL Server container with `McpDb` database | (dynamic) |
| `selfhosted-api` | Knowz Self-Hosted API (ASP.NET Core) | 5000 |
| `selfhosted-web` | Knowz Self-Hosted Web Client (React + Vite) | 5173 |
| Aspire Dashboard | Traces, logs, metrics | 17200 |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server container)
- [Node.js 20+](https://nodejs.org/) (for the web client)

## Quick Start (no AI credentials needed)

```bash
# 1. Install web client dependencies (one-time)
cd src/knowz-selfhosted-web && npm install && cd ../..

# 2. Start everything
dotnet run --project src/Knowz.SelfHosted.AppHost
```

This starts the full stack immediately. Auth, admin, CRUD, import/export, and the web UI all work without any AI credentials. Search and AI features (Q&A, embeddings, summarization) return "not configured" responses until you add credentials.

## Setup

### 1. Install npm dependencies for the web client

```bash
cd src/knowz-selfhosted-web
npm install
cd ../..
```

### 2. (Optional) Configure AI credentials via user-secrets

AI credentials enable search and AI features. Without them, the API runs with NoOp services — all non-AI features work normally.

```bash
# Azure OpenAI (enables Q&A, embeddings, summarization)
dotnet user-secrets set "Parameters:azure-openai-endpoint" "https://your-openai.openai.azure.com/" --project src/Knowz.SelfHosted.AppHost
dotnet user-secrets set "Parameters:azure-openai-apikey" "<your-openai-key>" --project src/Knowz.SelfHosted.AppHost

# Azure AI Search (enables hybrid search, indexing)
dotnet user-secrets set "Parameters:azure-ai-search-endpoint" "https://your-search.search.windows.net" --project src/Knowz.SelfHosted.AppHost
dotnet user-secrets set "Parameters:azure-ai-search-apikey" "<your-search-key>" --project src/Knowz.SelfHosted.AppHost
dotnet user-secrets set "Parameters:azure-ai-search-indexname" "knowledge" --project src/Knowz.SelfHosted.AppHost
```

You can configure OpenAI and Search independently — each service falls back to NoOp when its credentials are missing.

### 3. Verify secrets are set

```bash
dotnet user-secrets list --project src/Knowz.SelfHosted.AppHost
```

## Run

```bash
dotnet run --project src/Knowz.SelfHosted.AppHost
```

This starts:
1. **SQL Server container** — with `McpDb` database (auto-created by Aspire)
2. **Self-Hosted API** — applies EF Core migrations automatically (`Database__AutoMigrate=true`), seeds SuperAdmin user. AI credentials injected only if configured.
3. **Self-Hosted Web Client** — Vite dev server with hot reload, proxies `/api` requests to the API
4. **Aspire Dashboard** — opens in browser automatically (traces, logs, metrics for all resources)

## How it works

- **SQL Server**: Aspire starts a containerized SQL Server and creates `McpDb`. The connection string is injected into the API automatically via `ConnectionStrings:McpDb`.
- **Auto-migration**: On startup, the API runs `Database.MigrateAsync()` to apply all pending EF Core migrations. If migration fails (e.g., SQL still starting), it logs a warning and continues.
- **SuperAdmin seeding**: After migration, the API seeds the SuperAdmin user from `appsettings.json` config.
- **AI credentials**: Read from user-secrets and conditionally injected into the API as environment variables. If not configured, no AI env vars are set, the API reads empty values from `appsettings.json`, and NoOp service implementations are used (search and AI features gracefully disabled). OpenAI and Search can be configured independently.
- **Web client**: The Vite dev server proxies `/api` requests to `localhost:5000` (the API's fixed port).

## Ports

| Service | Port | Purpose |
|---------|------|---------|
| Aspire Dashboard (HTTPS) | 17200 | Resource monitoring |
| Aspire Dashboard (HTTP) | 17201 | Resource monitoring |
| Self-Hosted API | 5000 | REST API (fixed port for Vite proxy) |
| Self-Hosted Web | 5173 | Vite dev server |
| OTLP Endpoint | 21200/21201 | OpenTelemetry collector |

Port range 172xx avoids conflicts with the full platform AppHost (151xx/170xx).

## Data Portability (Import/Export)

Knowz Self-Hosted includes a full data portability system for migrating data between hosting models — self-hosted to self-hosted, or between the Knowz platform (cloud) and self-hosted — with zero data loss.

### Portability API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/portability/export` | Export all tenant data as JSON |
| POST | `/api/portability/import/validate` | Dry-run validation (preview before import) |
| POST | `/api/portability/import?strategy=skip` | Execute import with conflict strategy |
| GET | `/api/portability/schema` | Check schema version and compatibility |

All endpoints require authentication.

### Quick Start

```bash
# Export your data
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/portability/export > backup.json

# Validate an import (dry-run, no changes made)
curl -s -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @backup.json \
  http://localhost:5000/api/portability/import/validate

# Import with conflict handling
curl -s -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d @backup.json \
  "http://localhost:5000/api/portability/import?strategy=overwrite"

# Check schema compatibility
curl -s -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/portability/schema
```

### Conflict Strategies

| Strategy | Behavior |
|----------|----------|
| `skip` (default) | Keep existing data, skip conflicts |
| `overwrite` | Replace existing data with imported data |
| `merge` | Fill in missing fields only, keep existing non-null values |

### Platform Round-Trip

When migrating from the Knowz cloud platform to self-hosted and back, all platform-specific data (AI enrichments, entity extraction results, perspectives, temporal contexts, etc.) is preserved transparently via the `PlatformData` mechanism. Self-hosted doesn't need to understand these fields — they're stored opaquely and restored on re-export.

For full documentation, see [Data Portability Guide](../../docs/DATA_PORTABILITY.md).

## Standalone mode (without Aspire)

The API can still be run standalone without Aspire:

```bash
dotnet run --project src/Knowz.SelfHosted.API
```

In this mode, configure the database connection string in `appsettings.Local.json` (gitignored) and auto-migration is not triggered (no `Database__AutoMigrate` env var).

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Aspire workload not installed" | Run `dotnet workload install aspire` |
| SQL container fails to start | Ensure Docker Desktop is running |
| Migration fails on startup | SQL may still be initializing — the API logs a warning and retries on next restart |
| Search/Q&A returns "not configured" | This is expected without AI credentials. See [Setup step 2](#2-optional-configure-ai-credentials-via-user-secrets) to enable AI features |
| Web client not starting | Run `npm install` in `src/knowz-selfhosted-web/` first |
| Port conflict on 5000 | Stop any other process using port 5000, or modify `.WithHttpEndpoint(port: 5000)` in Program.cs |
