# =============================================================================
# SQL SERVER + DATABASE + FIREWALL RULES
# =============================================================================

resource "azurerm_mssql_server" "main" {
  name                          = "${var.prefix}-sql-${local.unique_suffix}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "12.0"
  administrator_login           = var.sql_admin_username
  administrator_login_password  = var.sql_admin_password
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  tags = local.tags
}

# Allow Azure services to access SQL Server
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAllAzureIps"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Allow all IPs for testing (local dev, CI/CD, EF migrations) -- opt-in only
resource "azurerm_mssql_firewall_rule" "allow_all" {
  count = var.allow_all_ips ? 1 : 0

  name             = "AllowAllIps-TestOnly"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}

# Single database for self-hosted MCP (NOT the dual KnowzMaster/KnowzKnowledge pattern)
resource "azurerm_mssql_database" "mcp" {
  name      = "McpKnowledge"
  server_id = azurerm_mssql_server.main.id
  collation = "SQL_Latin1_General_CP1_CI_AS"
  sku_name  = "Basic"

  max_size_gb    = 2
  zone_redundant = false

  tags = local.tags
}
