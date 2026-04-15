<#
.SYNOPSIS
    Sets up local development for Knowz Self-Hosted by pulling secrets from the rg-knowz-sh Key Vault.

.DESCRIPTION
    Pulls all secrets from Key Vault 'knowzshkvfdda9c35' (subscription ca048e18-501f-4243-b3e6-3fca82449226,
    resource group rg-knowz-sh) and stores them as user-secrets on the Aspire AppHost project.

    After running this script, you can use any dev mode:
      dotnet run --project selfhosted/src/Knowz.SelfHosted.AppHost --launch-profile cloud
      dotnet run --project selfhosted/src/Knowz.SelfHosted.AppHost --launch-profile local
      INFRA_MODE=cloud dotnet run --project selfhosted/src/Knowz.SelfHosted.AppHost

    For UI-only mode (no local API):
      cd selfhosted/src/knowz-selfhosted-web && npm run dev:cloud

.PARAMETER Force
    Overwrite existing user-secrets without prompting.

.PARAMETER KeyVaultName
    Key Vault name. Defaults to 'knowzshkvfdda9c35' (rg-knowz-sh test environment).

.PARAMETER Subscription
    Azure subscription ID. Defaults to ca048e18-501f-4243-b3e6-3fca82449226.
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [string]$KeyVaultName = "knowzshkvfdda9c35",
    [string]$Subscription = "ca048e18-501f-4243-b3e6-3fca82449226"
)

$ErrorActionPreference = "Stop"

function Write-Info    { param($m) Write-Host "[INFO] $m" -ForegroundColor Cyan }
function Write-Ok      { param($m) Write-Host "[OK]   $m" -ForegroundColor Green }
function Write-Warn    { param($m) Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Write-Err     { param($m) Write-Host "[ERROR] $m" -ForegroundColor Red }

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot   = Split-Path -Parent $scriptDir   # selfhosted/
$appHostDir = Join-Path $repoRoot "src\Knowz.SelfHosted.AppHost"
$appHostProj = Join-Path $appHostDir "Knowz.SelfHosted.AppHost.csproj"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "  Knowz Self-Hosted Dev Setup (rg-knowz-sh)" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host ""

# ── Prerequisites ──────────────────────────────────────────────────────────
Write-Info "Checking prerequisites..."

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Err "Azure CLI not found. Install from https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
}
Write-Ok "Azure CLI found"

$account = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Err "Not logged in. Run: az login"
    exit 1
}
$accountInfo = $account | ConvertFrom-Json
Write-Ok "Logged in as: $($accountInfo.user.name)"

# Set subscription
az account set --subscription $Subscription 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Err "Could not set subscription $Subscription"
    exit 1
}
Write-Ok "Using subscription: $Subscription"

# Check KV access
Write-Info "Checking Key Vault access to '$KeyVaultName'..."
$kvTest = az keyvault secret list --vault-name $KeyVaultName --query "[0].name" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Err "Cannot access Key Vault '$KeyVaultName'."
    Write-Err "Grant yourself 'Key Vault Secrets User' role on the vault, then retry."
    exit 1
}
Write-Ok "Key Vault access verified"
Write-Host ""

# ── Secret mappings: KV name -> AppHost user-secret key ──────────────────
# The AppHost reads these from user-secrets and injects them as env vars into the API.
# Key format matches the nested config path used in Program.cs.
$secretMappings = [ordered]@{
    # Database
    "ConnectionStrings--McpDb"              = "ConnectionStrings:McpDb"

    # Storage
    "Storage--Azure--ConnectionString"      = "Storage:Azure:ConnectionString"

    # Auth
    "SelfHosted--JwtSecret"                 = "SelfHosted:JwtSecret"
    "SelfHosted--SuperAdminPassword"        = "SelfHosted:SuperAdminPassword"
    "SelfHosted--ApiKey"                    = "SelfHosted:ApiKey"

    # AI: Azure OpenAI
    "AzureOpenAI--Endpoint"                 = "AzureOpenAI:Endpoint"
    "AzureOpenAI--ApiKey"                   = "AzureOpenAI:ApiKey"
    "AzureOpenAI--DeploymentName"           = "AzureOpenAI:DeploymentName"
    "AzureOpenAI--EmbeddingDeploymentName"  = "AzureOpenAI:EmbeddingDeploymentName"

    # AI: Azure AI Search
    "AzureAISearch--Endpoint"               = "AzureAISearch:Endpoint"
    "AzureAISearch--ApiKey"                 = "AzureAISearch:ApiKey"

    # AI: Azure AI Vision
    "AzureAIVision--Endpoint"               = "AzureAIVision:Endpoint"
    "AzureAIVision--ApiKey"                 = "AzureAIVision:ApiKey"

    # AI: Document Intelligence
    "AzureDocumentIntelligence--Endpoint"   = "AzureDocumentIntelligence:Endpoint"
    "AzureDocumentIntelligence--ApiKey"     = "AzureDocumentIntelligence:ApiKey"

    # Monitoring
    "ApplicationInsights--ConnectionString" = "ApplicationInsights:ConnectionString"
}

# ── Pull secrets ───────────────────────────────────────────────────────────
Write-Info "Pulling secrets from Key Vault '$KeyVaultName'..."
$fetched  = @{}
$skipped  = @()
$failed   = @()

foreach ($kvName in $secretMappings.Keys) {
    $userSecretKey = $secretMappings[$kvName]
    $ErrorActionPreference = "SilentlyContinue"
    $value = az keyvault secret show --vault-name $KeyVaultName --name $kvName --query value -o tsv 2>&1
    $ok = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"

    if ($ok -and $value -and $value -notmatch "^ERROR:") {
        $fetched[$userSecretKey] = $value
        $masked = if ($value.Length -gt 8) { $value.Substring(0,4) + "****" + $value.Substring($value.Length-4) } else { "****" }
        Write-Host "  [+] $kvName -> $userSecretKey" -ForegroundColor Gray
    } else {
        $skipped += $kvName
        Write-Host "  [-] $kvName (not found, skipping)" -ForegroundColor DarkGray
    }
}

Write-Host ""

if ($fetched.Count -eq 0) {
    Write-Err "No secrets were retrieved. Check Key Vault access and secret names."
    exit 1
}

# ── Write user-secrets to AppHost project ─────────────────────────────────
Write-Info "Writing user-secrets to AppHost project..."

if (-not (Test-Path $appHostProj)) {
    Write-Err "AppHost project not found at: $appHostProj"
    exit 1
}

# Initialize user-secrets (idempotent)
dotnet user-secrets init --project $appHostProj 2>&1 | Out-Null

foreach ($secretKey in $fetched.Keys) {
    $result = dotnet user-secrets set $secretKey $fetched[$secretKey] --project $appHostProj 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [+] $secretKey" -ForegroundColor Gray
    } else {
        $failed += $secretKey
        Write-Warn "  [-] Failed to set: $secretKey"
    }
}

# Fallback: if deployment names weren't in KV, auto-detect from the Azure OpenAI resource
if (-not $fetched.ContainsKey("AzureOpenAI:DeploymentName") -or -not $fetched.ContainsKey("AzureOpenAI:EmbeddingDeploymentName")) {
    $endpoint = $fetched["AzureOpenAI:Endpoint"]
    if ($endpoint) {
        # Parse resource name from endpoint (https://{name}.openai.azure.com/)
        $resourceName = ($endpoint -replace 'https?://','' -split '\.')[0]
        Write-Info "Auto-detecting deployments from Azure OpenAI resource '$resourceName'..."
        $ErrorActionPreference = "SilentlyContinue"
        $deployments = az cognitiveservices account deployment list --name $resourceName --resource-group "rg-knowz-sh" --query "[].{name:name, model:properties.model.name}" -o json 2>$null | ConvertFrom-Json
        $ErrorActionPreference = "Stop"
        if ($deployments) {
            if (-not $fetched.ContainsKey("AzureOpenAI:DeploymentName")) {
                $chatDep = $deployments | Where-Object { $_.model -notmatch 'embedding' } | Select-Object -First 1
                if ($chatDep) {
                    $fetched["AzureOpenAI:DeploymentName"] = $chatDep.name
                    dotnet user-secrets set "AzureOpenAI:DeploymentName" $chatDep.name --project $appHostProj 2>&1 | Out-Null
                    Write-Host "  [+] AzureOpenAI:DeploymentName (auto-detected: $($chatDep.name))" -ForegroundColor Gray
                }
            }
            if (-not $fetched.ContainsKey("AzureOpenAI:EmbeddingDeploymentName")) {
                $embDep = $deployments | Where-Object { $_.model -match 'embedding' } | Select-Object -First 1
                if ($embDep) {
                    $fetched["AzureOpenAI:EmbeddingDeploymentName"] = $embDep.name
                    dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" $embDep.name --project $appHostProj 2>&1 | Out-Null
                    Write-Host "  [+] AzureOpenAI:EmbeddingDeploymentName (auto-detected: $($embDep.name))" -ForegroundColor Gray
                }
            }
        }
    }
}

# Storage container is always the same convention
dotnet user-secrets set "Storage:Azure:ContainerName" "selfhosted-files" --project $appHostProj 2>&1 | Out-Null
Write-Host "  [+] Storage:Azure:ContainerName (selfhosted-files)" -ForegroundColor Gray

Write-Host ""

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "  Setup Complete!" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  Configured : $($fetched.Count + 3) user-secrets" -ForegroundColor Green
if ($skipped.Count -gt 0) {
    Write-Host "  Skipped    : $($skipped.Count) secrets (not in KV)" -ForegroundColor Yellow
}
if ($failed.Count -gt 0) {
    Write-Host "  Failed     : $($failed.Count) secrets" -ForegroundColor Red
}
Write-Host ""
Write-Host "Dev modes:" -ForegroundColor White
Write-Host ""
Write-Host "  Full Aspire - cloud infra (no Docker):" -ForegroundColor Cyan
Write-Host "    dotnet run --project selfhosted/src/Knowz.SelfHosted.AppHost --launch-profile cloud" -ForegroundColor Gray
Write-Host ""
Write-Host "  Full Aspire - local SQL container:" -ForegroundColor Cyan
Write-Host "    dotnet run --project selfhosted/src/Knowz.SelfHosted.AppHost --launch-profile local" -ForegroundColor Gray
Write-Host ""
Write-Host "  UI only - web client proxies to deployed API (no local API or containers):" -ForegroundColor Cyan
Write-Host "    cd selfhosted/src/knowz-selfhosted-web" -ForegroundColor Gray
Write-Host "    npm run dev:cloud" -ForegroundColor Gray
Write-Host ""
Write-Host "  Deployed endpoints (rg-knowz-sh):" -ForegroundColor White
$apiUrl = "https://knowz-sh-api.jollymeadow-44a9327c.eastus2.azurecontainerapps.io"
$webUrl = "https://knowz-sh-web.jollymeadow-44a9327c.eastus2.azurecontainerapps.io"
$mcpUrl = "https://knowz-sh-mcp.jollymeadow-44a9327c.eastus2.azurecontainerapps.io"
Write-Host "    API:  $apiUrl" -ForegroundColor Gray
Write-Host "    Web:  $webUrl" -ForegroundColor Gray
Write-Host "    MCP:  $mcpUrl" -ForegroundColor Gray
Write-Host ""
