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
