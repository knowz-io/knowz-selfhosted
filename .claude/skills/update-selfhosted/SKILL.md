---
name: update-selfhosted
description: "Update an existing Knowz self-hosted deployment to a new version. Use when the user wants to upgrade container images of a deployed instance."
user-invocable: true
allowed-tools: Read, Bash, Glob, Grep, AskUserQuestion
argument-hint: "[--resource-group=NAME] [--version=X.Y.Z]"
---

# Update Knowz Self-Hosted Deployment

Upgrade the API, Web, and MCP container images of an existing Azure deployment to a new version. Handles rolling updates, database migrations (via `Database__AutoMigrate=true` on startup), and post-update health verification.

**Usage**: `/update-selfhosted [--resource-group=NAME] [--version=X.Y.Z]`

**Examples**:
- `/update-selfhosted` — Interactive: prompts for RG and version
- `/update-selfhosted --version=1.0.0` — Skip version prompt
- `/update-selfhosted --resource-group=rg-knowz-selfhosted --version=0.9.1`

Parse arguments.

---

## Phase 1: Pre-flight

### Step 1.1: Azure CLI check

```bash
az account show --query '{name:name, id:id}' -o json
```

If not logged in, prompt user to run `! az login`.

### Step 1.2: Resource group discovery

If `--resource-group` was provided, use it. Otherwise ask with `AskUserQuestion` after listing:
```bash
az group list --query "[?starts_with(name, 'rg-knowz')].{name:name, location:location}" -o table
```

### Step 1.3: Container Apps discovery

```bash
az containerapp list --resource-group $RG \
  --query "[].{name:name, image:properties.template.containers[0].image}" -o table
```

Confirm the 3 expected apps exist (`-api`, `-web`, `-mcp`). If not found, tell the user this resource group doesn't contain a Knowz deployment.

---

## Phase 2: Version check

### Step 2.1: Current versions

```bash
for app in api web mcp; do
  APP_NAME=$(az containerapp list --resource-group $RG \
    --query "[?ends_with(name, '-$app')].name | [0]" -o tsv)
  IMAGE=$(az containerapp show --name $APP_NAME -g $RG \
    --query 'properties.template.containers[0].image' -o tsv)
  echo "$app: $IMAGE"
done
```

### Step 2.2: Available versions from GHCR

```bash
gh api /orgs/knowz-io/packages/container/knowz-selfhosted-api/versions \
  --jq '.[] | select(.metadata.container.tags | length > 0) | .metadata.container.tags[]' 2>/dev/null \
  | sort -V | tail -10
```

If `gh` CLI not installed, list GHCR tags via the Docker registry API or tell the user to check https://github.com/knowz-io/knowz-selfhosted/releases.

### Step 2.3: Version selection

If `--version` was provided, use it. Otherwise ask with `AskUserQuestion`:
- Options: Latest stable / Specific version / Cancel

### Step 2.4: Change preview

Show a table:
```
Component    Current       → New
─────────────────────────────────────
API          0.8.1         → 0.9.0
Web          0.8.1         → 0.9.0
MCP          0.8.1         → 0.9.0
```

Confirm with `AskUserQuestion`: Proceed / Cancel.

---

## Phase 3: Update execution

### Step 3.1: Update each container app

```bash
for app in api web mcp; do
  APP_NAME=$(az containerapp list --resource-group $RG \
    --query "[?ends_with(name, '-$app')].name | [0]" -o tsv)
  NEW_IMAGE="ghcr.io/knowz-io/knowz-selfhosted-$app:$VERSION"
  echo "Updating $APP_NAME to $NEW_IMAGE..."
  az containerapp update \
    --name $APP_NAME \
    --resource-group $RG \
    --image $NEW_IMAGE \
    -o none
done
```

### Step 3.2: Wait for new revisions

```bash
for app in api web mcp; do
  APP_NAME=$(az containerapp list --resource-group $RG --query "[?ends_with(name, '-$app')].name | [0]" -o tsv)
  for i in {1..30}; do
    STATUS=$(az containerapp show --name $APP_NAME -g $RG --query 'properties.runningStatus' -o tsv)
    [ "$STATUS" = "Running" ] && break
    sleep 10
  done
done
```

Database migrations run automatically on API container startup (`Database__AutoMigrate=true` env var).

---

## Phase 4: Verification

### Step 4.1: Health checks

```bash
API_FQDN=$(az containerapp show --name ${PREFIX}-api -g $RG --query 'properties.configuration.ingress.fqdn' -o tsv)
curl -sfS "https://${API_FQDN}/healthz" && echo "✓ API healthy"
```

### Step 4.2: MCP service key sync

```bash
API_KEY=$(az containerapp show --name ${PREFIX}-api -g $RG \
  --query "properties.template.containers[0].env[?name=='MCP__ServiceKey'].value | [0]" -o tsv)
MCP_KEY=$(az containerapp show --name ${PREFIX}-mcp -g $RG \
  --query "properties.template.containers[0].env[?name=='MCP__ServiceKey'].value | [0]" -o tsv)
[ "$API_KEY" = "$MCP_KEY" ] && echo "✓ MCP keys aligned" || echo "✗ MCP keys mismatched"
```

### Step 4.3: DB migration check

Check API container logs for migration messages:
```bash
az containerapp logs show --name ${PREFIX}-api -g $RG --tail 50 \
  | grep -i "migration\|applied"
```

---

## Phase 5: Summary

Display results:
```
Update Complete

  Version:  0.8.1 → 0.9.0
  
  API:      ✓ Running
  Web:      ✓ Running
  MCP:      ✓ Running
  DB:       ✓ Migrations applied
  MCP Key:  ✓ Synced

  Web UI:   https://knowz-sh-web.xxx.eastus2.azurecontainerapps.io
```

### Rollback command

```bash
# To roll back to the previous version:
for app in api web mcp; do
  APP_NAME=$(az containerapp list --resource-group $RG --query "[?ends_with(name, '-$app')].name | [0]" -o tsv)
  az containerapp update --name $APP_NAME -g $RG \
    --image ghcr.io/knowz-io/knowz-selfhosted-$app:<previous-version>
done
```

---

## Error Handling

- **Azure CLI not logged in**: Pause; ask user to run `! az login`.
- **Image pull failure**: Check that the version tag exists in GHCR.
- **Container fails to start after update**: Check logs; offer rollback.
- **Migration failure**: Logs will show the error. Migration failure on startup means the new image has a schema requirement that can't be auto-applied (rare). Offer rollback.

## Related Skills

- `/release-selfhosted` — Release a new version to GHCR
- `/deploy-selfhosted` — Deploy a new instance from scratch
