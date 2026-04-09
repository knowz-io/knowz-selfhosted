# Knowz Self-Hosted

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#blade/Microsoft_Azure_CreateUIDef/CustomDeploymentBlade/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2Fazuredeploy.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2FcreateUiDefinition.json)

Knowz is a self-hosted knowledge management platform with AI-powered search, chat, and a Model Context Protocol (MCP) server for AI tool integration.

## Features

- **Knowledge Management** -- Create vaults, organize knowledge items, tag and categorize content
- **AI-Powered Search** -- Semantic vector search with Azure OpenAI embeddings and Azure AI Search
- **AI Chat** -- Conversational interface over your knowledge base with source citations
- **MCP Integration** -- Model Context Protocol server lets AI assistants (Claude, Cursor, etc.) query your knowledge
- **Single Sign-On** -- Microsoft Entra ID (Azure AD) authentication with PKCE and confidential client modes
- **Data Portability** -- Import and export your data in standard formats
- **File Storage** -- Upload and attach files to knowledge items (local filesystem or Azure Blob)
- **API-First** -- Full REST API with Swagger documentation and per-user API keys
- **Enrichment Pipeline** -- Automatic text extraction, chunking, entity extraction, and summarization

## Architecture

```
┌───────────────────┐              ┌───────────────────────┐
│   Web UI (React)  │              │   MCP Server (.NET 9) │
│ localhost:3000    │              │   localhost:3001       │
│                   │              │   22 tools · OAuth     │
└────────┬──────────┘              └──────────┬────────────┘
         │ /api/* (nginx proxy)               │ HTTP proxy
         │                                    │
┌────────▼────────────────────────────────────▼────────────┐
│  Presentation — API (.NET 9, localhost:5000)             │
│  20 endpoint groups · JWT + API key auth · Rate limiting │
├─────────────────────────────────────────────────────────-┤
│  Application — Services                                  │
│  Auth · Knowledge · Vaults · Search · Chat · Enrichment  │
│  Files · Tags · Topics · Import/Export · SSO             │
├──────────────────────────────────────────────────────────┤
│  Infrastructure — Data + External Services               │
│  EF Core · Azure OpenAI · AI Search · Storage Providers  │
│  Content Extraction (PDF, DOCX) · Enrichment Pipeline    │
└──┬──────────┬──────────┬──────────┬──────────────────────┘
   │          │          │          │
┌──▼───────┐ ┌▼────────┐ ┌▼────────┐ ┌▼─────────────────┐
│ SQL      │ │ Azure   │ │ Azure   │ │ File Storage      │
│ Server   │ │ OpenAI  │ │ AI      │ │ (Local FS or      │
│(Database)│ │(optional)│ │ Search  │ │  Azure Blob)      │
└──────────┘ └─────────┘ └─────────┘ └───────────────────┘
```

**Services:**

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **API** | .NET 9 Minimal API | REST API, auth, enrichment pipeline, file storage |
| **Web** | React 19 + Vite + Tailwind | Single-page application |
| **MCP** | .NET 9 + MCP SDK | Model Context Protocol server for AI tools |
| **Database** | SQL Server 2022 Express | Single `KnowzSelfHosted` database |
| **File Storage** | Local filesystem or Azure Blob | Uploaded files and attachments |
| **Azure OpenAI** | GPT + Embeddings (optional) | Chat, summarization, entity extraction, vector embeddings |
| **Azure AI Search** | Vector + semantic search (optional) | Knowledge base search index |

## Quickstart (Docker Compose)

```bash
git clone https://github.com/knowz-io/knowz-selfhosted.git
cd knowz-selfhosted
cp .env.example .env
docker compose up -d
```

> **Default credentials:** Username `admin`, password `changeme`. Change these immediately in production.

Once the services are running:

| Service | URL |
|---------|-----|
| Web UI | [http://localhost:3000](http://localhost:3000) |
| API / Swagger | [http://localhost:5000/swagger](http://localhost:5000/swagger) |
| MCP Server | [http://localhost:3001](http://localhost:3001) |

The API automatically migrates the database and creates the SuperAdmin account on first startup.

### Environment Variables

Copy `.env.example` to `.env` and customize:

| Variable | Default | Description |
|----------|---------|-------------|
| `SA_PASSWORD` | `Knowz_Dev_P@ssw0rd!` | SQL Server SA password |
| `JWT_SECRET` | (example value) | JWT signing secret (64+ chars) |
| `ADMIN_USERNAME` | `admin` | SuperAdmin username |
| `ADMIN_PASSWORD` | `changeme` | SuperAdmin password |
| `MCP_PORT` | `3001` | MCP server host port |

See [Configuration Reference](docs/CONFIGURATION.md) for all environment variables including Azure OpenAI, Azure AI Search, SSO, storage, and rate limiting options.

## Development Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server container)

### Option A: Aspire AppHost (Recommended)

The Aspire AppHost orchestrates all services with a dashboard UI.

```bash
# 1. Install web client dependencies (one-time)
cd src/knowz-selfhosted-web && npm install && cd ../..

# 2. Start everything (SQL container + API + Web + Dashboard)
dotnet run --project src/Knowz.SelfHosted.AppHost --launch-profile local
```

The stack works immediately without AI credentials (auth, CRUD, import/export all functional). To enable AI features, configure via user-secrets:

```bash
cd src/Knowz.SelfHosted.AppHost

# Option 1: Knowz Platform Proxy (simplest -- just an API key)
dotnet user-secrets set "KnowzPlatform:Enabled" "true"
dotnet user-secrets set "KnowzPlatform:BaseUrl" "https://api.knowz.io"
dotnet user-secrets set "KnowzPlatform:ApiKey" "ukz_your_key"

# Option 2: Direct Azure (full-featured -- your own Azure resources)
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-openai.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "text-embedding-3-small"
dotnet user-secrets set "AzureAISearch:Endpoint" "https://your-search.search.windows.net/"
dotnet user-secrets set "AzureAISearch:ApiKey" "your-key"
dotnet user-secrets set "AzureAISearch:IndexName" "knowledge"
```

See [AppHost README](src/Knowz.SelfHosted.AppHost/README.md) for full details, modes, and tier comparison.

#### Aspire Dashboard

Opens automatically at `https://localhost:17200` showing all services, logs, traces, and metrics.

### Option B: Manual (without Aspire)

```bash
# Terminal 1: Start SQL Server
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourPassword123!" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

# Terminal 2: API
cd src/Knowz.SelfHosted.API
dotnet run --urls http://localhost:5000

# Terminal 3: Web
cd src/knowz-selfhosted-web
npm install && npm run dev

# Terminal 4: MCP
cd src/Knowz.MCP
dotnet run --urls http://localhost:8080
```

### Option C: Docker Compose

```bash
docker compose up --build
```

## Azure Deployment

### Standard Deployment

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#blade/Microsoft_Azure_CreateUIDef/CustomDeploymentBlade/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2Fazuredeploy.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2FcreateUiDefinition.json)

Deploys to a single resource group with public endpoints. Best for evaluation and small teams.

This deploys: SQL Server, Azure OpenAI (optional), Azure AI Search, Storage, Key Vault, Application Insights, and 3 Container Apps (API, MCP, Web). Estimated cost: ~$90-100/month at basic SKUs.

After deployment, access the Web UI at the URL shown in the deployment outputs. Default login: `admin` / your chosen password.

### Enterprise Deployment (Azure Landing Zone)

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#blade/Microsoft_Azure_CreateUIDef/CustomDeploymentBlade/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2Fselfhosted-enterprise.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fknowz-io%2Fknowz-selfhosted%2Fmain%2Finfrastructure%2FcreateUiDefinition.enterprise.json)

Policy-compliant deployment with VNet isolation, private endpoints, Azure Front Door with WAF, AAD-only SQL authentication, and full diagnostic logging. Designed for Azure landing zones with CSPM/CSE security policies.

Estimated cost: ~$200-300/month at standard SKUs with Front Door Premium.

**Post-deployment steps:**
1. Approve Front Door private link connections: `az network private-endpoint-connection list -g <rg> --type Microsoft.App/managedEnvironments` then approve each pending connection
2. Access the application via the Front Door endpoint (shown in deployment outputs)

### Infrastructure Only (local dev against Azure resources)

Provisions SQL, Azure OpenAI, AI Search, Storage, Key Vault, and Application Insights in a single resource group, then generates `appsettings.Local.json` for local development.

```powershell
.\selfhosted\infrastructure\selfhosted-deploy.ps1 -SqlPassword "YourSecurePassword123!"
```

**Options:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-SqlPassword` | (required) | SQL Server admin password |
| `-ResourceGroup` | `rg-knowz-selfhosted` | Azure resource group name |
| `-Location` | `eastus2` | Azure region |
| `-Prefix` | `sh-test` | Resource name prefix |
| `-SearchSku` | `basic` | AI Search SKU (`free`, `basic`, `standard`) |
| `-AllowAllIps` | off | Open SQL firewall to all IPs (dev only) |
| `-SkipKeyVault` | off | Skip Key Vault deployment |
| `-SkipMonitoring` | off | Skip Log Analytics + App Insights |
| `-SkipMigration` | off | Skip EF Core database migration |
| `-SkipSearchIndex` | off | Skip search index creation |

### Full Stack (Infrastructure + Container Apps)

Deploys everything above **plus** API, MCP, and Web as Azure Container Apps pulling images from GHCR.

```powershell
.\selfhosted\infrastructure\selfhosted-deploy.ps1 `
  -SqlPassword "YourSecurePassword123!" `
  -DeployContainerApps `
  -GhcrUsername "knowz-io" `
  -GhcrToken "ghp_your_token_here"
```

**Additional Container Apps parameters:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-DeployContainerApps` | off | Enable Container Apps deployment |
| `-ImageTag` | `latest` | Container image tag (e.g., `v1.0.0`) |
| `-GhcrUsername` | (required with CA) | GitHub username or org for GHCR |
| `-GhcrToken` | (required with CA) | GHCR PAT with `read:packages` scope |
| `-ApiKeyOverride` | (auto-generated) | API key for the deployment |
| `-JwtSecretOverride` | (auto-generated) | JWT signing secret |
| `-AdminPassword` | `changeme` | SuperAdmin password |

On completion, the script prints the Container App URLs:

```
Container Apps:
  API:  https://sh-test-api.<region>.azurecontainerapps.io
  MCP:  https://sh-test-mcp.<region>.azurecontainerapps.io
  Web:  https://sh-test-web.<region>.azurecontainerapps.io
```

### Teardown

Remove all Azure resources:

```powershell
.\selfhosted\infrastructure\selfhosted-teardown.ps1 -ResourceGroup "rg-knowz-selfhosted"
```

### Bicep Direct (advanced)

```bash
# Infrastructure only (backward compatible)
az deployment group create -g rg-knowz-selfhosted \
  -f selfhosted/infrastructure/selfhosted-test.bicep \
  -p sqlAdminPassword='YourPassword'

# With Container Apps
az deployment group create -g rg-knowz-selfhosted \
  -f selfhosted/infrastructure/selfhosted-test.bicep \
  -p sqlAdminPassword='YourPassword' \
     deployContainerApps=true \
     registryUsername='knowz-io' \
     registryPassword='ghp_...' \
     apiKey='your-api-key' \
     jwtSecret='your-jwt-secret'
```

## API Reference

The API exposes 20 endpoint groups. Full Swagger docs available at `/swagger` when running.

| Group | Prefix | Description |
|-------|--------|-------------|
| Auth | `/api/v1/auth` | Login, token refresh, SSO |
| Account | `/api/v1/account` | User profile, password change |
| API Keys | `/api/v1/apikeys` | Per-user API key management |
| Knowledge | `/api/v1/knowledge` | CRUD for knowledge items |
| Vaults | `/api/v1/vaults` | Vault management |
| Vault Access | `/api/v1/vault-access` | Per-user vault permissions |
| Search | `/api/v1/search` | Semantic + vector search |
| Chat | `/api/v1/chat` | AI chat over knowledge base |
| Topics | `/api/v1/topics` | Topic browsing |
| Tags | `/api/v1/tags` | Tag management |
| Entities | `/api/v1/entities` | AI-extracted entities |
| Files | `/api/v1/files` | File upload and management |
| Comments | `/api/v1/comments` | Knowledge item comments |
| Inbox | `/api/v1/inbox` | Staging area for new items |
| Portability | `/api/v1/portability` | Import/export data |
| Config | `/api/v1/config` | Runtime configuration |
| SSO | `/api/v1/sso` | SSO/OIDC configuration |
| Admin | `/api/v1/admin` | Admin operations |
| MCP Internal | `/api/v1/mcp` | Internal endpoints for MCP server |
| Health | `/healthz` | Health check |

**Authentication:** Two schemes supported:
- **JWT Bearer** -- Login via `/api/v1/auth/login`, use `Authorization: Bearer <token>`
- **API Key** -- Generate at `/api/v1/apikeys`, use `X-Api-Key: ksh_...` header

## MCP Server

The MCP server exposes 22 tools for AI assistants to interact with your knowledge base.

### Connecting Claude Desktop / Cursor

```json
{
  "mcpServers": {
    "knowz": {
      "url": "http://localhost:3001/mcp",
      "headers": {
        "X-Api-Key": "ksh_your_api_key_here"
      }
    }
  }
}
```

### Available Tools

| Tool | Description |
|------|-------------|
| `search_knowledge` | Semantic search across knowledge base |
| `get_knowledge_item` | Get a specific knowledge item by ID |
| `create_knowledge` | Create a new knowledge item |
| `update_knowledge` | Update an existing knowledge item |
| `list_knowledge_items` | List knowledge items with pagination |
| `bulk_get_knowledge_items` | Retrieve multiple items by ID |
| `count_knowledge` | Count knowledge items with filters |
| `list_vaults` | List available vaults |
| `list_vault_contents` | Browse vault contents |
| `create_vault` | Create a new vault |
| `list_topics` | List topics |
| `get_topic_details` | Get topic details |
| `find_entities` | Search AI-extracted entities |
| `ask_question` | Ask a question over the knowledge base |
| `create_inbox_item` | Add item to inbox staging area |
| `search_by_file_pattern` | Search by file path pattern |
| `search_by_title_pattern` | Search by title pattern |
| `get_statistics` | Get knowledge base statistics |
| `add_comment` | Add a comment to a knowledge item |
| `list_comments` | List comments on a knowledge item |
| `amend_knowledge` | Apply AI edit instructions to existing content |
| `attach_files` | Attach uploaded files to knowledge items |

## Project Structure

```
selfhosted/
├── docker-compose.yml              # Docker Compose for local stack
├── .env.example                    # Environment variable template
├── Knowz.SelfHosted.sln            # Visual Studio solution
├── infrastructure/
│   ├── selfhosted-test.bicep       # Azure Bicep template (infra + Container Apps)
│   ├── selfhosted-test.bicepparam  # Bicep parameter file
│   ├── selfhosted-deploy.ps1       # One-command deployment script
│   └── selfhosted-teardown.ps1     # Resource cleanup script
├── docs/
│   ├── QUICKSTART.md               # 5-minute Docker setup
│   ├── CONFIGURATION.md            # Full environment variable reference
│   └── ARCHITECTURE.md             # System design and data flow
├── src/
│   ├── Knowz.SelfHosted.API/       # REST API (Minimal API, .NET 9)
│   │   ├── Endpoints/              # 20 endpoint modules
│   │   ├── Dockerfile              # Multi-stage Docker build
│   │   └── Program.cs              # Startup, DI, auth, middleware
│   ├── Knowz.SelfHosted.Application/
│   │   └── Services/               # Business logic (auth, knowledge, search, etc.)
│   ├── Knowz.SelfHosted.Infrastructure/
│   │   ├── Data/                   # EF Core DbContext, migrations, entities
│   │   └── Services/               # Azure integrations, storage, enrichment
│   ├── Knowz.SelfHosted.AppHost/   # Aspire orchestrator (azure/local modes)
│   ├── Knowz.SelfHosted.Tests/     # xUnit integration tests
│   ├── Knowz.MCP/                  # MCP server (19 tools, OAuth sessions)
│   ├── Knowz.MCP.Tests/            # MCP unit tests
│   └── knowz-selfhosted-web/       # React SPA (Vite, Tailwind, TypeScript)
│       ├── src/pages/              # 20+ pages
│       ├── src/components/         # Reusable UI components
│       ├── Dockerfile              # nginx + envsubst for runtime config
│       └── vite.config.ts          # Dev server proxy config
└── .github/workflows/
    ├── ci.yml                      # PR checks (build, test, Docker)
    └── release.yml                 # Multi-arch GHCR image publish
```

## AI Features (Optional)

Knowz runs as a fully functional knowledge management platform without any AI services. To enable AI-powered search, chat, and automatic enrichment, configure one of two AI tiers:

| Tier | Name | Setup | Search Quality |
|------|------|-------|----------------|
| 1 | **Knowz Platform Proxy** | 3 config values (API key) | Keyword search |
| 2 | **Direct Azure** | 7 config values (your Azure resources) | Vector + keyword hybrid |

**Tier 1 (Platform Proxy)** delegates AI operations to the Knowz Platform API -- no Azure subscription needed. All AI features (chat, summarization, embeddings, enrichment) work via the platform. Search uses local keyword matching.

**Tier 2 (Direct Azure)** uses your own Azure OpenAI and Azure AI Search resources. Provides the best search quality with hybrid vector + keyword search.

See [Configuration Reference](docs/CONFIGURATION.md) for setup instructions.

### Enrichment Pipeline

When AI services are configured (either tier), the enrichment pipeline automatically processes new knowledge items:

1. **Content Extraction** -- PDF, DOCX, and text files parsed to plain text
2. **Chunking** -- Content split into overlapping chunks for embedding
3. **Entity Extraction** -- AI identifies people, places, organizations, concepts
4. **Summarization** -- AI generates concise summaries
5. **Vector Embedding** -- Content embedded for semantic search
6. **Search Indexing** -- Chunks indexed for retrieval (Azure AI Search in Tier 2, local DB in Tier 1)

Without AI configured, knowledge items are stored and accessible via exact-match search, tags, and manual organization.

## CI/CD

### Pull Request Checks (`ci.yml`)

Runs on PRs to `main`:
- .NET build and test (all projects)
- Node.js build and test (web client)
- Docker image builds (no push)

### Release (`release.yml`)

Triggered by version tags (`v*.*.*`) or manual dispatch:
- Multi-architecture builds (`linux/amd64`, `linux/arm64`)
- Pushes to GHCR with semver tags + `latest`:
  - `ghcr.io/knowz-io/knowz-selfhosted-api`
  - `ghcr.io/knowz-io/knowz-selfhosted-web`
  - `ghcr.io/knowz-io/knowz-selfhosted-mcp`

## Testing

```bash
# .NET tests
dotnet test Knowz.SelfHosted.sln

# Web client tests
cd src/knowz-selfhosted-web
npm test

# Full Docker stack
docker compose up --build
# Then: http://localhost:3000
```

## Documentation

| Document | Description |
|----------|-------------|
| [Quickstart Guide](docs/QUICKSTART.md) | 5-minute setup with Docker |
| [Configuration Reference](docs/CONFIGURATION.md) | All environment variables and settings |
| [Architecture Overview](docs/ARCHITECTURE.md) | System design, service diagram, data flow |
| [Contributing](CONTRIBUTING.md) | Development setup, PR process, coding standards |
| [Security Policy](SECURITY.md) | Vulnerability reporting and security practices |

## License

This project is licensed under the [Apache License 2.0](LICENSE).

Copyright 2026 Knowz AI (knowzai.com)
