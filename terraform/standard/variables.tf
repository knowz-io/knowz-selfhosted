# =============================================================================
# GENERAL
# =============================================================================

variable "prefix" {
  description = "Resource name prefix (used for all resource naming)"
  type        = string
  default     = "sh-test"
}

variable "location" {
  description = "Location for all resources"
  type        = string
  default     = "eastus2"
}

variable "resource_group_name" {
  description = "Name of the resource group to create"
  type        = string
  default     = "rg-knowz-selfhosted"
}

variable "tags" {
  description = "Additional tags to apply to all resources (merged with default tags)"
  type        = map(string)
  default     = {}
}

# =============================================================================
# SQL
# =============================================================================

variable "sql_admin_username" {
  description = "SQL Server administrator username"
  type        = string
  default     = "sqladmin"
  sensitive   = true
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}

variable "allow_all_ips" {
  description = "Allow all IPs to access SQL Server (test/dev only, default OFF)"
  type        = bool
  default     = false
}

# =============================================================================
# AZURE OPENAI
# =============================================================================

variable "deploy_openai" {
  description = "Deploy Azure OpenAI (set to false when using external/shared OpenAI)"
  type        = bool
  default     = true
}

variable "external_openai_endpoint" {
  description = "External OpenAI endpoint (required if deploy_openai is false)"
  type        = string
  default     = ""
}

variable "external_openai_key" {
  description = "External OpenAI API key (required if deploy_openai is false)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "chat_deployment_name" {
  description = "Chat model deployment name (must match appsettings DeploymentName)"
  type        = string
  default     = "gpt-5.2-chat"
}

variable "embedding_deployment_name" {
  description = "Embedding deployment name (must match appsettings EmbeddingDeploymentName)"
  type        = string
  default     = "text-embedding-3-small"
}

variable "embedding_model_name" {
  description = "Embedding model name (text-embedding-3-small or text-embedding-3-large)"
  type        = string
  default     = "text-embedding-3-small"
}

# =============================================================================
# AZURE AI VISION
# =============================================================================

variable "deploy_vision" {
  description = "Deploy Azure AI Vision for image/diagram analysis"
  type        = bool
  default     = true
}

variable "external_vision_endpoint" {
  description = "External Azure AI Vision endpoint (required if deploy_vision is false)"
  type        = string
  default     = ""
}

variable "external_vision_key" {
  description = "External Azure AI Vision API key (required if deploy_vision is false)"
  type        = string
  sensitive   = true
  default     = ""
}

# =============================================================================
# DOCUMENT INTELLIGENCE
# =============================================================================

variable "deploy_document_intelligence" {
  description = "Deploy Azure Document Intelligence for advanced document extraction"
  type        = bool
  default     = true
}

variable "external_doc_intel_endpoint" {
  description = "External Document Intelligence endpoint (required if deploy_document_intelligence is false)"
  type        = string
  default     = ""
}

variable "external_doc_intel_key" {
  description = "External Document Intelligence API key (required if deploy_document_intelligence is false)"
  type        = string
  sensitive   = true
  default     = ""
}

# =============================================================================
# AI SEARCH
# =============================================================================

variable "search_sku" {
  description = "Azure AI Search SKU"
  type        = string
  default     = "basic"

  validation {
    condition     = contains(["free", "basic", "standard"], var.search_sku)
    error_message = "search_sku must be one of: free, basic, standard."
  }
}

variable "search_location" {
  description = "Location for AI Search (override if SKU unavailable in primary location). Uses var.location if empty."
  type        = string
  default     = ""
}

# =============================================================================
# KEY VAULT
# =============================================================================

variable "deploy_key_vault" {
  description = "Deploy Azure Key Vault for secret management (set to false for flat env var config)"
  type        = bool
  default     = true
}

# =============================================================================
# MONITORING
# =============================================================================

variable "deploy_monitoring" {
  description = "Deploy Log Analytics + Application Insights for monitoring"
  type        = bool
  default     = true
}

# =============================================================================
# STORAGE
# =============================================================================

variable "storage_allow_shared_key_access" {
  description = "Allow shared key access on storage account (set to false after migrating to Managed Identity)"
  type        = bool
  default     = true
}

# =============================================================================
# CONTAINER APPS
# =============================================================================

variable "deploy_container_apps" {
  description = "Deploy Container Apps for API, MCP, and Web"
  type        = bool
  default     = false
}

variable "image_tag" {
  description = "Container image tag (e.g., latest, v1.0.0)"
  type        = string
  default     = "latest"
}

variable "registry_server" {
  description = "Container registry server (GHCR)"
  type        = string
  default     = "ghcr.io"
}

variable "registry_username" {
  description = "Container registry username (only needed for private GHCR images)"
  type        = string
  default     = ""
}

variable "registry_password" {
  description = "Container registry password (only needed for private GHCR images)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "api_key" {
  description = "API key for selfhosted authentication (auto-generated if empty)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "jwt_secret" {
  description = "JWT secret for selfhosted token signing (auto-generated if empty)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "admin_password" {
  description = "SuperAdmin password for initial setup"
  type        = string
  sensitive   = true
  default     = "changeme"
}

variable "ca_deployment_name" {
  description = "Chat model deployment name for Container Apps config (defaults to chat_deployment_name)"
  type        = string
  default     = ""
}

variable "ca_embedding_deployment_name" {
  description = "Embedding deployment name for Container Apps config"
  type        = string
  default     = "text-embedding-3-small"
}
