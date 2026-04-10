// infrastructure/selfhosted-test.bicepparam
// Parameter file for self-hosted testing infrastructure.
//
// Usage:
//   az deployment group create -g rg-knowz-selfhosted \
//     -f infrastructure/selfhosted-test.bicep \
//     -p infrastructure/selfhosted-test.bicepparam \
//     -p sqlAdminPassword='<secure>'
//
// Optional overrides via environment variables:
//   SH_PREFIX            - Resource name prefix (default: sh-test)
//   SH_LOCATION          - Azure region (default: eastus2)
//   SH_DEPLOY_OPENAI     - true/false (default: true)
//   SH_SEARCH_SKU        - free/basic/standard (default: basic)
//   SH_EMBEDDING_MODEL   - text-embedding-3-small or text-embedding-3-large (default: text-embedding-3-large)
//   SH_ALLOW_ALL_IPS     - true/false, open SQL firewall to all IPs (default: false)
//   SH_DEPLOY_KEYVAULT   - true/false, deploy Key Vault (default: true)
//   SH_DEPLOY_MONITORING - true/false, deploy Log Analytics + App Insights (default: true)
//   SH_STORAGE_SHARED_KEY - true/false, allow storage shared key access (default: true)

using 'selfhosted-test.bicep'

param prefix = readEnvironmentVariable('SH_PREFIX', 'sh-test')
param location = readEnvironmentVariable('SH_LOCATION', 'eastus2')
param sqlAdminUsername = 'sqladmin'

// OpenAI: deploy locally or use external/shared
param deployOpenAI = readEnvironmentVariable('SH_DEPLOY_OPENAI', 'true') == 'true'
param externalOpenAiEndpoint = readEnvironmentVariable('SH_OPENAI_ENDPOINT', '')

// SQL firewall: allow all IPs (opt-in, default OFF)
param allowAllIps = readEnvironmentVariable('SH_ALLOW_ALL_IPS', 'false') == 'true'

// Search SKU: basic for testing, free if basic unavailable in region
param searchSku = readEnvironmentVariable('SH_SEARCH_SKU', 'basic')

// Search location: override if basic SKU unavailable in primary location
param searchLocation = readEnvironmentVariable('SH_SEARCH_LOCATION', readEnvironmentVariable('SH_LOCATION', 'eastus2'))

// Model deployment names (must match appsettings.json)
param chatDeploymentName = 'gpt-4o'
param embeddingDeploymentName = 'text-embedding-3-small'
param embeddingModelName = readEnvironmentVariable('SH_EMBEDDING_MODEL', 'text-embedding-3-large')

// Key Vault: deploy by default for enterprise secret management
param deployKeyVault = readEnvironmentVariable('SH_DEPLOY_KEYVAULT', 'true') == 'true'

// Monitoring: deploy Log Analytics + App Insights
param deployMonitoring = readEnvironmentVariable('SH_DEPLOY_MONITORING', 'true') == 'true'

// Storage shared key access: true for testing (connection string uses AccountKey)
// WARNING: Setting this to false will BREAK blob storage access. The app code
// (AzureBlobStorageProvider + StorageExtensions) only supports connection string auth
// (AccountKey). There is no DefaultAzureCredential/Managed Identity code path for
// blob storage yet. Disabling shared key access requires implementing MI-based
// BlobServiceClient in StorageExtensions.cs first.
param storageAllowSharedKeyAccess = readEnvironmentVariable('SH_STORAGE_SHARED_KEY', 'true') == 'true'

// Additional tags (optional)
// Use --parameters additionalTags='{"costCenter":"engineering"}' on CLI to add custom tags.

// ---- Container Apps (opt-in) ----
// Set SH_DEPLOY_CONTAINER_APPS=true to deploy API, MCP, and Web as Container Apps.
// Requires GHCR credentials (PAT with read:packages scope).
param deployContainerApps = readEnvironmentVariable('SH_DEPLOY_CONTAINER_APPS', 'false') == 'true'
param imageTag = readEnvironmentVariable('SH_IMAGE_TAG', 'latest')
param registryServer = readEnvironmentVariable('SH_REGISTRY_SERVER', 'ghcr.io')
param registryUsername = readEnvironmentVariable('SH_REGISTRY_USERNAME', '')

// Container Apps model deployment names (may differ from the OpenAI resource deployment names)
param caDeploymentName = readEnvironmentVariable('SH_CA_DEPLOYMENT_NAME', 'gpt-4o')
param caEmbeddingDeploymentName = readEnvironmentVariable('SH_CA_EMBEDDING_DEPLOYMENT_NAME', 'text-embedding-3-small')
