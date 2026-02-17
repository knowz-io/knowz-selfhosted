---
description: "E2E API test for selfhosted deployment — auth, knowledge CRUD, search, ask, chat, tenant isolation, vault moves, edits, attachments, portability"
argument-hint: "[--url=http://localhost:5000] [--user=admin] [--pass=changeme] [--skip-cleanup]"
---

# Selfhosted E2E API Test

Validate that a selfhosted Knowz deployment is fully operational by exercising the complete API pipeline: authentication, vault/knowledge CRUD, enrichment polling, search, ask (RAG), chat, tenant isolation, vault moves, content edits, file attachments, and cross-tenant portability.

**Usage**: `/test-e2e-api [--url=http://localhost:5000] [--user=admin] [--pass=changeme] [--skip-cleanup]`

## Arguments

Parse from `$ARGUMENTS`:

| Arg | Default | Description |
|-----|---------|-------------|
| `--url` | `http://localhost:5000` | Base URL of the selfhosted API |
| `--user` | `admin` | Login username (maps to `.env` `ADMIN_USERNAME`) |
| `--pass` | env var `ADMIN_PASSWORD`, then `changeme` | Login password |
| `--skip-cleanup` | false | Keep test data for manual inspection |

---

## Phase 0: Health Check

```bash
curl -sf "{url}/healthz" | jq .
```

Expected: `{"status":"healthy","version":"1.0.0"}`

**If this fails, stop immediately** — the API is not reachable. Tell the user to check that `docker compose up -d` has completed and the API container is running.

---

## Phase 1: Authentication

### 1A: Login

```bash
curl -sf -X POST "{url}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"{user}","password":"{pass}"}'
```

Response shape (`AuthResult`):
```json
{"token":"eyJ...","expiresAt":"2026-...","user":{"id":"guid","username":"admin","role":"Admin"}}
```

Extract `token` from the response. **All subsequent requests** use header `Authorization: Bearer {token}`.

Store `admin_user_id` and `admin_tenant_id` from the response (get tenant ID from `GET /api/v1/auth/me` — the `tenantId` field).

### 1B: Verify token

```bash
curl -sf "{url}/api/v1/auth/me" -H "Authorization: Bearer {token}"
```

Expected: `UserDto` with `id`, `username`, `role`, `tenantId` fields. Confirms the JWT is valid.

Store `admin_tenant_id` from the `tenantId` field.

---

## Phase 2: Create Vault

Generate a unique timestamp: `E2E-Test-{unix_timestamp}`

```bash
curl -sf -X POST "{url}/api/v1/vaults" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"E2E-Test-{timestamp}","description":"Automated E2E test vault"}'
```

Response (`CreateVaultResult`, HTTP 201):
```json
{"id":"guid","name":"E2E-Test-1739700000","created":true}
```

Store `vaultId` from response.

### Verify vault exists

```bash
curl -sf "{url}/api/v1/vaults?includeStats=true" -H "Authorization: Bearer {token}"
```

Response (`VaultListResponse`): Confirm the vault appears in `vaults[]` array with matching name.

---

## Phase 3: Create Knowledge Items (3 items)

Create 3 items with distinct, factual, searchable content. Each uses:

```bash
curl -sf -X POST "{url}/api/v1/knowledge" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{...}'
```

Response (`CreateKnowledgeResult`, HTTP 201): `{"id":"guid","title":"...","created":true}`

### Item 1: Platform Info (for ask test)

```json
{
  "title": "Knowz Platform Architecture Overview",
  "content": "Knowz is a self-hosted knowledge management platform built on .NET 9 and SQL Server. It organizes information into vaults, supports tags for categorization, and provides semantic search powered by Azure AI Search. The platform features automatic AI enrichment including summarization, entity extraction, and vector embeddings for retrieval-augmented generation (RAG). It runs as Docker containers with a React frontend, .NET API backend, and optional MCP server for AI tool integration.",
  "type": "Note",
  "vaultId": "{vaultId}",
  "tags": ["e2e-test", "architecture"]
}
```

### Item 2: Recipe (for search diversity)

```json
{
  "title": "Classic Chocolate Chip Cookie Recipe",
  "content": "Preheat oven to 375°F (190°C). Cream together 1 cup softened butter, 3/4 cup granulated sugar, and 3/4 cup packed brown sugar until fluffy. Beat in 2 large eggs and 1 teaspoon vanilla extract. In a separate bowl, whisk 2 1/4 cups all-purpose flour, 1 teaspoon baking soda, and 1 teaspoon salt. Gradually mix dry ingredients into wet mixture. Fold in 2 cups semi-sweet chocolate chips. Drop rounded tablespoons onto ungreased baking sheets. Bake for 9 to 11 minutes or until golden brown. Cool on baking sheet for 2 minutes before transferring to wire rack. Makes approximately 5 dozen cookies.",
  "type": "Note",
  "vaultId": "{vaultId}",
  "tags": ["e2e-test", "recipe"]
}
```

### Item 3: Science (for chat test)

```json
{
  "title": "James Webb Space Telescope Facts",
  "content": "The James Webb Space Telescope (JWST) was launched on December 25, 2021, aboard an Ariane 5 rocket from French Guiana. It orbits the Sun at the second Lagrange point (L2), approximately 1.5 million kilometers from Earth. The telescope's primary mirror is 6.5 meters in diameter, composed of 18 hexagonal gold-plated beryllium segments. JWST observes primarily in infrared wavelengths, allowing it to see through dust clouds and observe the earliest galaxies formed after the Big Bang. Its sunshield is roughly the size of a tennis court and keeps the instruments at an operating temperature of about -233°C (-387°F).",
  "type": "Note",
  "vaultId": "{vaultId}",
  "tags": ["e2e-test", "science"]
}
```

Store all 3 knowledge IDs: `knowledgeId1`, `knowledgeId2`, `knowledgeId3`.

### Verify each item

For each ID:
```bash
curl -sf "{url}/api/v1/knowledge/{id}" -H "Authorization: Bearer {token}" | jq '{id, title, content: (.content[:50] + "..."), tags}'
```

Confirm `title` and `content` match what was sent.

---

## Phase 4: Wait for Enrichment

Poll each knowledge item checking the `isIndexed` field:

```bash
curl -sf "{url}/api/v1/knowledge/{id}" -H "Authorization: Bearer {token}" | jq '{id, isIndexed, indexedAt}'
```

**Polling strategy:**
- Poll every 5 seconds, max 60 seconds total
- Success: all 3 items have `isIndexed: true`
- Timeout: continue with a WARNING

**AI-not-configured detection:**
If after 60 seconds, **none** of the 3 items have `isIndexed: true`, set `AI_NOT_CONFIGURED=true`. This means Azure AI services (OpenAI, AI Search) are not configured — a valid deployment state for basic CRUD-only usage.

When `AI_NOT_CONFIGURED=true`, AI-dependent tests report **SKIPPED** instead of **FAIL**.

---

## Phase 5: Search, Ask & Chat

Skip this entire phase if `AI_NOT_CONFIGURED=true` — report all as SKIPPED.

### 5A: Search

```bash
curl -sf "{url}/api/v1/search?q=chocolate+chip+cookie+recipe&limit=5&vaultId={vaultId}" \
  -H "Authorization: Bearer {token}"
```

Response (`SearchResponse`):
```json
{"items":[{"knowledgeId":"guid","title":"...","score":0.85,...}],"totalResults":1}
```

**PASS criteria:** `totalResults > 0` AND at least one item's `knowledgeId` matches `knowledgeId2` (the cookie recipe).

### 5B: Ask (RAG)

```bash
curl -sf -X POST "{url}/api/v1/ask" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"question":"What technology stack does the Knowz platform use?","vaultId":"{vaultId}"}'
```

Response (`AskAnswerResponse`):
```json
{"answer":"Knowz is built on .NET 9 and SQL Server...","sources":[{"knowledgeId":"guid"}],"confidence":0.85}
```

**PASS criteria:** `confidence > 0` AND answer mentions at least one of: `.NET`, `SQL`, `vault`, `Docker`.

**Not-found detection:** If `confidence == 0` and answer contains `"AI services are not configured"` (from `NoOpOpenAIService`), report SKIPPED.

### 5C: Chat

```bash
curl -sf -X POST "{url}/api/v1/chat" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"question":"Tell me about the James Webb Space Telescope","conversationHistory":[],"vaultId":"{vaultId}"}'
```

Response (`ChatResponse`):
```json
{"answer":"The James Webb Space Telescope was launched...","sources":[{"knowledgeId":"guid"}],"confidence":0.9}
```

**PASS criteria:** `confidence > 0` AND answer mentions at least one of: `JWST`, `telescope`, `Lagrange`, `mirror`, `infrared`.

### 5D: Chat with History

Use the response from 5C as conversation history:

```bash
curl -sf -X POST "{url}/api/v1/chat" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "question":"How far is it from Earth?",
    "conversationHistory":[
      {"role":"user","content":"Tell me about the James Webb Space Telescope"},
      {"role":"assistant","content":"{answer_from_5C}"}
    ],
    "vaultId":"{vaultId}"
  }'
```

**PASS criteria:** `confidence > 0` AND answer mentions at least one of: `1.5 million`, `L2`, `kilometer`, `Lagrange`.

---

## Phase 6: Tenant Isolation

Test that data is properly scoped per-tenant and that role-based access controls work.

### 6A: Create test tenant

```bash
curl -sf -X POST "{url}/api/v1/admin/tenants" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"E2E-Isolation-{timestamp}","slug":"e2e-isolation-{timestamp}"}'
```

Response (`TenantDto`, HTTP 201): Store `test_tenant_id` from the `id` field.

**PASS criteria:** HTTP 201, response has `id` and `name`.

### 6B: Create user in test tenant

```bash
curl -sf -X POST "{url}/api/v1/admin/users" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"{test_tenant_id}","username":"e2e-testuser-{timestamp}","password":"E2ETestPass123!","role":"User"}'
```

Response (`UserDto`, HTTP 201): Store `test_user_id`.

**PASS criteria:** HTTP 201, user's `tenantId` matches `test_tenant_id`.

### 6C: Login as test user

```bash
curl -sf -X POST "{url}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"e2e-testuser-{timestamp}","password":"E2ETestPass123!"}'
```

Store `testuser_token` from response.

**PASS criteria:** Login succeeds, role is `User`, tenantId matches `test_tenant_id`.

### 6D: Knowledge isolation

```bash
curl -sf "{url}/api/v1/knowledge" -H "Authorization: Bearer {testuser_token}"
```

**PASS criteria:** `totalItems` is `0` — test user sees none of the admin's knowledge items.

### 6E: Vault isolation

```bash
curl -sf "{url}/api/v1/vaults" -H "Authorization: Bearer {testuser_token}"
```

**PASS criteria:** Response `vaults` array does NOT contain the admin's test vault (no vault with name matching `E2E-Test-{timestamp}`).

### 6F: Search isolation

Skip if `AI_NOT_CONFIGURED=true`.

```bash
curl -sf "{url}/api/v1/search?q=chocolate+chip+cookie&limit=5" \
  -H "Authorization: Bearer {testuser_token}"
```

**PASS criteria:** `totalResults` is `0` — test user cannot find admin's indexed content.

### 6G: Cross-tenant delete blocked

Use the test user token to try deleting an admin knowledge item:

```bash
curl -s -o /dev/null -w "%{http_code}" -X DELETE "{url}/api/v1/knowledge/{knowledgeId1}" \
  -H "Authorization: Bearer {testuser_token}"
```

**PASS criteria:** HTTP `404` — item is invisible to the test user, so cannot be deleted.

### 6H: SuperAdmin X-Tenant-Id switching

Admin (SuperAdmin role) uses the `X-Tenant-Id` header to view test tenant's data:

```bash
curl -sf "{url}/api/v1/knowledge" \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-Id: {test_tenant_id}"
```

**PASS criteria:** `totalItems` is `0` — admin correctly sees the empty test tenant's knowledge list.

### 6I: Regular user X-Tenant-Id bypass blocked

Test user attempts to use `X-Tenant-Id` to view admin's data:

```bash
curl -sf "{url}/api/v1/knowledge" \
  -H "Authorization: Bearer {testuser_token}" \
  -H "X-Tenant-Id: {admin_tenant_id}"
```

**PASS criteria:** `totalItems` is `0` — the header is ignored for non-SuperAdmin users, so test user still sees their own (empty) data.

### 6J: Role-based access control

Test user attempts to access admin-only endpoint:

```bash
curl -s -o /dev/null -w "%{http_code}" "{url}/api/v1/admin/tenants" \
  -H "Authorization: Bearer {testuser_token}"
```

**PASS criteria:** HTTP `403` — regular users cannot access admin endpoints.

### 6K: Cleanup test user + tenant

**Skip if `--skip-cleanup` was passed.**

```bash
# Delete user
curl -sf -X DELETE "{url}/api/v1/admin/users/{test_user_id}" -H "Authorization: Bearer {token}"
# Delete tenant
curl -sf -X DELETE "{url}/api/v1/admin/tenants/{test_tenant_id}" -H "Authorization: Bearer {token}"
```

**PASS criteria:** Both deletions succeed.

---

## Phase 7: Vault Move

Test moving knowledge items between vaults (single + batch).

### 7A: Create target vault

```bash
curl -sf -X POST "{url}/api/v1/vaults" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"E2E-MoveTarget-{timestamp}","description":"Target vault for move tests"}'
```

Store `moveTargetVaultId` from response.

**PASS criteria:** HTTP 201, vault created.

### 7B: Move item 1 to target vault (single move via PUT)

```bash
curl -sf -X PUT "{url}/api/v1/knowledge/{knowledgeId1}" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"vaultId":"{moveTargetVaultId}"}'
```

**PASS criteria:** HTTP 200, `updated: true`.

### 7C: Verify item is in target vault

```bash
curl -sf "{url}/api/v1/vaults/{moveTargetVaultId}/contents" -H "Authorization: Bearer {token}"
```

**PASS criteria:** Response `items` array contains an item with `id` matching `knowledgeId1`.

### 7D: Verify item removed from source vault

```bash
curl -sf "{url}/api/v1/vaults/{vaultId}/contents" -H "Authorization: Bearer {token}"
```

**PASS criteria:** Response `items` array does NOT contain `knowledgeId1` (should only have items 2 and 3).

### 7E: Search still finds moved item

Skip if `AI_NOT_CONFIGURED=true`.

```bash
curl -sf "{url}/api/v1/search?q=Knowz+platform+architecture&limit=5" \
  -H "Authorization: Bearer {token}"
```

**PASS criteria:** `totalResults > 0` AND at least one result's `knowledgeId` matches `knowledgeId1`.

### 7F: Batch move items 2+3 to target vault

```bash
curl -sf -X POST "{url}/api/v1/knowledge/batch-move" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"knowledgeIds":["{knowledgeId2}","{knowledgeId3}"],"targetVaultId":"{moveTargetVaultId}"}'
```

Response (`BatchMoveResult`):
```json
{"requestedCount":2,"movedCount":2,"movedIds":["...","..."],"notFoundIds":[]}
```

**PASS criteria:** `movedCount` is `2` AND `notFoundIds` is empty.

### 7G: Verify target vault has all 3 items

```bash
curl -sf "{url}/api/v1/vaults/{moveTargetVaultId}/contents" -H "Authorization: Bearer {token}"
```

**PASS criteria:** `totalItems` is `3`.

### 7H: Move all back to original vault and cleanup

Move all 3 items back:
```bash
curl -sf -X POST "{url}/api/v1/knowledge/batch-move" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"knowledgeIds":["{knowledgeId1}","{knowledgeId2}","{knowledgeId3}"],"targetVaultId":"{vaultId}"}'
```

Then delete the target vault:
```bash
curl -sf -X DELETE "{url}/api/v1/vaults/{moveTargetVaultId}" -H "Authorization: Bearer {token}"
```

**PASS criteria:** Batch move returns `movedCount: 3`, vault delete succeeds.

---

## Phase 8: Edit Content + Search Updates

Skip this entire phase if `AI_NOT_CONFIGURED=true` — report all as SKIPPED.

### 8A: Verify original content is searchable

```bash
curl -sf "{url}/api/v1/search?q=chocolate+chip+cookie&limit=5&vaultId={vaultId}" \
  -H "Authorization: Bearer {token}"
```

**PASS criteria:** At least one result's `knowledgeId` matches `knowledgeId2`.

### 8B: Edit item 2 — change to unique content

```bash
curl -sf -X PUT "{url}/api/v1/knowledge/{knowledgeId2}" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"title":"Yggdrasil Sourdough Bread Method","content":"The Yggdrasil sourdough method is a unique breadmaking technique that uses a three-stage fermentation process inspired by Nordic baking traditions. First, create a rye-based starter using wild-captured yeast from birch bark. Second, perform a 24-hour cold fermentation at exactly 4°C. Third, shape the dough using the traditional Scandinavian folding pattern and bake in a cast-iron Dutch oven at 450°F for 35 minutes. The result is a deeply flavored sourdough with a crackling crust and open crumb structure. This method was popularized by baker Elara Magnusdottir of Reykjavik."}'
```

**PASS criteria:** HTTP 200, `updated: true`.

### 8C: Poll for re-indexing

Poll `GET /api/v1/knowledge/{knowledgeId2}` checking for `indexedAt` to be updated (newer than the value before the edit). Poll every 5 seconds, max 60 seconds.

**PASS criteria:** `indexedAt` updates within timeout.

### 8D: Search for new content

```bash
curl -sf "{url}/api/v1/search?q=Yggdrasil+sourdough&limit=5" \
  -H "Authorization: Bearer {token}"
```

**PASS criteria:** `totalResults > 0` AND at least one result matches `knowledgeId2`.

### 8E: Search for old content — should NOT find item 2

```bash
curl -sf "{url}/api/v1/search?q=chocolate+chip+cookie&limit=5&vaultId={vaultId}" \
  -H "Authorization: Bearer {token}"
```

**PASS criteria:** Either `totalResults == 0` OR none of the results have `knowledgeId` matching `knowledgeId2`.

### 8F: Chat about edited content

```bash
curl -sf -X POST "{url}/api/v1/chat" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"question":"What is the Yggdrasil method for making bread?","conversationHistory":[],"vaultId":"{vaultId}"}'
```

**PASS criteria:** Answer mentions at least one of: `sourdough`, `bread`, `fermentation`, `Yggdrasil`, `Nordic`.

---

## Phase 9: File Attachment + Ask

Skip this entire phase if `AI_NOT_CONFIGURED=true` — report all as SKIPPED.

### 9A: Create anchor knowledge item

```bash
curl -sf -X POST "{url}/api/v1/knowledge" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"title":"E2E File Attachment Test","content":"This knowledge item is used to test file attachment and content extraction in the E2E test suite.","type":"Note","vaultId":"{vaultId}","tags":["e2e-test","file-test"]}'
```

Store `fileTestKnowledgeId` from response.

**PASS criteria:** HTTP 201, item created.

### 9B: Upload text file

Create a temporary text file and upload it:

```bash
# Create temp file with unique facts
echo "Project Nightingale Status Report - CLASSIFIED
Lead Researcher: Dr. Eleanor Whitmore
Project Start Date: March 15, 2024
Facility: Building 7, Level B3

Project Nightingale is a groundbreaking initiative to develop bio-luminescent navigation systems for deep-sea exploration vehicles. Dr. Eleanor Whitmore assembled a team of 12 marine biologists and 8 optical engineers. The project achieved its first milestone on June 1, 2024 when the prototype navigation beacon successfully guided an unmanned submersible through a simulated deep-ocean trench at 4,000 meters depth. The beacon uses genetically modified dinoflagellate organisms that produce consistent light output for up to 72 hours." > /tmp/e2e-nightingale-report.txt

curl -sf -X POST "{url}/api/v1/files/upload" \
  -H "Authorization: Bearer {token}" \
  -F "file=@/tmp/e2e-nightingale-report.txt"
```

Response (`FileUploadResult`, HTTP 201): Store `fileRecordId`.

**PASS criteria:** HTTP 201, `success: true`.

### 9C: Attach file to knowledge item

```bash
curl -sf -X POST "{url}/api/v1/knowledge/{fileTestKnowledgeId}/attachments" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"fileRecordId":"{fileRecordId}"}'
```

Response (`FileAttachmentDto`, HTTP 201).

**PASS criteria:** HTTP 201, attachment created with matching `knowledgeId` and `fileRecordId`.

### 9D: Trigger reprocess

```bash
curl -sf -X POST "{url}/api/v1/knowledge/{fileTestKnowledgeId}/reprocess" \
  -H "Authorization: Bearer {token}"
```

**PASS criteria:** HTTP 200, `reprocessed: true`.

### 9E: Poll for re-indexing

Poll `GET /api/v1/knowledge/{fileTestKnowledgeId}` checking `isIndexed: true`. Poll every 5 seconds, max 60 seconds.

**PASS criteria:** Item becomes indexed within timeout.

### 9F: Ask about file content

```bash
curl -sf -X POST "{url}/api/v1/ask" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"question":"Who led Project Nightingale and what was it about?","vaultId":"{vaultId}"}'
```

**PASS criteria:** Answer mentions at least one of: `Nightingale`, `Whitmore`, `Eleanor`, `bio-luminescent`, `deep-sea`.

### 9G: Cleanup file test data

**Skip if `--skip-cleanup` was passed.** This cleanup is also done in Phase 11.

```bash
# Detach file
curl -sf -X DELETE "{url}/api/v1/knowledge/{fileTestKnowledgeId}/attachments/{fileRecordId}" \
  -H "Authorization: Bearer {token}"
# Delete knowledge item
curl -sf -X DELETE "{url}/api/v1/knowledge/{fileTestKnowledgeId}" -H "Authorization: Bearer {token}"
# Delete file
curl -sf -X DELETE "{url}/api/v1/files/{fileRecordId}" -H "Authorization: Bearer {token}"
```

---

## Phase 10: Cross-Tenant Portability

Test exporting data from the source tenant and importing it into a new target tenant.

### 10A: Create target tenant + admin user

```bash
# Create tenant
curl -sf -X POST "{url}/api/v1/admin/tenants" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"name":"E2E-Import-{timestamp}","slug":"e2e-import-{timestamp}"}'
```

Store `import_tenant_id`.

```bash
# Create admin user in target tenant
curl -sf -X POST "{url}/api/v1/admin/users" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"{import_tenant_id}","username":"e2e-import-admin-{timestamp}","password":"E2EImportPass123!","role":"Admin"}'
```

Store `import_user_id`.

**PASS criteria:** Both tenant and user created (HTTP 201).

### 10B: Export from source tenant

```bash
curl -sf "{url}/api/v1/portability/export" -H "Authorization: Bearer {token}"
```

Store the full JSON response as `export_package`.

**PASS criteria:** Response contains `metadata` with `totalKnowledgeItems > 0` and `vaults` array is non-empty.

### 10C: Import to target tenant

Use the admin token with `X-Tenant-Id` header to import into the target tenant:

```bash
curl -sf -X POST "{url}/api/v1/portability/import?strategy=skip" \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-Id: {import_tenant_id}" \
  -H "Content-Type: application/json" \
  -d '{export_package}'
```

Response (`PortableImportResult`).

**PASS criteria:** `success: true` AND `knowledgeItems.created > 0`.

### 10D: Verify imported data exists in target tenant

```bash
curl -sf "{url}/api/v1/knowledge" \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-Id: {import_tenant_id}"
```

**PASS criteria:** `totalItems > 0` — imported knowledge items are visible in the target tenant.

### 10E: Verify source tenant data unchanged

```bash
curl -sf "{url}/api/v1/knowledge" -H "Authorization: Bearer {token}"
```

**PASS criteria:** `totalItems` matches the count before the export (source data not affected).

### 10F: Cleanup target user + tenant

**Skip if `--skip-cleanup` was passed.**

```bash
curl -sf -X DELETE "{url}/api/v1/admin/users/{import_user_id}" -H "Authorization: Bearer {token}"
curl -sf -X DELETE "{url}/api/v1/admin/tenants/{import_tenant_id}" -H "Authorization: Bearer {token}"
```

---

## Phase 11: Cleanup

**Skip this phase if `--skip-cleanup` was passed.** Always attempt cleanup even if earlier phases failed — use whatever IDs were captured.

### Delete knowledge items

For each captured knowledge ID (`knowledgeId1`, `knowledgeId2`, `knowledgeId3`, and `fileTestKnowledgeId` if it exists):
```bash
curl -sf -X DELETE "{url}/api/v1/knowledge/{id}" -H "Authorization: Bearer {token}"
```

Expected (`DeleteResult`): `{"id":"guid","deleted":true}`

### Delete files

If `fileRecordId` was captured:
```bash
curl -sf -X DELETE "{url}/api/v1/files/{fileRecordId}" -H "Authorization: Bearer {token}"
```

### Delete vaults

Delete main test vault and move target vault (if not already deleted):
```bash
curl -sf -X DELETE "{url}/api/v1/vaults/{vaultId}" -H "Authorization: Bearer {token}"
curl -sf -X DELETE "{url}/api/v1/vaults/{moveTargetVaultId}" -H "Authorization: Bearer {token}"
```

Expected (`DeleteVaultResult`): `{"id":"guid","deleted":true}`

### Delete test tenants + users

If Phase 6 or Phase 10 cleanup was skipped or failed:
```bash
# Phase 6 artifacts
curl -sf -X DELETE "{url}/api/v1/admin/users/{test_user_id}" -H "Authorization: Bearer {token}"
curl -sf -X DELETE "{url}/api/v1/admin/tenants/{test_tenant_id}" -H "Authorization: Bearer {token}"
# Phase 10 artifacts
curl -sf -X DELETE "{url}/api/v1/admin/users/{import_user_id}" -H "Authorization: Bearer {token}"
curl -sf -X DELETE "{url}/api/v1/admin/tenants/{import_tenant_id}" -H "Authorization: Bearer {token}"
```

### Verify deletion

```bash
curl -s -o /dev/null -w "%{http_code}" "{url}/api/v1/knowledge/{knowledgeId1}" \
  -H "Authorization: Bearer {token}"
```

Expected: HTTP `404`.

---

## Phase 12: Report

Print a structured summary:

```
=== Selfhosted E2E API Test Results ===
URL:        {url}
Username:   {user}
Timestamp:  {ISO 8601}

Phase 0: Health Check              PASS / FAIL
Phase 1: Authentication
  Login:                           PASS / FAIL
  Token verification:              PASS / FAIL
Phase 2: Vault
  Create vault:                    PASS / FAIL
  Verify vault exists:             PASS / FAIL
Phase 3: Knowledge CRUD
  Create item 1 (platform):       PASS / FAIL
  Create item 2 (recipe):         PASS / FAIL
  Create item 3 (science):        PASS / FAIL
  Verify items:                    PASS / FAIL
Phase 4: Enrichment
  Items indexed:                   PASS / SKIPPED (AI not configured)
Phase 5: AI Features
  Search:                          PASS / FAIL / SKIPPED
  Ask (RAG):                       PASS / FAIL / SKIPPED
  Chat:                            PASS / FAIL / SKIPPED
  Chat with history:               PASS / FAIL / SKIPPED
Phase 6: Tenant Isolation
  Create test tenant:              PASS / FAIL
  Create test user:                PASS / FAIL
  Login as test user:              PASS / FAIL
  Knowledge isolation:             PASS / FAIL
  Vault isolation:                 PASS / FAIL
  Search isolation:                PASS / FAIL / SKIPPED
  Cross-tenant delete blocked:     PASS / FAIL
  SuperAdmin tenant switching:     PASS / FAIL
  Regular user bypass blocked:     PASS / FAIL
  Role-based access:               PASS / FAIL
  Cleanup isolation:               PASS / FAIL / SKIPPED (--skip-cleanup)
Phase 7: Vault Move
  Create target vault:             PASS / FAIL
  Single move (PUT):               PASS / FAIL
  Verify in target vault:          PASS / FAIL
  Verify removed from source:     PASS / FAIL
  Search finds moved item:         PASS / FAIL / SKIPPED
  Batch move:                      PASS / FAIL
  Verify all in target:            PASS / FAIL
  Move back + cleanup:             PASS / FAIL
Phase 8: Edit + Search
  Original content searchable:    PASS / FAIL / SKIPPED
  Edit content:                    PASS / FAIL / SKIPPED
  Re-indexing:                     PASS / FAIL / SKIPPED
  New content searchable:          PASS / FAIL / SKIPPED
  Old content removed:             PASS / FAIL / SKIPPED
  Chat about edit:                 PASS / FAIL / SKIPPED
Phase 9: File Attachment
  Create anchor item:              PASS / FAIL / SKIPPED
  Upload file:                     PASS / FAIL / SKIPPED
  Attach file:                     PASS / FAIL / SKIPPED
  Reprocess:                       PASS / FAIL / SKIPPED
  Re-indexing:                     PASS / FAIL / SKIPPED
  Ask about file content:          PASS / FAIL / SKIPPED
  Cleanup file test:               PASS / FAIL / SKIPPED
Phase 10: Cross-Tenant Portability
  Create target tenant:            PASS / FAIL
  Export:                          PASS / FAIL
  Import to target:                PASS / FAIL
  Verify imported data:            PASS / FAIL
  Source data unchanged:           PASS / FAIL
  Cleanup portability:             PASS / FAIL / SKIPPED (--skip-cleanup)
Phase 11: Cleanup
  Delete knowledge:                PASS / FAIL / SKIPPED (--skip-cleanup)
  Delete files:                    PASS / FAIL / SKIPPED (--skip-cleanup)
  Delete vaults:                   PASS / FAIL / SKIPPED (--skip-cleanup)
  Delete test tenants/users:       PASS / FAIL / SKIPPED (--skip-cleanup)
  Verify deletion:                 PASS / FAIL / SKIPPED (--skip-cleanup)

Overall: PASS / PARTIAL / FAIL
```

**Overall result logic:**
- **PASS**: All tests passed (including AI features)
- **PARTIAL**: Core tests passed, AI features SKIPPED (Azure AI not configured — this is a valid deployment state)
- **FAIL**: Any non-SKIPPED test failed (health, auth, vault, knowledge CRUD, tenant isolation, vault moves, or cleanup)

---

## Error Handling

- **Connection refused / timeout on health check**: Stop immediately, tell user to run `docker compose up -d` and wait for containers to be healthy
- **401 on login**: Credentials are wrong. Check `.env` values for `ADMIN_USERNAME` / `ADMIN_PASSWORD`
- **401 on subsequent calls**: Token may have expired. This shouldn't happen in a short test run — report as FAIL
- **400 errors**: Log the error body and report FAIL for that test
- **500 errors**: Log the error body, report FAIL, continue with remaining tests
- **Always attempt cleanup** even if earlier phases failed, using whatever IDs were successfully captured
