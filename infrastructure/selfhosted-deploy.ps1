# infrastructure/selfhosted-deploy.ps1
# Deploys the self-hosted Knowz infrastructure to an isolated Azure resource group.
#
# Usage:
#   .\infrastructure\selfhosted-deploy.ps1 -SqlPassword "YourSecurePassword123!"
#   .\infrastructure\selfhosted-deploy.ps1 -SqlPassword "P@ss" -ResourceGroup "rg-my-selfhosted" -Location "westus2"
#   .\infrastructure\selfhosted-deploy.ps1 -SqlPassword "P@ss" -SkipKeyVault -SkipMonitoring
#   .\infrastructure\selfhosted-deploy.ps1 -SqlPassword "P@ss" -DeployContainerApps -GhcrUsername "knowz-io" -GhcrToken "ghp_..."
#
# After deployment, outputs are captured and can be used to configure:
#   - appsettings.Local.json for local development
#   - EF Core migration connection string
#   - Azure AI Search index creation

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SqlPassword,

    [string]$ResourceGroup = "rg-knowz-selfhosted",
    [string]$Location = "eastus2",
    [string]$Prefix = "sh-test",
    [string]$SearchSku = "basic",
    [string]$SearchLocation = "",
    [string]$EmbeddingModel = "text-embedding-3-small",
    [switch]$SkipMigration,
    [switch]$SkipSearchIndex,
    [switch]$AllowAllIps,
    [switch]$SkipKeyVault,
    [switch]$SkipMonitoring,
    [switch]$SkipDocumentIntelligence,
    [switch]$DisableStorageSharedKey,

    # Container Apps deployment (opt-in)
    [switch]$DeployContainerApps,
    [string]$ImageTag = "latest",
    [string]$GhcrUsername = "",
    [string]$GhcrToken = "",
    [string]$ApiKeyOverride = "",
    [string]$JwtSecretOverride = "",
    [string]$AdminPassword = "changeme"
)

$ErrorActionPreference = "Continue"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Knowz Self-Hosted Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location:       $Location"
Write-Host "Prefix:         $Prefix"
# Default search location to primary location if not specified
if (-not $SearchLocation) { $SearchLocation = $Location }

Write-Host "Search SKU:     $SearchSku"
Write-Host "Search Region:  $SearchLocation"
Write-Host "Embedding:      $EmbeddingModel"
if (-not $SkipKeyVault) { Write-Host "Key Vault:      Enabled" -ForegroundColor Green }
if (-not $SkipMonitoring) { Write-Host "Monitoring:     Enabled" -ForegroundColor Green }
if (-not $SkipDocumentIntelligence) { Write-Host "Doc Intel:      Enabled" -ForegroundColor Green }
if ($DisableStorageSharedKey) {
    Write-Host "Storage:        Shared key DISABLED" -ForegroundColor DarkYellow
    Write-Host "  WARNING: Storage shared key access disabled. Connection strings using AccountKey will not work." -ForegroundColor DarkYellow
    Write-Host "  All storage access must use Managed Identity (DefaultAzureCredential)." -ForegroundColor DarkYellow
}
if ($DeployContainerApps) {
    Write-Host "Container Apps: Enabled" -ForegroundColor Green
    Write-Host "  Image Tag:    $ImageTag"
    if (-not $GhcrUsername -or -not $GhcrToken) {
        throw "Container Apps deployment requires -GhcrUsername and -GhcrToken parameters"
    }
}
Write-Host ""

# Auto-generate API key and JWT secret for Container Apps if not provided
if ($DeployContainerApps) {
    if (-not $ApiKeyOverride) {
        $ApiKeyOverride = "sh-" + [guid]::NewGuid().ToString("N").Substring(0, 24)
        Write-Host "  Auto-generated API key for Container Apps." -ForegroundColor DarkGray
    }
    if (-not $JwtSecretOverride) {
        $JwtSecretOverride = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object { [char]$_ })
        Write-Host "  Auto-generated JWT secret for Container Apps." -ForegroundColor DarkGray
    }
}

# Step 1: Create resource group
Write-Host "[1/7] Creating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none 2>$null
if ($LASTEXITCODE -ne 0) { throw "Failed to create resource group" }
Write-Host "  Resource group '$ResourceGroup' ready." -ForegroundColor Green

# Step 1.5: Purge any soft-deleted resources that would conflict
Write-Host "[1.5/7] Checking for soft-deleted resources..." -ForegroundColor Yellow

# Check for soft-deleted Cognitive Services accounts
$openAiName = "$Prefix-openai-$Location"
$deletedAccounts = az cognitiveservices account list-deleted --output json 2>$null | ConvertFrom-Json
$conflicting = $deletedAccounts | Where-Object { $_.name -eq $openAiName }
if ($conflicting) {
    Write-Host "  Purging soft-deleted OpenAI account '$openAiName'..." -ForegroundColor DarkYellow
    az cognitiveservices account purge --name $openAiName --resource-group $ResourceGroup --location $Location 2>$null
    Write-Host "  Purged." -ForegroundColor Green
}

# Check for soft-deleted Key Vault
if (-not $SkipKeyVault) {
    $kvPrefix = [string]::new(($Prefix -replace '-','').ToLower().ToCharArray()[0..7])
    $deletedVaults = az keyvault list-deleted --query "[?starts_with(name, '$kvPrefix')]" -o json 2>$null | ConvertFrom-Json
    foreach ($vault in $deletedVaults) {
        Write-Host "  Purging soft-deleted Key Vault '$($vault.name)'..." -ForegroundColor DarkYellow
        az keyvault purge --name $vault.name 2>$null
        Write-Host "  Purged." -ForegroundColor Green
    }
}

# Check for soft-deleted Form Recognizer (Document Intelligence)
if (-not $SkipDocumentIntelligence) {
    $diName = "$Prefix-docintel-$Location"
    $conflictingDI = $deletedAccounts | Where-Object { $_.name -eq $diName }
    if ($conflictingDI) {
        Write-Host "  Purging soft-deleted Document Intelligence '$diName'..." -ForegroundColor DarkYellow
        az cognitiveservices account purge --name $diName --resource-group $ResourceGroup --location $Location 2>$null
        Write-Host "  Purged." -ForegroundColor Green
    }
}

if (-not $conflicting -and -not $deletedVaults -and -not $conflictingDI) {
    Write-Host "  No conflicting soft-deleted resources." -ForegroundColor Green
}

# Step 2: Deploy Bicep
Write-Host "[2/7] Deploying infrastructure (Bicep)..." -ForegroundColor Yellow
$bicepFile = Join-Path $scriptDir "selfhosted-test.bicep"

# First compile Bicep to ARM JSON (avoids Bicep CLI issues during deployment)
$armJsonFile = Join-Path $scriptDir "selfhosted-test.json"
az bicep build --file $bicepFile --outfile $armJsonFile 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Failed to compile Bicep to ARM JSON." -ForegroundColor Red
    throw "Bicep compilation failed"
}

# Deploy ARM JSON via REST API (az deployment group create has a bug in 2.77.0)
$token = (az account get-access-token --query accessToken -o tsv 2>$null)
$subId = (az account show --query id -o tsv 2>$null)
$templateJson = Get-Content $armJsonFile -Raw
$deployBody = @{
    properties = @{
        mode = "Incremental"
        template = ($templateJson | ConvertFrom-Json)
        parameters = @{
            prefix = @{ value = $Prefix }
            location = @{ value = $Location }
            sqlAdminPassword = @{ value = $SqlPassword }
            searchSku = @{ value = $SearchSku }
            searchLocation = @{ value = $SearchLocation }
            embeddingModelName = @{ value = $EmbeddingModel }
            allowAllIps = @{ value = [bool]$AllowAllIps }
            deployKeyVault = @{ value = -not [bool]$SkipKeyVault }
            deployMonitoring = @{ value = -not [bool]$SkipMonitoring }
            deployDocumentIntelligence = @{ value = -not [bool]$SkipDocumentIntelligence }
            storageAllowSharedKeyAccess = @{ value = -not [bool]$DisableStorageSharedKey }
            deployContainerApps = @{ value = [bool]$DeployContainerApps }
            imageTag = @{ value = $ImageTag }
            registryServer = @{ value = "ghcr.io" }
            registryUsername = @{ value = $GhcrUsername }
            registryPassword = @{ value = $GhcrToken }
            apiKey = @{ value = $ApiKeyOverride }
            jwtSecret = @{ value = $JwtSecretOverride }
            adminPassword = @{ value = $AdminPassword }
        }
    }
} | ConvertTo-Json -Depth 100 -Compress

$deployUri = "https://management.azure.com/subscriptions/$subId/resourcegroups/$ResourceGroup/providers/Microsoft.Resources/deployments/selfhosted-test?api-version=2025-04-01"

try {
    $deployResponse = Invoke-RestMethod -Uri $deployUri -Method Put -Body $deployBody `
        -ContentType "application/json" `
        -Headers @{ "Authorization" = "Bearer $token" }
} catch {
    $errMsg = $_.ErrorDetails.Message
    Write-Host "  Deployment submission failed: $errMsg" -ForegroundColor Red
    throw "Bicep deployment failed: $errMsg"
}

# Poll deployment status until complete
Write-Host "  Deployment started. Polling for completion..." -ForegroundColor DarkGray
$statusUri = "https://management.azure.com/subscriptions/$subId/resourcegroups/$ResourceGroup/providers/Microsoft.Resources/deployments/selfhosted-test?api-version=2025-04-01"
$maxPollTime = 1800  # 30 minutes
$pollElapsed = 0
$pollInterval = 15

while ($pollElapsed -lt $maxPollTime) {
    Start-Sleep -Seconds $pollInterval
    $pollElapsed += $pollInterval

    try {
        $statusResponse = Invoke-RestMethod -Uri $statusUri -Method Get `
            -Headers @{ "Authorization" = "Bearer $token" }
    } catch {
        # Token might have expired, refresh it
        $token = (az account get-access-token --query accessToken -o tsv 2>$null)
        $statusResponse = Invoke-RestMethod -Uri $statusUri -Method Get `
            -Headers @{ "Authorization" = "Bearer $token" }
    }

    $provisioningState = $statusResponse.properties.provisioningState
    Write-Host "  Status: $provisioningState ($pollElapsed s)" -ForegroundColor DarkGray

    if ($provisioningState -eq "Succeeded") {
        break
    }
    if ($provisioningState -eq "Failed" -or $provisioningState -eq "Canceled") {
        $errorDetails = $statusResponse.properties.error | ConvertTo-Json -Depth 5
        Write-Host "  Deployment failed: $errorDetails" -ForegroundColor Red
        throw "Bicep deployment failed with status: $provisioningState"
    }
}

if ($provisioningState -ne "Succeeded") {
    throw "Deployment timed out after ${maxPollTime}s"
}

# Extract outputs from the deployment
$outputs = $statusResponse.properties.outputs
Write-Host "  Infrastructure deployed successfully." -ForegroundColor Green

# Extract non-secret outputs (REST API returns .value on each output property)
$searchEndpoint = $outputs.searchEndpoint.value
$searchServiceName = $outputs.searchServiceName.value
$openAiEndpoint = $outputs.openAiEndpoint.value
$openAiResourceName = $outputs.openAiResourceName.value
$chatDeployment = $outputs.chatDeploymentName.value
$embeddingDeployment = $outputs.embeddingDeploymentName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlDatabaseName = $outputs.sqlDatabaseName.value
$storageAccountName = $outputs.storageAccountName.value

# New outputs
$keyVaultName = $outputs.keyVaultName.value
$keyVaultUri = $outputs.keyVaultUri.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value
$appInsightsName = $outputs.appInsightsName.value

$docIntelEndpoint = $outputs.documentIntelligenceEndpoint.value
$docIntelName = $outputs.documentIntelligenceName.value

Write-Host "  Outputs extracted:" -ForegroundColor DarkGray
Write-Host "    Search: $searchEndpoint" -ForegroundColor DarkGray
Write-Host "    OpenAI: $openAiEndpoint" -ForegroundColor DarkGray
Write-Host "    SQL:    $sqlServerFqdn" -ForegroundColor DarkGray
if ($docIntelEndpoint) { Write-Host "    Doc Intel: $docIntelEndpoint" -ForegroundColor DarkGray }
if ($keyVaultUri) { Write-Host "    Key Vault: $keyVaultUri" -ForegroundColor DarkGray }
if ($appInsightsName) { Write-Host "    App Insights: $appInsightsName" -ForegroundColor DarkGray }

# Container Apps outputs
$apiFqdn = $outputs.apiContainerAppFqdn.value
$mcpFqdn = $outputs.mcpContainerAppFqdn.value
$webFqdn = $outputs.webContainerAppFqdn.value
if ($apiFqdn) { Write-Host "    API App:  https://$apiFqdn" -ForegroundColor DarkGray }
if ($mcpFqdn) { Write-Host "    MCP App:  https://$mcpFqdn" -ForegroundColor DarkGray }
if ($webFqdn) { Write-Host "    Web App:  https://$webFqdn" -ForegroundColor DarkGray }

# Retrieve secrets via Azure CLI (not from Bicep outputs)
Write-Host "  Retrieving API keys via Azure CLI..." -ForegroundColor DarkGray
$searchApiKey = (az search admin-key show --service-name $searchServiceName --resource-group $ResourceGroup --query primaryKey -o tsv 2>$null)
if (-not $searchApiKey) { throw "Failed to retrieve Search admin key" }

if ($openAiResourceName -ne "external") {
    $openAiKey = (az cognitiveservices account keys list --name $openAiResourceName --resource-group $ResourceGroup --query key1 -o tsv 2>$null)
    if (-not $openAiKey) { throw "Failed to retrieve OpenAI key" }
} else {
    $openAiKey = $env:SH_OPENAI_KEY
    if (-not $openAiKey) { throw "External OpenAI key required: set SH_OPENAI_KEY environment variable" }
}

$storageKeys = (az storage account keys list --account-name $storageAccountName --resource-group $ResourceGroup --query "[0].value" -o tsv 2>$null)
if (-not $storageKeys) { throw "Failed to retrieve Storage account key" }

if ($docIntelName -ne "external" -and $docIntelName) {
    $docIntelKey = (az cognitiveservices account keys list --name $docIntelName --resource-group $ResourceGroup --query key1 -o tsv 2>$null)
    if (-not $docIntelKey) { Write-Host "  Warning: Failed to retrieve Document Intelligence key" -ForegroundColor DarkYellow }
}

# Reconstruct connection strings from non-secret outputs + retrieved secrets
$sqlConnectionString = "Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=sqladmin;Password=${SqlPassword};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$storageConnection = "DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net;AccountName=${storageAccountName};AccountKey=${storageKeys}"

Write-Host "  Secrets retrieved and connection strings built." -ForegroundColor Green

# Step 3: Verify Key Vault (if deployed)
if ($keyVaultName) {
    Write-Host "[3/7] Verifying Key Vault secrets..." -ForegroundColor Yellow

    # Wait briefly for RBAC propagation (Key Vault Secrets User role)
    Start-Sleep -Seconds 10

    # Verify by reading one secret
    $kvTestSecret = az keyvault secret show `
        --vault-name $keyVaultName `
        --name "AzureAISearch--Endpoint" `
        --query value -o tsv 2>$null

    if ($kvTestSecret -eq $searchEndpoint) {
        Write-Host "  Key Vault secrets verified (7 secrets populated)." -ForegroundColor Green
    } else {
        Write-Host "  Warning: Key Vault secret verification failed. RBAC may still be propagating." -ForegroundColor DarkYellow
        Write-Host "  Secrets were populated by Bicep. Retry in a few minutes if needed." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "[3/7] Key Vault not deployed (skipped)." -ForegroundColor DarkGray
}

# Step 4: Create search index
if (-not $SkipSearchIndex) {
    Write-Host "[4/7] Creating search index 'knowledge'..." -ForegroundColor Yellow
    $indexBody = @{
        name = "knowledge"
        fields = @(
            @{ name = "id"; type = "Edm.String"; key = $true; filterable = $true }
            @{ name = "tenantId"; type = "Edm.String"; filterable = $true }
            @{ name = "knowledgeId"; type = "Edm.String"; filterable = $true }
            @{ name = "title"; type = "Edm.String"; searchable = $true }
            @{ name = "content"; type = "Edm.String"; searchable = $true }
            @{ name = "aiSummary"; type = "Edm.String"; searchable = $true }
            @{ name = "vaultName"; type = "Edm.String"; searchable = $true; filterable = $true }
            @{ name = "vaultGuidId"; type = "Edm.String"; filterable = $true }
            @{ name = "ancestorVaultIds"; type = "Collection(Edm.String)"; filterable = $true }
            @{ name = "topicName"; type = "Edm.String"; searchable = $true; filterable = $true }
            @{ name = "tags"; type = "Collection(Edm.String)"; searchable = $true; filterable = $true }
            @{ name = "knowledgeType"; type = "Edm.String"; filterable = $true }
            @{ name = "filePath"; type = "Edm.String"; searchable = $true }
            @{ name = "createdAt"; type = "Edm.DateTimeOffset"; filterable = $true; sortable = $true }
            @{
                name = "contentVector"
                type = "Collection(Edm.Single)"
                searchable = $true
                dimensions = 1536
                vectorSearchProfile = "default-profile"
            }
        )
        vectorSearch = @{
            algorithms = @(
                @{
                    name = "hnsw-algo"
                    kind = "hnsw"
                    hnswParameters = @{
                        m = 4
                        efConstruction = 400
                        efSearch = 500
                        metric = "cosine"
                    }
                }
            )
            profiles = @(
                @{
                    name = "default-profile"
                    algorithm = "hnsw-algo"
                }
            )
        }
    } | ConvertTo-Json -Depth 6

    $indexResponse = Invoke-RestMethod `
        -Uri "$searchEndpoint/indexes/knowledge?api-version=2024-07-01" `
        -Method Put `
        -Headers @{ "api-key" = $searchApiKey; "Content-Type" = "application/json" } `
        -Body $indexBody `
        -ErrorAction SilentlyContinue

    if ($indexResponse.name -eq "knowledge") {
        Write-Host "  Search index 'knowledge' created." -ForegroundColor Green
    } else {
        Write-Host "  Warning: Search index creation may have failed. Check manually." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "[4/7] Skipping search index creation." -ForegroundColor DarkGray
}

# Step 5: Apply EF migrations
if (-not $SkipMigration) {
    Write-Host "[5/7] Applying EF Core migrations..." -ForegroundColor Yellow
    $infraProject = Join-Path $repoRoot "src\Knowz.SelfHosted.Infrastructure\Knowz.SelfHosted.Infrastructure.csproj"
    $apiProject = Join-Path $repoRoot "src\Knowz.SelfHosted.API\Knowz.SelfHosted.API.csproj"

    dotnet ef database update `
        --project $infraProject `
        --startup-project $apiProject `
        --context SelfHostedDbContext `
        --connection $sqlConnectionString 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Warning: EF migration may have failed. Check output above." -ForegroundColor DarkYellow
    } else {
        Write-Host "  Migrations applied successfully." -ForegroundColor Green
    }
} else {
    Write-Host "[5/7] Skipping EF migration." -ForegroundColor DarkGray
}

# Step 6: Generate appsettings.Local.json
Write-Host "[6/7] Generating appsettings.Local.json..." -ForegroundColor Yellow
$apiSettingsPath = Join-Path $repoRoot "src\Knowz.SelfHosted.API\appsettings.Local.json"
$mcpSettingsPath = Join-Path $repoRoot "src\Knowz.MCP\appsettings.Local.json"

# Reuse Container Apps API key for local settings if deploying Container Apps, otherwise generate new
if ($DeployContainerApps -and $ApiKeyOverride) {
    $apiKey = $ApiKeyOverride
} else {
    $apiKey = "sh-" + [guid]::NewGuid().ToString("N").Substring(0, 24)
}

$localSettings = @{
    ConnectionStrings = @{ McpDb = $sqlConnectionString }
    AzureAISearch = @{
        Endpoint = $searchEndpoint
        ApiKey = $searchApiKey
        IndexName = "knowledge"
    }
    AzureOpenAI = @{
        Endpoint = $openAiEndpoint
        ApiKey = $openAiKey
        DeploymentName = $chatDeployment
        EmbeddingDeploymentName = $embeddingDeployment
    }
    SelfHosted = @{
        TenantId = "00000000-0000-0000-0000-000000000001"
        ServerName = "knowz-selfhosted-api"
        ApiKey = $apiKey
        JwtSecret = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object { [char]$_ })
        SuperAdminUsername = "admin"
        EnableSwagger = $true
    }
    Logging = @{
        LogLevel = @{
            Default = "Debug"
            "Microsoft.AspNetCore" = "Information"
            "Microsoft.EntityFrameworkCore" = "Information"
        }
    }
}

# Add AzureKeyVault section (Enabled=false by default for local dev)
if ($keyVaultUri) {
    $localSettings.AzureKeyVault = @{
        Enabled = $false  # Set to true to read secrets from Key Vault instead of inline values
        VaultUri = $keyVaultUri
    }
}

# Add ApplicationInsights section
if ($appInsightsConnectionString) {
    $localSettings.ApplicationInsights = @{
        ConnectionString = $appInsightsConnectionString
    }
}

# Add Document Intelligence section
if ($docIntelEndpoint) {
    $localSettings.AzureDocumentIntelligence = @{
        Endpoint = $docIntelEndpoint
        ApiKey = $docIntelKey
    }
}

# Add Storage section with Azure connection string
if ($storageConnection) {
    $localSettings.Storage = @{
        Provider = "AzureBlob"
        Azure = @{
            ConnectionString = $storageConnection
            ContainerName = "selfhosted-files"
        }
    }
}

$localSettingsJson = $localSettings | ConvertTo-Json -Depth 4
$localSettingsJson | Set-Content -Path $apiSettingsPath -Encoding UTF8
Write-Host "  Written: $apiSettingsPath" -ForegroundColor Green

# MCP settings (same connection info)
$mcpSettings = @{
    ConnectionStrings = @{ McpDb = $sqlConnectionString }
    AzureAISearch = @{
        Endpoint = $searchEndpoint
        ApiKey = $searchApiKey
        IndexName = "knowledge"
    }
    AzureOpenAI = @{
        Endpoint = $openAiEndpoint
        ApiKey = $openAiKey
        DeploymentName = $chatDeployment
        EmbeddingDeploymentName = $embeddingDeployment
    }
    SelfHosted = @{
        TenantId = "00000000-0000-0000-0000-000000000001"
        ServerName = "knowz-mcp-selfhosted"
        ApiKey = $apiKey
    }
    Logging = @{
        LogLevel = @{
            Default = "Debug"
            "Microsoft.AspNetCore" = "Information"
        }
    }
}

# Add AzureKeyVault section to MCP settings too
if ($keyVaultUri) {
    $mcpSettings.AzureKeyVault = @{
        Enabled = $false
        VaultUri = $keyVaultUri
    }
}

$mcpSettingsJson = $mcpSettings | ConvertTo-Json -Depth 4
$mcpSettingsJson | Set-Content -Path $mcpSettingsPath -Encoding UTF8
Write-Host "  Written: $mcpSettingsPath" -ForegroundColor Green

# Step 7: Summary
Write-Host "[7/7] Deployment complete!" -ForegroundColor Yellow
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Deployment Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resources:" -ForegroundColor White
Write-Host "  SQL Server:     $sqlServerFqdn"
Write-Host "  OpenAI:         $openAiEndpoint"
Write-Host "  AI Search:      $searchEndpoint"
Write-Host "  Storage:        $storageAccountName"
if ($docIntelEndpoint) {
    Write-Host "  Doc Intel:      $docIntelEndpoint" -ForegroundColor White
}
if ($keyVaultName) {
    Write-Host "  Key Vault:      $keyVaultUri" -ForegroundColor White
}
if ($appInsightsName) {
    Write-Host "  App Insights:   $appInsightsName" -ForegroundColor White
}
Write-Host ""
Write-Host "API Key:          $apiKey" -ForegroundColor Yellow
Write-Host ""

# Key Vault instructions
if ($keyVaultUri) {
    Write-Host "Key Vault:" -ForegroundColor White
    Write-Host "  Secrets are pre-populated in Key Vault." -ForegroundColor DarkGray
    Write-Host "  To enable KV-based config, set in appsettings.Local.json:" -ForegroundColor DarkGray
    Write-Host "    `"AzureKeyVault`": { `"Enabled`": true, `"VaultUri`": `"$keyVaultUri`" }" -ForegroundColor DarkGray
    Write-Host "  Ensure you are logged in via 'az login' for DefaultAzureCredential." -ForegroundColor DarkGray
    Write-Host ""
}

# Monitoring instructions
if ($appInsightsConnectionString) {
    Write-Host "Monitoring:" -ForegroundColor White
    Write-Host "  App Insights connection string is in appsettings.Local.json." -ForegroundColor DarkGray
    Write-Host "  Key Vault audit logs flow to Log Analytics workspace." -ForegroundColor DarkGray
    Write-Host ""
}

# Storage hardening notice
if ($DisableStorageSharedKey) {
    Write-Host "Storage:" -ForegroundColor White
    Write-Host "  Shared key access DISABLED. Use Managed Identity for all blob operations." -ForegroundColor DarkYellow
    Write-Host ""
}

# Container Apps summary
if ($apiFqdn) {
    Write-Host "Container Apps:" -ForegroundColor White
    Write-Host "  API:  https://$apiFqdn" -ForegroundColor Green
    Write-Host "  MCP:  https://$mcpFqdn" -ForegroundColor Green
    Write-Host "  Web:  https://$webFqdn" -ForegroundColor Green
    Write-Host "  API Key:     $ApiKeyOverride" -ForegroundColor Yellow
    Write-Host "  Admin:       admin / $AdminPassword" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Run locally:" -ForegroundColor White
Write-Host "  API:  cd src\Knowz.SelfHosted.API && dotnet run --urls http://localhost:5000"
Write-Host "  Web:  cd src\knowz-selfhosted-web && npm run dev"
Write-Host "  MCP:  cd src\Knowz.MCP && dotnet run --urls http://localhost:8080"
Write-Host ""
Write-Host "Run tests:" -ForegroundColor White
Write-Host "  dotnet test src\Knowz.SelfHosted.Tests --filter Category=Integration"
Write-Host ""
