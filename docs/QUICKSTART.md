# Quickstart Guide

Get Knowz Self-Hosted running in under 5 minutes with Docker.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose plugin)
- At least 2 GB of available RAM (SQL Server requirement)

## Step 1: Clone the Repository

```bash
git clone https://github.com/knowz-io/knowz-selfhosted.git
cd knowz-selfhosted
```

## Step 2: Configure Environment

Copy the example environment file:

```bash
cp .env.example .env
```

Open `.env` and review the settings. For a quick start, the defaults work out of the box. For production use, you should change these values:

| Variable | Default | Recommendation |
|----------|---------|----------------|
| `SA_PASSWORD` | `Knowz_Dev_P@ssw0rd!` | Use a strong password (min 8 chars, mixed case + number/symbol) |
| `JWT_SECRET` | `change-this-to-a-random-64-char-string-in-production-please!!` | Generate a random 64+ character string |
| `ADMIN_USERNAME` | `admin` | Change if desired |
| `ADMIN_PASSWORD` | `changeme` | Change immediately after first login |

## Step 3: Start the Stack

```bash
docker compose up -d
```

This starts 4 services:

| Service | Description | Port |
|---------|-------------|------|
| **db** | SQL Server 2022 Express | 1433 |
| **api** | Knowz API (.NET 9) | 5000 |
| **web** | Web UI (React + nginx) | 3000 |
| **mcp** | MCP Server | 3001 |

## Step 4: Wait for Startup

On first run, the API will:

1. Wait for SQL Server to become healthy
2. Run database migrations automatically
3. Create the SuperAdmin account

Check that all services are running:

```bash
docker compose ps
```

All services should show a `healthy` or `running` status. The API may take 30-60 seconds on first startup while it initializes the database.

## Step 5: Log In

Open [http://localhost:3000](http://localhost:3000) in your browser and log in with:

- **Username:** `admin` (or your configured `ADMIN_USERNAME`)
- **Password:** `changeme` (or your configured `ADMIN_PASSWORD`)

## First Steps After Login

1. **Create a vault** -- Vaults are containers for organizing your knowledge
2. **Add knowledge** -- Create knowledge items manually or upload files
3. **Try search** -- Search across your knowledge base (full-text search works without AI; semantic search requires Azure OpenAI)
4. **Generate an API key** -- Go to Settings to create a per-user API key for programmatic access

## Enabling AI Features

Knowz works without AI services for basic knowledge management. To enable AI-powered search, chat, and automatic enrichment, choose one of two approaches:

### Option 1: Knowz Platform Proxy (simplest)

No Azure subscription needed -- just an API key from your Knowz account. Add to your `.env` file:

```bash
KNOWZ_PLATFORM_ENABLED=true
KNOWZ_PLATFORM_BASE_URL=https://api.knowz.io
KNOWZ_PLATFORM_API_KEY=ukz_your_api_key
```

All AI features (chat, summarization, embeddings, enrichment) work via the platform. Search uses local keyword matching.

### Option 2: Direct Azure (full-featured)

Uses your own Azure resources for the best search quality (hybrid vector + keyword):

1. Set up an [Azure OpenAI](https://azure.microsoft.com/en-us/products/ai-services/openai-service) resource
2. Set up an [Azure AI Search](https://azure.microsoft.com/en-us/products/ai-services/ai-search) resource
3. Add the credentials to your `.env` file (see [Configuration Reference](CONFIGURATION.md))

### After configuring either option:

```bash
docker compose up -d
```

## Stopping and Restarting

```bash
# Stop all services (data is preserved in Docker volumes)
docker compose down

# Start again
docker compose up -d
```

## Upgrading

```bash
# Pull the latest images
docker compose pull

# Restart with new images (data persists in volumes)
docker compose up -d
```

The API automatically applies any new database migrations on startup.

## Troubleshooting

### Port Conflicts

If ports 1433, 3000, 3001, or 5000 are already in use, either stop the conflicting service or change the port mapping in `docker-compose.yml`:

```yaml
ports:
  - "3001:8080"  # Change the left side (host port) to an available port
```

### SQL Server Memory

SQL Server 2022 requires at least 2 GB of RAM. If the `db` container exits immediately, check your Docker Desktop memory allocation under Settings > Resources.

### API Fails to Start

Check the API logs for details:

```bash
docker compose logs api
```

Common issues:
- **Database connection failed** -- The SQL Server container may still be starting. The API retries automatically with exponential backoff (up to 10 attempts).
- **Migration error** -- If the database was manually modified, you may need to reset it: `docker compose down -v` (warning: this deletes all data).

### ARM64 (Apple Silicon) Notes

SQL Server 2022 runs under Rosetta emulation on Apple Silicon Macs. Performance is adequate for development and small deployments. Ensure Docker Desktop has Rosetta emulation enabled (Settings > General > Use Rosetta).

### Viewing Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
```
