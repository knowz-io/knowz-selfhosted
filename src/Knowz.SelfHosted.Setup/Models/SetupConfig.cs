namespace Knowz.SelfHosted.Setup.Models;

public enum RunMode
{
    DockerCompose,
    AspireLocal,
    AspireAzure,
    DirectRun,
    AzureCloudDeploy
}

public enum AiMode
{
    NoAi,
    DirectAzure,
    PlatformProxy,
    AutoDetect
}

public enum StorageMode
{
    LocalFileSystem,
    AzureBlobStorage
}

public class SetupConfig
{
    // Run mode
    public RunMode RunMode { get; set; } = RunMode.DockerCompose;

    // AI configuration
    public AiMode AiMode { get; set; } = AiMode.NoAi;

    // Direct Azure OpenAI
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiApiKey { get; set; } = string.Empty;
    public string AzureOpenAiDeployment { get; set; } = "gpt-4o";
    public string AzureOpenAiEmbedding { get; set; } = "text-embedding-3-small";

    // Azure AI Search
    public string AzureSearchEndpoint { get; set; } = string.Empty;
    public string AzureSearchApiKey { get; set; } = string.Empty;
    public string AzureSearchIndex { get; set; } = "knowz-selfhosted";

    // Platform Proxy
    public string PlatformProxyUrl { get; set; } = "https://api.knowz.io";
    public string PlatformProxyApiKey { get; set; } = string.Empty;

    // Auto-detect Key Vault
    public string KeyVaultName { get; set; } = string.Empty;

    // Storage
    public StorageMode StorageMode { get; set; } = StorageMode.LocalFileSystem;
    public string AzureStorageConnectionString { get; set; } = string.Empty;
    public string AzureStorageContainer { get; set; } = "selfhosted-files";

    // Credentials
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "changeme";
    public string JwtSecret { get; set; } = string.Empty;
    public string SaPassword { get; set; } = "Knowz_Dev_P@ssw0rd!";
    public string McpServiceKey { get; set; } = "knowz-mcp-dev-service-key";

    // Advanced
    public string CorsOrigin { get; set; } = "http://localhost:3000";
    public bool RateLimitingEnabled { get; set; } = true;
    public bool SwaggerEnabled { get; set; } = true;
    public int McpPort { get; set; } = 3001;
}
