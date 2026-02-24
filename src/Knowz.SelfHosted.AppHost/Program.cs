var builder = DistributedApplication.CreateBuilder(args);

// =====================================================================
// ASPIRE_MODE: "azure" (default) or "local"
// =====================================================================
// azure: Projects read from appsettings.Local.json (SQL, OpenAI, Search, Storage).
//        No containers — everything runs against live Azure resources.
// local: Spins up SQL Server container. OpenAI/Search still require Azure config
//        in appsettings. Storage uses LocalFileSystem.
var mode = builder.Configuration["ASPIRE_MODE"] ?? "azure";
var isLocal = mode.Equals("local", StringComparison.OrdinalIgnoreCase);

IResourceBuilder<IResourceWithConnectionString>? sqlDb = null;

if (isLocal)
{
    var sql = builder.AddSqlServer("sql")
        .WithLifetime(ContainerLifetime.Persistent);

    sqlDb = sql.AddDatabase("McpDb");
}

// =====================================================================
// AI SERVICES CONFIGURATION
// =====================================================================
// The selfhosted API uses a three-tier fallback:
//   Tier 1: KnowzPlatform proxy (KnowzPlatform:Enabled + BaseUrl + ApiKey)
//   Tier 2: Direct Azure OpenAI + Azure AI Search (endpoints + keys)
//   Tier 3: NoOp (AI features disabled)
//
// Configure in ONE of these ways (in priority order):
//   1. Set values below (AppHost injects as env vars — highest priority)
//   2. Create appsettings.Local.json in the API project (see .example file)
//   3. Leave empty for NoOp mode
// =====================================================================

// --- Tier 1: Knowz Platform Proxy ---
var platformEnabled = builder.Configuration["KnowzPlatform:Enabled"];
var platformBaseUrl = builder.Configuration["KnowzPlatform:BaseUrl"];
var platformApiKey = builder.Configuration["KnowzPlatform:ApiKey"];

// --- Tier 2: Direct Azure OpenAI + Azure AI Search ---
var openAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var openAiApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var openAiDeployment = builder.Configuration["AzureOpenAI:DeploymentName"];
var openAiEmbedding = builder.Configuration["AzureOpenAI:EmbeddingDeploymentName"];
var searchEndpoint = builder.Configuration["AzureAISearch:Endpoint"];
var searchApiKey = builder.Configuration["AzureAISearch:ApiKey"];
var searchIndexName = builder.Configuration["AzureAISearch:IndexName"];

// ===== SELF-HOSTED API =====
var api = builder.AddProject<Projects.Knowz_SelfHosted_API>("selfhosted-api")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Database__AutoMigrate", "true");

if (isLocal)
{
    api.WithReference(sqlDb!)
        .WaitFor(sqlDb!)
        .WithEnvironment("Storage__Provider", "LocalFileSystem")
        .WithEnvironment("Storage__Local__RootPath", "/tmp/knowz-files");
}

// Inject AI config from AppHost configuration (if present).
// These env vars override the API's own appsettings, so AppHost config wins.
if (!string.IsNullOrWhiteSpace(platformEnabled))
{
    api.WithEnvironment("KnowzPlatform__Enabled", platformEnabled);
    if (!string.IsNullOrWhiteSpace(platformBaseUrl))
        api.WithEnvironment("KnowzPlatform__BaseUrl", platformBaseUrl);
    if (!string.IsNullOrWhiteSpace(platformApiKey))
        api.WithEnvironment("KnowzPlatform__ApiKey", platformApiKey);
}

if (!string.IsNullOrWhiteSpace(openAiEndpoint))
    api.WithEnvironment("AzureOpenAI__Endpoint", openAiEndpoint);
if (!string.IsNullOrWhiteSpace(openAiApiKey))
    api.WithEnvironment("AzureOpenAI__ApiKey", openAiApiKey);
if (!string.IsNullOrWhiteSpace(openAiDeployment))
    api.WithEnvironment("AzureOpenAI__DeploymentName", openAiDeployment);
if (!string.IsNullOrWhiteSpace(openAiEmbedding))
    api.WithEnvironment("AzureOpenAI__EmbeddingDeploymentName", openAiEmbedding);

if (!string.IsNullOrWhiteSpace(searchEndpoint))
    api.WithEnvironment("AzureAISearch__Endpoint", searchEndpoint);
if (!string.IsNullOrWhiteSpace(searchApiKey))
    api.WithEnvironment("AzureAISearch__ApiKey", searchApiKey);
if (!string.IsNullOrWhiteSpace(searchIndexName))
    api.WithEnvironment("AzureAISearch__IndexName", searchIndexName);

// ===== MCP SERVER =====
var mcp = builder.AddProject<Projects.Knowz_MCP>("mcp")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Knowz__BaseUrl", api.GetEndpoint("http"))
    .WithEnvironment("MCP__ApiKeyValidationEndpoint", "/api/vaults");

// ===== SELF-HOSTED WEB CLIENT (React + Vite) =====
builder.AddNpmApp("selfhosted-web", "../knowz-selfhosted-web", "dev")
    .WithHttpEndpoint(targetPort: 5173, name: "http", isProxied: false)
    .WithReference(api);

builder.Build().Run();
