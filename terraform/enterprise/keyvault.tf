# keyvault.tf — Key Vault (purge protection, RBAC, private endpoint) + secrets

# -----------------------------------------------------------------------------
# Key Vault (hardened: purge protection, RBAC auth, private endpoint)
# -----------------------------------------------------------------------------

resource "azurerm_key_vault" "main" {
  name                          = local.key_vault_name
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  soft_delete_retention_days    = 90
  purge_protection_enabled      = true
  public_network_access_enabled = false
  tags                          = local.effective_tags

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
  }
}

# -----------------------------------------------------------------------------
# Private Endpoint: Key Vault
# -----------------------------------------------------------------------------

resource "azurerm_private_endpoint" "keyvault" {
  name                = "${var.prefix}-pe-kv"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = azurerm_subnet.private_endpoints.id
  tags                = local.effective_tags

  private_service_connection {
    name                           = "${var.prefix}-plsc-kv"
    private_connection_resource_id = azurerm_key_vault.main.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.zones["keyvault"].id]
  }
}

# -----------------------------------------------------------------------------
# Key Vault Secrets
# -----------------------------------------------------------------------------

resource "azurerm_key_vault_secret" "sql_connection" {
  name         = "ConnectionStrings--McpDb"
  value        = local.sql_connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "search_endpoint" {
  name         = "AzureAISearch--Endpoint"
  value        = "https://${azurerm_search_service.main.name}.search.windows.net"
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "search_key" {
  name         = "AzureAISearch--ApiKey"
  value        = azurerm_search_service.main.primary_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "openai_endpoint" {
  name         = "AzureOpenAI--Endpoint"
  value        = local.effective_openai_endpoint
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "openai_key" {
  name         = "AzureOpenAI--ApiKey"
  value        = local.effective_openai_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "docintel_endpoint" {
  name         = "AzureDocumentIntelligence--Endpoint"
  value        = local.effective_docintel_endpoint
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "docintel_key" {
  name         = "AzureDocumentIntelligence--ApiKey"
  value        = local.effective_docintel_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "vision_endpoint" {
  name         = "AzureAIVision--Endpoint"
  value        = local.effective_vision_endpoint
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "vision_key" {
  name         = "AzureAIVision--ApiKey"
  value        = local.effective_vision_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

# SH_ENTERPRISE_BICEP_HARDENING §MI-swap: Storage--Azure--ConnectionString RETIRED.
# App reads Storage--Azure--AccountUrl + uses DefaultAzureCredential.
resource "azurerm_key_vault_secret" "storage_account_url" {
  name         = "Storage--Azure--AccountUrl"
  value        = azurerm_storage_account.main.primary_blob_endpoint
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "appinsights_connection" {
  name         = "ApplicationInsights--ConnectionString"
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "api_key" {
  name         = "SelfHosted--ApiKey"
  value        = local.effective_api_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "jwt_secret" {
  name         = "SelfHosted--JwtSecret"
  value        = local.effective_jwt_secret
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

resource "azurerm_key_vault_secret" "admin_password" {
  name         = "SelfHosted--SuperAdminPassword"
  value        = var.admin_password
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

# MCP service key KV secret — the random_uuid resource itself is declared in main.tf
# so local.mcp_service_key can reference it. SH_ENTERPRISE_BICEP_HARDENING §Rule 4.
# Previous pattern was deterministic (uniqueString per RG name → predictable).
resource "azurerm_key_vault_secret" "mcp_service_key" {
  name         = "MCP--ServiceKey"
  value        = local.mcp_service_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.keyvault_secrets_user, time_sleep.wait_kv_rbac]
}

# Data Protection master key — SH_ENTERPRISE_BICEP_HARDENING §Rule 9.
# Wraps the ASP.NET Core DP key ring persisted to the dp-keys blob container.
# Name MUST match app-side default at selfhosted/src/Knowz.SelfHosted.API/Program.cs:50.
resource "azurerm_key_vault_key" "dp_master_key" {
  name         = "selfhosted-dp-key"
  key_vault_id = azurerm_key_vault.main.id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]

  rotation_policy {
    automatic {
      time_after_creation = "P90D"
    }
    expire_after         = "P2Y"
    notify_before_expiry = "P30D"
  }

  depends_on = [
    azurerm_role_assignment.kv_deployer_secrets_officer,
    time_sleep.wait_kv_rbac,
  ]
}
