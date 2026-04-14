# variables.tf — All input variables for enterprise self-hosted deployment

# -----------------------------------------------------------------------------
# Core
# -----------------------------------------------------------------------------

variable "prefix" {
  description = "Resource name prefix (2-8 chars, used for all resource naming)"
  type        = string

  validation {
    condition     = length(var.prefix) >= 2 && length(var.prefix) <= 8
    error_message = "Prefix must be between 2 and 8 characters."
  }
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
}

variable "tags" {
  description = "Tags applied to all resources (merged with default tags)"
  type        = map(string)
  default     = {}
}

# -----------------------------------------------------------------------------
# AAD / SQL Administration
# -----------------------------------------------------------------------------

variable "aad_admin_object_id" {
  description = "AAD Object ID for SQL administrator (user or group)"
  type        = string
}

variable "aad_admin_display_name" {
  description = "AAD display name for SQL administrator"
  type        = string
}

# -----------------------------------------------------------------------------
# Application Secrets
# -----------------------------------------------------------------------------

variable "admin_password" {
  description = "SuperAdmin password for Knowz application initial setup"
  type        = string
  sensitive   = true
}

variable "api_key" {
  description = "API key for the selfhosted API. Auto-generated if empty."
  type        = string
  sensitive   = true
  default     = ""
}

variable "jwt_secret" {
  description = "JWT signing secret (min 32 chars). Auto-generated if empty."
  type        = string
  sensitive   = true
  default     = ""
}

# -----------------------------------------------------------------------------
# Azure OpenAI
# -----------------------------------------------------------------------------

variable "deploy_openai" {
  description = "Deploy Azure OpenAI (false = use external OpenAI endpoint)"
  type        = bool
  default     = true
}

variable "external_openai_endpoint" {
  description = "External OpenAI endpoint (required when deploy_openai is false)"
  type        = string
  default     = ""
}

variable "external_openai_key" {
  description = "External OpenAI API key (required when deploy_openai is false)"
  type        = string
  sensitive   = true
  default     = ""
}

# --- Existing Resource Lookup (OpenAI) ---

variable "existing_openai_resource_name" {
  description = "Name of an existing Azure OpenAI/AIServices resource to reuse (alternative to deploying new). Leave empty to deploy new or use external endpoint."
  type        = string
  default     = ""
}

variable "existing_openai_resource_group" {
  description = "Resource group of the existing OpenAI resource (required if existing_openai_resource_name is set)"
  type        = string
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
  description = "Embedding model name"
  type        = string
  default     = "text-embedding-3-small"
}

# -----------------------------------------------------------------------------
# Azure AI Vision
# -----------------------------------------------------------------------------

variable "deploy_vision" {
  description = "Deploy Azure AI Vision for image/diagram analysis (caption, tags, objects, OCR)"
  type        = bool
  default     = true
}

variable "external_vision_endpoint" {
  description = "External Azure AI Vision endpoint (required when deploy_vision is false)"
  type        = string
  default     = ""
}

variable "external_vision_key" {
  description = "External Azure AI Vision API key (required when deploy_vision is false)"
  type        = string
  sensitive   = true
  default     = ""
}

# --- Existing Resource Lookup (Vision) ---

variable "existing_vision_resource_name" {
  description = "Name of an existing Azure AI Vision resource to reuse (alternative to deploying new). Leave empty to deploy new or use external endpoint."
  type        = string
  default     = ""
}

variable "existing_vision_resource_group" {
  description = "Resource group of the existing Vision resource (required if existing_vision_resource_name is set)"
  type        = string
  default     = ""
}

# -----------------------------------------------------------------------------
# Document Intelligence
# -----------------------------------------------------------------------------

variable "deploy_document_intelligence" {
  description = "Deploy Azure Document Intelligence for advanced document extraction"
  type        = bool
  default     = true
}

variable "external_docintel_endpoint" {
  description = "External Document Intelligence endpoint (required when deploy_document_intelligence is false)"
  type        = string
  default     = ""
}

variable "external_docintel_key" {
  description = "External Document Intelligence API key (required when deploy_document_intelligence is false)"
  type        = string
  sensitive   = true
  default     = ""
}

# --- Existing Resource Lookup (Document Intelligence) ---

variable "existing_docintel_resource_name" {
  description = "Name of an existing Azure Document Intelligence resource to reuse (alternative to deploying new). Leave empty to deploy new or use external endpoint."
  type        = string
  default     = ""
}

variable "existing_docintel_resource_group" {
  description = "Resource group of the existing Document Intelligence resource (required if existing_docintel_resource_name is set)"
  type        = string
  default     = ""
}

# -----------------------------------------------------------------------------
# Azure AI Search
# -----------------------------------------------------------------------------

variable "search_sku" {
  description = "Azure AI Search SKU (enterprise requires standard or higher for private endpoint support)"
  type        = string
  default     = "standard"

  validation {
    condition     = contains(["standard", "standard2", "standard3"], var.search_sku)
    error_message = "Enterprise search SKU must be standard, standard2, or standard3 (private endpoints require standard+)."
  }
}

# -----------------------------------------------------------------------------
# Container Apps
# -----------------------------------------------------------------------------

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
  description = "Container registry username (empty = public GHCR)"
  type        = string
  default     = ""
}

variable "registry_password" {
  description = "Container registry password (empty = public GHCR)"
  type        = string
  sensitive   = true
  default     = ""
}
