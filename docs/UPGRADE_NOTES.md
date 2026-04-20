# Knowz Self-Hosted — Upgrade Notes

Release-specific operational guidance for self-hosted customers upgrading between versions. Read the section for the version you are moving **to** before pulling the image.

---

## Upgrading to the release containing `FIX_SelfhostedVectorDimsConfigurable`

**What changed:** The self-hosted API previously hardcoded the vector-search index dimension to `1536`. It now reads the dimension from configuration (`Embedding:Dimensions`), matching the main Knowz Platform's `ARCH_EmbeddingConfigOwnership` design. The change is mandatory for parity and for supporting higher-dim embedding models (e.g. `text-embedding-3-large` → 3072).

### What you need to do

1. **Add the `Embedding` config block** before pulling the new image. Two new keys are required whenever Azure AI Search is in use:

   | Config key | Example | Notes |
   |---|---|---|
   | `Embedding:ModelName` (`Embedding__ModelName` env) | `text-embedding-3-small` | Must match the Azure OpenAI embedding deployment's model |
   | `Embedding:Dimensions` (`Embedding__Dimensions` env) | `1536` | **Must match the model's output dim:** `text-embedding-3-small` / `text-embedding-ada-002` → `1536`, `text-embedding-3-large` → `3072` |

   See `selfhosted/docs/CONFIGURATION.md` for the full table.

2. **Supply it via the mechanism you deployed with:**

   - **ARM / Azure Portal deployment (Standard or Enterprise):** re-run the deployment with the new `embeddingDimensions` (and `embeddingModelNameParam` on Enterprise) parameter. The templates default to `1536`; set explicitly if you're on `-3-large`.
   - **Bicep:** re-deploy `selfhosted-test.bicep` or `selfhosted-enterprise.bicep` with the new `embeddingDimensions` param. Container Apps pick up `Embedding__ModelName` / `Embedding__Dimensions` env vars.
   - **Terraform (Standard or Enterprise):** set `embedding_dimensions` in `terraform.tfvars`, `terraform apply`.
   - **Docker Compose:** set `EMBEDDING_MODEL_NAME` / `EMBEDDING_DIMENSIONS` in `.env` (both default to `text-embedding-3-small` / `1536` if unset).
   - **Manual / user-secrets:** `dotnet user-secrets set "Embedding:ModelName" "..."` and `dotnet user-secrets set "Embedding:Dimensions" "..."`.
   - **Key Vault:** add two secrets `Embedding--ModelName` and `Embedding--Dimensions`. `selfhosted/scripts/setup-sh-dev.ps1` pulls both into user-secrets automatically.

3. **Restart the API container.** If `Embedding:Dimensions` is not configured, the API fails to start with a clear error message and a pointer to `ARCH_EmbeddingConfigOwnership`. Fix the config and restart.

### Dim mismatch with an existing index

If your configured `Embedding:Dimensions` does not match the dim of the `contentVector` field in the already-deployed Azure AI Search index, vector search will return no results and/or fail at ingest time with a dim mismatch. The index field dim is fixed at index creation time — you cannot edit it in place. Options:

| Situation | Action |
|---|---|
| You are still on the same embedding model, just setting the config explicitly | Set `Embedding:Dimensions = 1536` (the prior hardcoded value) — nothing else changes. |
| You are switching embedding models (e.g. `-3-small` → `-3-large`) | Delete the search index, deploy the new config, then re-ingest. See below. |

**Selfhosted does not yet have a SuperAdmin "wipe + reprocess" endpoint** (that ships with the main Knowz Platform via `FEAT_WipeAndReprocessEmbeddings`; the self-hosted parity NodeID `FEAT_SelfhostedWipeAndReprocess` is parked — no install base yet). For now, the manual procedure is:

```bash
# 1. Delete the search index (REST)
curl -X DELETE "https://<your-search>.search.windows.net/indexes/knowledge?api-version=2024-07-01" \
    -H "api-key: <admin-key>"

# 2. Update Embedding:Dimensions + AzureOpenAI:EmbeddingDeploymentName in config

# 3. Restart the API container. The index is auto-created on first use
#    (selfhosted/src/Knowz.SelfHosted.Infrastructure/Services/AzureSearchService.cs
#    — EnsureIndexExistsAsync) with the new dim.

# 4. Re-upload / re-sync your knowledge so new embeddings are generated.
```

### Verification

After restart, confirm the new dim is in use:

```bash
curl -s "https://<your-search>.search.windows.net/indexes/knowledge?api-version=2024-07-01" \
    -H "api-key: <admin-key>" \
    | jq '.fields[] | select(.name=="contentVector") | {name, dimensions: .dimensions}'
```

Expected output: `{"name":"contentVector","dimensions":1536}` (or `3072` if you switched).

The post-deploy smoke script (`selfhosted/scripts/post-deploy-smoke.sh`) exercises this end-to-end via its semantic-search step — a dim mismatch manifests as "seed content not found", failing the smoke loudly.
