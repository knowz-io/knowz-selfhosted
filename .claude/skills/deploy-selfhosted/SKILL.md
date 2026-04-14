---
name: deploy-selfhosted
description: "Deploy Knowz self-hosted to Azure — guides infrastructure provisioning, AI service configuration, and post-deployment verification. Use when the user wants to deploy or set up a new self-hosted instance."
user-invocable: true
allowed-tools: Read, Write, Bash, Glob, Grep, AskUserQuestion
argument-hint: "[--terraform|--bicep] [--resource-group=NAME] [--location=REGION]"
---

# Deploy Knowz Self-Hosted to Azure

A guided deployment of Knowz self-hosted to Azure. Covers pre-flight checks (Azure CLI, subscription, providers), interactive configuration (AI services with 3 modes: Deploy New / Use Existing / External), deployment execution (Terraform or Bicep), post-deployment verification, targeted remediation, and a final summary.

**Usage**: `/deploy-selfhosted [--terraform|--bicep] [--resource-group=NAME] [--location=REGION]`

**Examples**:
- `/deploy-selfhosted` — Full interactive deployment
- `/deploy-selfhosted --terraform --location=eastus2` — Terraform path, location pre-set
- `/deploy-selfhosted --bicep --resource-group=my-knowz-rg` — Bicep path, RG pre-set

Parse arguments. Extract `--terraform`, `--bicep`, `--resource-group`, `--location` if provided.

---

## Phase 0: Pre-flight & Azure CLI Setup

### Step 0.1: Detect repo context

```bash
if [ -f "Knowz.SelfHosted.sln" ]; then
  REPO_ROOT="."
  REPO_TYPE="standalone"
elif [ -f "selfhosted/Knowz.SelfHosted.sln" ]; then
  REPO_ROOT="selfhosted"
  REPO_TYPE="monorepo"
else
  echo "ERROR: Not in a Knowz self-hosted directory. Clone https://github.com/knowz-io/knowz-selfhosted first."
  exit 1
fi
```

Tell the user which repo type was detected. All subsequent paths should prepend `$REPO_ROOT/`.

### Step 0.2: Azure CLI check

```bash
az version --query '"azure-cli"' -o tsv 2>/dev/null
```

If Azure CLI is not installed, guide the user based on their platform:
- **Windows**: `winget install Microsoft.AzureCLI`
- **macOS**: `brew install azure-cli`
- **Linux (Debian/Ubuntu)**: `curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash`
- **Linux (RHEL/Fedora)**: `sudo dnf install azure-cli`

After install, tell the user to run `! az login` (the `!` prefix executes in the Claude Code session).

### Step 0.3: Azure login & subscription selection

```bash
az account show --query '{name:name, id:id, tenantId:tenantId}' -o json 2>/dev/null
```

If not logged in, tell the user to run `! az login` and wait.

If logged in, display the current subscription. Then ask with `AskUserQuestion`:
- **Question**: "You're logged into subscription `{name}` ({id}). Deploy here or switch?"
- **Options**:
  - Deploy to current subscription (Recommended if correct)
  - Switch to a different subscription
  - Show all available subscriptions first

If switching, list available subs and let user pick:
```bash
az account list --query '[].{name:name, id:id, isDefault:isDefault}' -o table
```
Then: `az account set --subscription "<id>"` and re-verify.

### Step 0.4: Resource provider registration

Check and auto-register required providers:
```bash
for provider in \
  Microsoft.CognitiveServices \
  Microsoft.Search \
  Microsoft.Sql \
  Microsoft.Storage \
  Microsoft.KeyVault \
  Microsoft.Insights \
  Microsoft.OperationalInsights \
  Microsoft.App \
  Microsoft.ManagedIdentity; do
  state=$(az provider show --namespace $provider --query registrationState -o tsv 2>/dev/null)
  if [ "$state" != "Registered" ]; then
    echo "Registering $provider..."
    az provider register --namespace $provider --wait
  fi
done
```

### Step 0.5: Deployment tool selection

If `--terraform` was in args: check `terraform --version`. If missing, direct user to install.
If `--bicep` was in args: check `az bicep version`. Auto-install if missing: `az bicep install`.
If neither was specified, ask the user with `AskUserQuestion`:
- **Question**: "Which deployment tool should we use?"
- **Options**:
  - Terraform (Recommended — more robust state management)
  - Bicep (Native Azure ARM — simpler if no Terraform installed)

### Step 0.6: Soft-delete conflict scan

Before any configuration, scan for soft-deleted Cognitive Services and Key Vaults that could cause naming conflicts:
```bash
az cognitiveservices account list-deleted -o json 2>/dev/null
az keyvault list-deleted -o json 2>/dev/null
```

If conflicts found matching the intended prefix/location, warn the user and offer to purge via `AskUserQuestion`:
- **Options**: Purge now / Pick different prefix / Cancel

---

## Phase 1: Configuration (Interactive)

### Step 1.1: Resource Group & Location

If `--resource-group` was provided in args, use it. Otherwise ask with `AskUserQuestion`:
- **Question**: "Where should we deploy?"
- **Options**:
  - Create new resource group (Recommended)
  - Use existing resource group
  - Other

If new: ask name (default: `rg-knowz-selfhosted`) and location (default from `--location` or `eastus2`).
If existing: list with `az group list --query '[].{name:name, location:location}' -o table` and let user pick.

Validate the location supports Cognitive Services:
```bash
az provider show --namespace Microsoft.CognitiveServices \
  --query "resourceTypes[?resourceType=='accounts'].locations" -o json
```

### Step 1.2: Resource prefix

Default: `knowz-sh`. Validate: 2-8 characters, lowercase alphanumeric + hyphens, starts with letter, ends with letter or digit. Regex: `^[a-z][a-z0-9-]{0,6}[a-z0-9]$`.

### Step 1.3: AI Services Configuration

For **each** of Azure OpenAI, Azure AI Vision, and Document Intelligence, ask with `AskUserQuestion`:
- **Question**: "How should {service} be configured?"
- **Options**:
  - Deploy New — Creates a new resource in your subscription
  - Use Existing Azure Resource — Reference one you already have
  - Skip — Disable this AI feature

**If "Use Existing"**, discover existing resources:

For OpenAI:
```bash
az cognitiveservices account list \
  --query "[?kind=='OpenAI' || kind=='AIServices'].{name:name, kind:kind, rg:resourceGroup, location:location, endpoint:properties.endpoint}" \
  -o table
```

For Vision:
```bash
az cognitiveservices account list \
  --query "[?kind=='ComputerVision'].{name:name, rg:resourceGroup, location:location, endpoint:properties.endpoint}" \
  -o table
```

For Doc Intelligence:
```bash
az cognitiveservices account list \
  --query "[?kind=='FormRecognizer'].{name:name, rg:resourceGroup, location:location, endpoint:properties.endpoint}" \
  -o table
```

Present the list to the user. If multiple, use `AskUserQuestion` to select. Store `{name, resourceGroup}` for each selected existing resource.

**For OpenAI "Use Existing"**, verify the required deployments exist:
```bash
az cognitiveservices account deployment list \
  --name $OPENAI_NAME \
  --resource-group $OPENAI_RG \
  --query "[].{name:name, model:properties.model.name}" -o table
```

If `gpt-5.2-chat` and `text-embedding-3-small` aren't found, warn the user and ask which deployments to use.

**For "Deploy New"**, attempt a quota pre-check (best-effort):
```bash
az cognitiveservices usage list \
  --location $LOCATION \
  --query "[?contains(name.value, 'OpenAI')].{name:name.value, current:currentValue, limit:limit}" \
  -o table 2>/dev/null
```

If the API returns quota data and it's insufficient, offer fallback via `AskUserQuestion`:
- **Options**: Switch to Use Existing / Try different region / Skip OpenAI

### Step 1.4: Optional features

Ask with `AskUserQuestion` (multi-select):
- Key Vault (Recommended)
- Monitoring — App Insights + Log Analytics (Recommended)
- Container Apps — API, Web, MCP as Azure Container Apps

### Step 1.5: Credentials

- **SQL admin password**: Ask user. Validate: 12-128 chars with uppercase, lowercase, digit, special char. Regex: `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{12,128}$`
- **SuperAdmin password** (for Web UI): Ask user. Validate: 8+ chars similar complexity.
- **API key + JWT secret** (if Container Apps): Auto-generate, display once, recommend saving.

### Step 1.6: Confirmation

Display the full deployment plan:

```
═══════════════════════════════════════════════
  Knowz Self-Hosted Deployment Plan
═══════════════════════════════════════════════
  Resource Group:   rg-knowz-selfhosted
  Location:         eastus2
  Prefix:           knowz-sh
  Deploy Mode:      Terraform
  
  AI Services:
    OpenAI:         Deploy New (gpt-5.2-chat + text-embedding-3-small)
    Vision:         Use Existing (knowz-vision in rg-knowz-shared)
    Doc Intel:      Deploy New
  
  Optional:
    Key Vault:      Yes
    Monitoring:     Yes
    Container Apps: Yes (image: 0.9.0)
  
  Estimated Cost:   ~$90-100/month at basic SKUs
═══════════════════════════════════════════════
```

Confirm with `AskUserQuestion`:
- **Options**: Proceed with deployment / Go back and change / Cancel

---

## Phase 2: AI Service Resolution

For each "Use Existing" selection, retrieve and validate the key:
```bash
KEY=$(az cognitiveservices account keys list \
  --name $NAME --resource-group $RG --query key1 -o tsv)
```

If key retrieval fails, tell the user they may need Cognitive Services User role on that resource and offer to cancel.

Test connectivity (optional but recommended):
```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "api-key: $KEY" \
  "$ENDPOINT/openai/deployments?api-version=2024-06-01"
```

Expect 200.

---

## Phase 3: Deployment Execution

### Step 3.1: Generate deployment config

**For Terraform** — write `$REPO_ROOT/terraform/standard/terraform.auto.tfvars`:
```hcl
sql_admin_password       = "..."
admin_password           = "..."
resource_group_name      = "..."
location                 = "..."
prefix                   = "..."
deploy_openai            = false
existing_openai_resource_name = "knowz-openai"
existing_openai_resource_group = "rg-knowz-shared"
deploy_vision            = true
deploy_document_intelligence = true
deploy_key_vault         = true
deploy_monitoring        = true
deploy_container_apps    = true
image_tag                = "0.9.0"
allow_all_ips            = false
```

**For Bicep** — construct parameter overrides for `az deployment group create` OR call the deploy script directly:
```bash
cd $REPO_ROOT/infrastructure
./selfhosted-deploy.ps1 \
  -SqlPassword "..." \
  -AdminPassword "..." \
  -ResourceGroup "..." \
  -Location "..." \
  -Prefix "..." \
  -ExistingOpenAiName "knowz-openai" \
  -ExistingOpenAiResourceGroup "rg-knowz-shared" \
  -DeployContainerApps \
  -ImageTag "0.9.0"
```

### Step 3.2: Terraform workspace isolation

```bash
cd $REPO_ROOT/terraform/standard
WORKSPACE=$(echo $RESOURCE_GROUP | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9-]/-/g')
terraform workspace new $WORKSPACE 2>/dev/null || terraform workspace select $WORKSPACE
```

### Step 3.3: Execute deployment

**Terraform**:
```bash
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

**Bicep** (via deploy script — already polls for completion).

### Step 3.4: Progress reporting

Stream user-friendly progress (filter raw TF/Bicep output):

```
[1/8] Creating resource group...              ✓
[2/8] Deploying Azure AI Search...            ✓ (knowz-sh-search-eastus2)
[3/8] Deploying Azure OpenAI + models...      ✓ (3 deployments)
[4/8] Deploying SQL Server + database...      ✓ (McpKnowledge)
[5/8] Deploying Storage + blob container...   ✓
[6/8] Deploying Key Vault + secrets...        ✓ (12 secrets)
[7/8] Deploying monitoring stack...           ✓
[8/8] Deploying Container Apps...             ✓ (API + MCP + Web)
```

### Step 3.5: Failure handling

- **Quota exhaustion** (`InsufficientQuota`): Offer "Use Existing" fallback (restart Phase 1.3 for the affected service), then retry from here.
- **RBAC errors** (Key Vault 403): Auto-assign `Key Vault Secrets Officer` role:
  ```bash
  USER_OID=$(az ad signed-in-user show --query id -o tsv)
  KV_ID=$(az keyvault show --name $KV_NAME --resource-group $RG --query id -o tsv)
  az role assignment create \
    --role "Key Vault Secrets Officer" \
    --assignee-object-id $USER_OID \
    --assignee-principal-type User \
    --scope $KV_ID
  ```
  Wait 30s, retry.
- **Soft-delete conflict**: Purge and retry:
  ```bash
  az cognitiveservices account purge --name X --location Y --resource-group Z
  # Poll until removed from list-deleted
  ```
- **Naming conflict** (custom subdomain taken): Suggest different prefix.
- **Partial failure**: Show what succeeded; offer retry of failed resources only.

---

## Phase 4: Post-Deployment Verification

### Step 4.1: Wait for Container App readiness

Container Apps deploy with `min_replicas=0` — cold start can take 30-60 seconds on first request.

```bash
for app in ${PREFIX}-api ${PREFIX}-mcp ${PREFIX}-web; do
  echo "Waiting for $app..."
  for i in {1..30}; do
    status=$(az containerapp show --name $app -g $RG \
      --query 'properties.runningStatus' -o tsv 2>/dev/null)
    [ "$status" = "Running" ] && break
    sleep 10
  done
done
```

### Step 4.2: DNS propagation wait

```bash
API_FQDN=$(az containerapp show --name ${PREFIX}-api -g $RG --query 'properties.configuration.ingress.fqdn' -o tsv)
for i in {1..12}; do
  if nslookup $API_FQDN >/dev/null 2>&1; then break; fi
  sleep 10
done
```

### Step 4.3: Health checks

```bash
curl -sfS "https://${API_FQDN}/healthz" && echo "✓ API healthy"
curl -sfS "https://${WEB_FQDN}" >/dev/null && echo "✓ Web UI accessible"
curl -sfS "https://${MCP_FQDN}/healthz" && echo "✓ MCP healthy"
```

### Step 4.4: AI connectivity test

Log in via the API and check the config endpoint:
```bash
TOKEN=$(curl -sfS "https://${API_FQDN}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"'$ADMIN_PASSWORD'"}' | jq -r '.token')

curl -sfS "https://${API_FQDN}/api/v1/config" \
  -H "Authorization: Bearer $TOKEN" | jq '.aiServices'
```

### Step 4.5: MCP service key sync check

```bash
API_MCP_KEY=$(az containerapp show --name ${PREFIX}-api -g $RG \
  --query "properties.template.containers[0].env[?name=='MCP__ServiceKey'].value | [0]" -o tsv)
MCP_MCP_KEY=$(az containerapp show --name ${PREFIX}-mcp -g $RG \
  --query "properties.template.containers[0].env[?name=='MCP__ServiceKey'].value | [0]" -o tsv)
[ "$API_MCP_KEY" = "$MCP_MCP_KEY" ] && echo "✓ MCP keys match" || echo "✗ MCP key mismatch"
```

### Step 4.6: Search index verification

```bash
SEARCH_NAME=$(terraform output -raw search_service_name 2>/dev/null || echo "${PREFIX}-search-${LOCATION}")
SEARCH_KEY=$(az search admin-key show --service-name $SEARCH_NAME -g $RG --query primaryKey -o tsv)
curl -sfS "https://${SEARCH_NAME}.search.windows.net/indexes/knowledge?api-version=2024-07-01" \
  -H "api-key: $SEARCH_KEY" | jq '.name'
```

### Step 4.7: Status matrix

```
═══════════════════════════════════════════════════════════════════
  Deployment Verification Report
═══════════════════════════════════════════════════════════════════
  Service              Status      Endpoint
  ─────────────────────────────────────────────────────────────────
  API                  ✓ Healthy   https://knowz-sh-api.xxx.eastus2...
  Web UI               ✓ Healthy   https://knowz-sh-web.xxx.eastus2...
  MCP Server           ✓ Healthy   https://knowz-sh-mcp.xxx.eastus2...
  Azure OpenAI         ✓ Connected gpt-5.2-chat + text-embedding-3-small
  AI Search            ✓ Indexed   knowledge (HNSW, 1536 dims)
  Azure AI Vision      ✓ Connected S1
  Document Intelligence ✓ Connected S0
  SQL Database         ✓ Migrated  McpKnowledge
  Storage              ✓ Ready     selfhosted-files
  Key Vault            ✓ Populated 12 secrets
  App Insights         ✓ Logging   Connected
  MCP Key Sync         ✓ Matched   API ↔ MCP
═══════════════════════════════════════════════════════════════════
```

---

## Phase 5: Remediation (if any checks failed)

For each failed check, offer targeted automated fix:

| Failure | Automated Fix |
|---------|---------------|
| API/Web/MCP not healthy | `az containerapp logs show --name X -g $RG --tail 100` to diagnose; offer restart |
| OpenAI not connected | Retrieve real key, update secret, restart API: `az containerapp secret set --name X --secrets openai-apikey=$REAL_KEY` then restart revision |
| Search index missing | Re-create using schema from `selfhosted-deploy.ps1` (1536-dim HNSW) |
| MCP key mismatch | Copy API's `MCP__ServiceKey` to MCP container env vars |
| KV secrets empty | Re-populate from Terraform/Bicep outputs; check RBAC |
| DB not migrated | Verify `Database__AutoMigrate=true`; trigger API container restart |

After each fix, re-run the specific verification check from Phase 4.

---

## Phase 6: Summary & Next Steps

### Step 6.1: Deployment summary

Display all endpoints, credentials, and configuration:
```
Deployment Complete

  Web UI:    https://knowz-sh-web.xxx.eastus2.azurecontainerapps.io
  API:       https://knowz-sh-api.xxx.eastus2.azurecontainerapps.io
  MCP:       https://knowz-sh-mcp.xxx.eastus2.azurecontainerapps.io
  Swagger:   https://knowz-sh-api.xxx.eastus2.azurecontainerapps.io/swagger
  
  Login:     admin / <your password>
  API Key:   <generated key>
```

### Step 6.2: Claude Desktop MCP config

```json
{
  "mcpServers": {
    "knowz": {
      "url": "https://knowz-sh-mcp.xxx.eastus2.azurecontainerapps.io/mcp",
      "headers": {
        "X-Api-Key": "<your API key>"
      }
    }
  }
}
```

### Step 6.3: Local dev configuration

Offer to generate `appsettings.Local.json` with all connection strings for local API/MCP development against the deployed Azure resources (the deploy script already does this if Bicep path was used).

### Step 6.4: Teardown instructions

```bash
# Remove all resources in the resource group
az group delete --name $RG --yes --no-wait

# If using Terraform:
cd $REPO_ROOT/terraform/standard
terraform workspace select $WORKSPACE
terraform destroy
```

### Step 6.5: Cost monitoring

```bash
az consumption usage list \
  --query "[?contains(instanceName, '$PREFIX')]" \
  --start-date $(date -d 'first day of this month' +%Y-%m-%d) \
  --end-date $(date +%Y-%m-%d) -o table
```

### Step 6.6: Save deployment record (optional)

Ask with `AskUserQuestion` if the user wants to save this deployment to the Knowz vault for future reference (only if MCP Knowz tools are available).

---

## Error Handling

- **Azure CLI not logged in**: Pause and ask user to run `! az login`.
- **Wrong subscription**: Offer to switch with `az account set`.
- **Provider not registered**: Auto-register with `az provider register --wait`.
- **Tool not installed**: Provide platform-specific install commands; don't attempt to install for the user.
- **Deployment timeout** (>30 min): Show current status; let user decide to wait or abort.
- **Ambiguous existing resource match**: Always confirm with `AskUserQuestion` before wiring.

## Related Skills

- `/release-selfhosted` — Release a new version to GHCR
- `/update-selfhosted` — Upgrade an existing deployment to a new version
