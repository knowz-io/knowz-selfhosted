# main.tf — Provider configuration, resource group, and shared locals

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}

# -----------------------------------------------------------------------------
# Resource Group
# -----------------------------------------------------------------------------

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.prefix}"
  location = var.location
  tags     = local.effective_tags
}

# -----------------------------------------------------------------------------
# Shared Locals
# -----------------------------------------------------------------------------

resource "random_string" "unique" {
  length  = 13
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "random_uuid" "api_key" {
  count = var.api_key == "" ? 1 : 0
}

resource "random_password" "jwt_secret" {
  count   = var.jwt_secret == "" ? 1 : 0
  length  = 64
  special = false
}

locals {
  unique_suffix      = random_string.unique.result
  sql_server_name    = "${var.prefix}-sql-${local.unique_suffix}"
  storage_prefix     = lower(substr(replace(var.prefix, "-", ""), 0, 8))
  storage_account_name = lower("${local.storage_prefix}st${substr(local.unique_suffix, 0, 12)}")
  kv_prefix          = lower(substr(replace(var.prefix, "-", ""), 0, 8))
  key_vault_name     = "${local.kv_prefix}kv${substr(local.unique_suffix, 0, 8)}"
  mcp_service_key    = "selfhosted-enterprise-mcp-service-key-${local.unique_suffix}"

  effective_api_key    = var.api_key != "" ? var.api_key : random_uuid.api_key[0].result
  effective_jwt_secret = var.jwt_secret != "" ? var.jwt_secret : random_password.jwt_secret[0].result

  # Effective endpoints (local or external)
  effective_openai_endpoint   = var.deploy_openai ? azurerm_cognitive_account.openai[0].endpoint : var.external_openai_endpoint
  effective_openai_key        = var.deploy_openai ? azurerm_cognitive_account.openai[0].primary_access_key : var.external_openai_key
  effective_docintel_endpoint = var.deploy_document_intelligence ? azurerm_cognitive_account.docintel[0].endpoint : var.external_docintel_endpoint
  effective_docintel_key      = var.deploy_document_intelligence ? azurerm_cognitive_account.docintel[0].primary_access_key : var.external_docintel_key
  effective_vision_endpoint   = var.deploy_vision ? azurerm_cognitive_account.vision[0].endpoint : var.external_vision_endpoint
  effective_vision_key        = var.deploy_vision ? azurerm_cognitive_account.vision[0].primary_access_key : var.external_vision_key

  # SQL connection string using AAD Managed Identity authentication (no password)
  sql_connection_string = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=McpKnowledge;Authentication=Active Directory Managed Identity;User Id=${azurerm_user_assigned_identity.main.client_id};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

  # Storage connection string (shared key until MI blob support in app code)
  storage_connection_string = "DefaultEndpointsProtocol=https;AccountName=${azurerm_storage_account.main.name};AccountKey=${azurerm_storage_account.main.primary_access_key};EndpointSuffix=core.windows.net"

  default_tags = {
    project      = "knowz-selfhosted"
    environment  = var.prefix
    tier         = "enterprise"
    "managed-by" = "terraform"
  }
  effective_tags = merge(local.default_tags, var.tags)
}

# Current Azure client info (for tenant_id, etc.)
data "azurerm_client_config" "current" {}
