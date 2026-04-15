# identity.tf — User-assigned managed identity and all role assignments

# -----------------------------------------------------------------------------
# User-Assigned Managed Identity
# -----------------------------------------------------------------------------

resource "azurerm_user_assigned_identity" "main" {
  name                = "${var.prefix}-identity"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.effective_tags
}

# -----------------------------------------------------------------------------
# Role Assignments
# -----------------------------------------------------------------------------

# Search: Index Data Contributor
resource "azurerm_role_assignment" "search_index_data_contributor" {
  scope                = azurerm_search_service.main.id
  role_definition_name = "Search Index Data Contributor"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
  principal_type       = "ServicePrincipal"
}

# Search: Service Contributor
resource "azurerm_role_assignment" "search_service_contributor" {
  scope                = azurerm_search_service.main.id
  role_definition_name = "Search Service Contributor"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
  principal_type       = "ServicePrincipal"
}

# OpenAI: Cognitive Services OpenAI Contributor (only when deploying OpenAI locally)
resource "azurerm_role_assignment" "openai_contributor" {
  count                = var.deploy_openai ? 1 : 0
  scope                = azurerm_cognitive_account.openai[0].id
  role_definition_name = "Cognitive Services OpenAI Contributor"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
  principal_type       = "ServicePrincipal"
}

# Storage: Blob Data Contributor
resource "azurerm_role_assignment" "storage_blob_data_contributor" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
  principal_type       = "ServicePrincipal"
}

# Key Vault: Secrets User (managed identity — read secrets at runtime)
resource "azurerm_role_assignment" "keyvault_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
  principal_type       = "ServicePrincipal"
}

# Key Vault: Secrets Officer (deployer — create secrets at deploy time)
# RBAC-enabled vaults require explicit data-plane role assignment even for
# subscription Owners. Grant the deploying identity Secrets Officer so
# Terraform can create the secrets below. Wait for RBAC propagation (~45s).
resource "azurerm_role_assignment" "kv_deployer_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "time_sleep" "wait_kv_rbac" {
  depends_on      = [azurerm_role_assignment.kv_deployer_secrets_officer]
  create_duration = "45s"
}
