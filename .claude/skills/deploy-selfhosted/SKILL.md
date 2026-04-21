---
name: deploy-selfhosted
description: "Deploy Knowz self-hosted to Azure — guides infrastructure provisioning, AI service configuration, and post-deployment verification. --enterprise adds BYO VNet/KV/LAW/OpenAI/ACR, MI-only auth, post-deploy smoke, and Front Door PL approval for landing-zone-ready customer deploys. Use when the user wants to deploy or set up a new self-hosted instance."
user-invocable: true
allowed-tools: Read, Write, Bash, Glob, Grep, AskUserQuestion
argument-hint: "[--terraform|--bicep] [--enterprise] [--resource-group=NAME] [--location=REGION]"
---

# Deploy Knowz Self-Hosted to Azure

A guided deployment of Knowz self-hosted to Azure. Covers pre-flight checks (Azure CLI, subscription, providers), interactive configuration (AI services with 3 modes: Deploy New / Use Existing / External), deployment execution (Terraform or Bicep), post-deployment verification, targeted remediation, and a final summary.

The `--enterprise` flag is **additive**: it inserts Phases 1.3.5 (Sentry DSN), 1.5 BYO-capture extension (VNet/PE subnet/KV/LAW/OpenAI/ACR), 4.5 (GHCR pull PAT → KV), 6.5 (real-enrichment smoke), and 6.6 (Front Door private-link approval). Standard deploys ignore every new phase.

**Usage**: `/deploy-selfhosted [--terraform|--bicep] [--enterprise] [--resource-group=NAME] [--location=REGION]`

**Examples**:
- `/deploy-selfhosted` — Full interactive standard deployment
- `/deploy-selfhosted --terraform --location=eastus2` — Terraform path, location pre-set
- `/deploy-selfhosted --bicep --resource-group=my-knowz-rg` — Bicep path, RG pre-set
- `/deploy-selfhosted --enterprise --bicep --resource-group=rg-sh-enterprise-customer-01` — Enterprise ALZ-ready deploy with BYO-infra prompts, MI-only auth, smoke test, FD PL approval

Parse arguments. Extract `--terraform`, `--bicep`, `--enterprise`, `--resource-group`, `--location` if provided.

```bash
# Argument parsing — --enterprise is additive
ENTERPRISE=false
for arg in "$@"; do
  case "$arg" in
    --enterprise) ENTERPRISE=true ;;
  esac
done
```

When `$ENTERPRISE = true`, every Phase marked "enterprise only" below runs in addition to the standard phases. API-key/JWT prompts in Phase 1.5 are **skipped** under enterprise (MI-only auth per `SH_ENTERPRISE_MI_SWAP.md`); credentials come from first-run bootstrap (β's `APP_CredentialBootstrap`).

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
# Also capture deployer object ID for Key Vault RBAC pre-wiring
DEPLOYER_OID=$(az ad signed-in-user show --query id -o tsv 2>/dev/null)
```

> **Note:** The deployer object ID (`$DEPLOYER_OID`) is injected into the Bicep/Terraform deployment and used to pre-grant `Key Vault Secrets Officer` on the new KV. This is required because subscription Owner role does NOT grant data-plane access in RBAC-enabled Key Vaults — without this step, the deployment fails with 403 on every KV secret.

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

If `gpt-5.2-chat` and `text-embedding-3-large` aren't found, warn the user and ask which deployments to use. (`text-embedding-3-small` at 1536 dims remains a supported option — if only small exists, warn and offer to deploy large alongside or proceed with small after flipping `Embedding__ModelName`/`Embedding__Dimensions` to match.)

**For "Deploy New"**, attempt a quota pre-check (best-effort):
```bash
az cognitiveservices usage list \
  --location $LOCATION \
  --query "[?contains(name.value, 'OpenAI')].{name:name.value, current:currentValue, limit:limit}" \
  -o table 2>/dev/null
```

If the API returns quota data and it's insufficient, offer fallback via `AskUserQuestion`:
- **Options**: Switch to Use Existing / Try different region / Skip OpenAI

### Step 1.3.5: Sentry DSN (enterprise only)

**Runs only if `$ENTERPRISE = true`.** Skipped in standard flow.

Ask with `AskUserQuestion`:
- **Question**: "Configure error tracking (Sentry)?"
- **Options**:
  - Yes — provide DSN (stores as KV secret `sentrydsn`)
  - No — skip (disables Sentry; no telemetry data leaves the tenant)

If Yes, prompt for the DSN and validate format before accepting. Re-prompt up to 2 more times, then fail the skill with a clear error (Rule 2 of `SH_ENTERPRISE_SKILL.md`: catch malformed input at prompt time, not post-deploy).

```bash
SENTRY_DSN_REGEX='^https://[a-f0-9]+@[a-z0-9.]+\.ingest\.sentry\.io/[0-9]+$'
attempt=0
while [ "$attempt" -lt 3 ]; do
  read -rp "Sentry DSN: " SENTRY_DSN
  if [[ "$SENTRY_DSN" =~ $SENTRY_DSN_REGEX ]]; then break; fi
  echo "Invalid DSN format (expect https://<key>@<org>.ingest.sentry.io/<project>)"
  attempt=$((attempt + 1))
done
[[ "$SENTRY_DSN" =~ $SENTRY_DSN_REGEX ]] || { echo "ERROR: Sentry DSN format invalid after 3 attempts"; exit 1; }
```

Persist the DSN to KV in Phase 3 (secret name `sentrydsn`). If the user answered "No", write an empty string to that secret so Bicep's secretRef resolves.

### Step 1.4: Optional features

Ask with `AskUserQuestion` (multi-select):
- Key Vault (Recommended)
- Monitoring — App Insights + Log Analytics (Recommended)
- Container Apps — API, Web, MCP as Azure Container Apps

### Step 1.5: Credentials

- **SQL admin password**: Ask user. Validate: 12-128 chars with uppercase, lowercase, digit, special char. Regex: `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{12,128}$`
- **SuperAdmin password** (for Web UI): Ask user. Validate: 8+ chars similar complexity. **Skipped in `--enterprise` mode** — credentials come from first-run bootstrap (`SelfHosted--BootstrapApiKey` KV secret, 24-hour auto-expiry).
- **API key + JWT secret** (if Container Apps): Auto-generate, display once, recommend saving. **Skipped in `--enterprise` mode** (MI-only auth per `SH_ENTERPRISE_MI_SWAP.md`).

#### Step 1.5 extension: BYO infrastructure capture (enterprise only)

**Runs only if `$ENTERPRISE = true`.** Each prompt validates the value before accepting. Failures re-prompt up to 2 more times, then abort the skill.

Ask sequentially with `AskUserQuestion` — **NOT multi-select**; each Yes branches into a follow-up:

1. **"Bring-your-own VNet subnet (for Container Apps)?"**
   - **Yes** → prompt `byoVnetSubnetId`. Validate: `az network vnet subnet show --ids "$ID" -o json` returns 200 AND the subnet has no delegation conflicting with CAE requirements.
   - **No** → Bicep creates a VNet with default CIDR.

2. **"Bring-your-own private-endpoint subnet?"**
   - **Yes** → prompt `byoVnetPeSubnetId`. Validate: same `az network vnet subnet show` check AND `.properties.delegations == []` (PE subnets must be non-delegated, enforced by `alz-assert-pe-subnet` Bicep guard).
   - **Create for me** → prompt `peSubnetAddressPrefix` (e.g., `10.100.0.0/24`). RFC-1918 validate: regex `^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.)[0-9./]+$`.

3. **"Bring-your-own Key Vault?"**
   - **Yes** → prompt `byoKeyVaultId`. Validate: `az keyvault show --ids "$ID"` returns 200 AND `.properties.enableRbacAuthorization == true` (enterprise standard — no access-policy KVs).

4. **"Bring-your-own Log Analytics Workspace (central)?"**
   - **Yes** → prompt `centralLogAnalyticsId`. Validate: `az monitor log-analytics workspace show --ids "$ID"` returns 200.

5. **"Bring-your-own Azure OpenAI?"**
   - **Yes** → prompt `existingOpenAiResourceId`. Validate: `az cognitiveservices account show --ids "$ID"` returns 200 AND `.kind in ('OpenAI', 'AIServices')`. Reject other kinds (e.g., `TextAnalytics`). Also probe required deployments (`gpt-5.2-chat`, `text-embedding-3-large`) via `az cognitiveservices account deployment list` — warn if missing, offer to proceed. Small (`text-embedding-3-small`, 1536 dims) is still supported — if the existing OpenAI resource has small instead of large, flag it so the operator can either add large or flip `Embedding__ModelName`/`Embedding__Dimensions` to 1536.
   - **No** → Bicep deploys a new OpenAI resource (warn about quota in the tenant).

6. **"Pull images from external ACR (instead of GHCR)?"**
   - **Yes** → prompt `externalAcrName` + `externalAcrResourceGroup`. Validate: `az acr show -n "$NAME" -g "$RG"` returns 200. Check the deployer or the target MI already has `AcrPull` on that registry — if not, offer to grant it.
   - **No** → use GHCR (default). Phase 4.5 will collect the pull PAT.

7. **"Entra ID admin group object ID for SQL AAD auth?"** (mandatory in enterprise)
   - Prompt `aadAdminObjectId`. Validate: `az ad group show --group "$OID"` returns 200.
   - Prompt `aadAdminEmail` (for alerting; Bicep can't look up email from OID — see `SH_ENTERPRISE_SKILL.md` D2). Validate: basic email regex AND cross-check matches `.mail` on the AAD group if present; warn if mismatched.

Each captured value flows into Bicep parameters in Phase 3 (see `SH_ENTERPRISE_BYO_INFRA.md` param list). If both `byoVnetPeSubnetId` and `peSubnetAddressPrefix` are unset, Bicep's `alz-assert-pe-subnet` guard will fail the template at evaluation time with a clear message — this matches the 2026-04-17 partner-outage hardening.

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
    OpenAI:         Deploy New (gpt-5.2-chat + text-embedding-3-large)
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
  Azure OpenAI         ✓ Connected gpt-5.2-chat + text-embedding-3-large
  AI Search            ✓ Indexed   knowledge (HNSW, ${Embedding__Dimensions} dims)
  Azure AI Vision      ✓ Connected S1
  Document Intelligence ✓ Connected S0
  SQL Database         ✓ Migrated  McpKnowledge
  Storage              ✓ Ready     selfhosted-files
  Key Vault            ✓ Populated 12 secrets
  App Insights         ✓ Logging   Connected
  MCP Key Sync         ✓ Matched   API ↔ MCP
═══════════════════════════════════════════════════════════════════
```

### Step 4.8: GHCR pull PAT (enterprise only)

**Runs only if `$ENTERPRISE = true` AND the user did NOT select external ACR in Phase 1.5.**

Enterprise Container Apps pull private images from `ghcr.io/knowz-io/knowz-selfhosted:*`. The PAT lives in Key Vault (secret `ghcr--pull--token`), referenced by `containerApp.registries[].passwordSecretRef` — never passed through env vars or the shell history (Rule 3, `SH_ENTERPRISE_SKILL.md`).

Ask with `AskUserQuestion`:
- **Question**: "Provide a fine-grained GitHub PAT with `read:packages` scope?"
- **Options**:
  - Provide now (Recommended — required for image pulls)
  - I'll set it later via `az keyvault secret set` (deploy succeeds, but Container Apps revisions will fail to pull until the secret is populated)

If "Provide now":

```bash
# Prompt + format validation
PAT_REGEX='^(ghp_|github_pat_)[A-Za-z0-9_]{36,}$'
attempt=0
while [ "$attempt" -lt 3 ]; do
  read -rsp "GitHub PAT (read:packages, 90-day expiry): " GHCR_PAT
  echo
  if [[ "$GHCR_PAT" =~ $PAT_REGEX ]]; then break; fi
  echo "Invalid PAT format (expect ghp_... or github_pat_..., 40+ chars)"
  attempt=$((attempt + 1))
done
[[ "$GHCR_PAT" =~ $PAT_REGEX ]] || { echo "ERROR: GHCR PAT format invalid after 3 attempts"; exit 1; }

# Persist to KV with expiry tag (90-day rotation reminder)
EXPIRES_AT="$(date -d '+90 days' -Iseconds 2>/dev/null || date -u -v+90d +%Y-%m-%dT%H:%M:%SZ)"
az keyvault secret set \
  --vault-name "$KV_NAME" \
  --name "ghcr--pull--token" \
  --value "$GHCR_PAT" \
  --tags "expiresAt=$EXPIRES_AT" "scope=read:packages" "rotationInterval=90d" \
  > /dev/null
unset GHCR_PAT

# Restart revisions so the Container App picks up the updated registry credential
for app in ${PREFIX}-api ${PREFIX}-mcp ${PREFIX}-web; do
  az containerapp revision restart -n "$app" -g "$RG" 2>/dev/null || true
done
```

Log a reminder to the deployment summary: "Rotate `ghcr--pull--token` before $EXPIRES_AT (14-day advance reminder recommended)."

---

## Phase 5: Remediation (if any checks failed)

For each failed check, offer targeted automated fix:

| Failure | Automated Fix |
|---------|---------------|
| API/Web/MCP not healthy | `az containerapp logs show --name X -g $RG --tail 100` to diagnose; offer restart |
| OpenAI not connected | Retrieve real key, update secret, restart API: `az containerapp secret set --name X --secrets openai-apikey=$REAL_KEY` then restart revision |
| Search index missing | Re-create using schema from `selfhosted-deploy.ps1` — pass `-EmbeddingDimensions $DIM` matching the deployed model (3072 for -3-large default, 1536 for -3-small / ada-002). HNSW, dim must match `Embedding__Dimensions` in the container. |
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

### Step 6.4b: Cost monitoring

```bash
az consumption usage list \
  --query "[?contains(instanceName, '$PREFIX')]" \
  --start-date $(date -d 'first day of this month' +%Y-%m-%d) \
  --end-date $(date +%Y-%m-%d) -o table
```

### Step 6.5: Real-enrichment smoke (enterprise only)

**Runs only if `$ENTERPRISE = true`.** Fails the skill on any non-zero exit — resources remain in place for post-mortem (Rule 2 of `SH_ENTERPRISE_SMOKE_TEST.md`).

The smoke test exercises the full write path (upload → enrichment → summary → tags → chunks → search → outbox) using three file types (MD, PDF, PNG) so OpenAI summarization, DocIntel extraction, and Vision analysis are each verified once. `/healthz`-only smoke is explicitly forbidden — it reports green on schema-drift-broken deploys (2026-04-15 incident).

```bash
# Export RESOURCE_GROUP + API_APP_NAME so smoke Step 8 (Data Protection canary,
# VERIFY 12) runs by default — restarts the API revision and decrypts a token.
# Without these, the canary prints a WARN and skips; exporting them here makes
# the default enterprise flow cover DP recovery end-to-end.
export RESOURCE_GROUP="$RG"
export API_APP_NAME="${PREFIX}-api"

bash "$REPO_ROOT/selfhosted/scripts/post-deploy-smoke.sh" \
    "https://${API_FQDN}" "$KV_NAME" \
    || {
        echo "ERROR: Post-deploy smoke FAILED. Resources remain in place for inspection."
        echo "  Inspect API logs:   az containerapp logs show -n ${PREFIX}-api -g $RG --tail 200"
        echo "  Inspect outbox:     az sql db query ... 'SELECT TOP 20 * FROM EnrichmentOutboxEntries WHERE Status=''Failed'''"
        echo "  Re-run after fix:   bash $REPO_ROOT/selfhosted/scripts/post-deploy-smoke.sh https://$API_FQDN $KV_NAME"
        exit 1
    }
```

On success the smoke script prints `[8/8] SMOKE PASSED` and cleans up its 3 seed knowledge items so the vault returns empty for the bootstrap user.

### Step 6.6: Front Door private-link approval (enterprise only, conditional)

**Runs only if `$ENTERPRISE = true` AND Front Door was deployed (`$ENTERPRISE_FD = true`).** Invoked AFTER Phase 6.5 succeeds — never enable traffic on a broken stack (Rule 6 of `SH_ENTERPRISE_SMOKE_TEST.md`).

```bash
if [ "$ENTERPRISE_FD" = "true" ]; then
    pwsh "$REPO_ROOT/selfhosted/infrastructure/post-deploy/approve-front-door-pl.ps1" \
        -ResourceGroup "$RG" \
        || {
            echo "ERROR: Front Door private-link approval FAILED."
            echo "  Inspect PL connections: az network private-endpoint-connection list --id \$(az containerapp env show -n ${PREFIX}-env -g $RG --query id -o tsv)"
            exit 1
        }
fi
```

The script polls for up to 180s for Pending FD shared-private-link connections on the CAE, approves them via `az network private-endpoint-connection approve`, and fails if any remaining connection is `Pending`, `Disconnected`, or `Rejected` (VERIFY criterion in `SH_ENTERPRISE_SMOKE_TEST.md`).

### Step 6.7: Save deployment record (optional)

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
