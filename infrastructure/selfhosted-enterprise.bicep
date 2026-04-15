// infrastructure/selfhosted-enterprise.bicep
// Enterprise-grade Bicep template for Knowz Self-Hosted deployment.
// Designed to comply with Azure Landing Zone CSPM/CSE security policies.
//
// Key differences from standard template (selfhosted-test.bicep):
//   - VNet with private endpoints for ALL data-plane services
//   - Azure Front Door (Premium) with WAF policy + managed rule sets as single ingress
//   - AAD-only SQL authentication (no SQL admin password)
//   - Storage: GRS, network ACLs deny-all (shared key access until MI blob support)
//   - Key Vault: purge protection, RBAC auth, private endpoint
//   - All Cognitive Services: publicNetworkAccess=Disabled
//   - SQL: publicNetworkAccess=Disabled, Defender, auditing, LTR backup
//   - Container Apps Environment: internal-only (VNet-injected)
//   - Private DNS zones for all 6 service types
//   - Log Analytics + diagnostics on all major resources
//
// Usage:
//   az group create -n rg-knowz-enterprise -l eastus2
//   az deployment group create -g rg-knowz-enterprise \
//     -f infrastructure/selfhosted-enterprise.bicep \
//     -p prefix='kze' location='eastus2' \
//        aadAdminObjectId='<guid>' aadAdminDisplayName='DBA Group' \
//        adminPassword='<secure>'

// ============================================================================
// PARAMETERS
// ============================================================================

@description('Resource name prefix (2-8 chars, used for all resource naming)')
@minLength(2)
@maxLength(8)
param prefix string

@description('Object ID of the deploying user/SP (grants Key Vault Secrets Officer for secret creation). Populated by deploy script.')
param deployerObjectId string = ''

@description('Azure region for all resources')
param location string

@description('AAD Object ID for SQL administrator (user or group)')
param aadAdminObjectId string

@description('AAD display name for SQL administrator')
param aadAdminDisplayName string

@description('SuperAdmin password for Knowz application initial setup')
@secure()
param adminPassword string

@description('Tags applied to all resources (organizations typically add their own via Azure Policy)')
param tags object = {}

@description('Deploy Azure OpenAI (false = use external OpenAI endpoint)')
param deployOpenAI bool = true

@description('External OpenAI endpoint (required when deployOpenAI is false)')
param externalOpenAiEndpoint string = ''

@description('External OpenAI API key (required when deployOpenAI is false)')
@secure()
param externalOpenAiKey string = ''

@description('Name of existing Azure OpenAI resource to reuse (leave empty to deploy new or use external)')
param existingOpenAiName string = ''

@description('Resource group of existing OpenAI resource (defaults to current RG)')
param existingOpenAiResourceGroup string = ''

@description('Chat model deployment name (must match appsettings DeploymentName)')
param chatDeploymentName string = 'gpt-5.2-chat'

@description('Embedding deployment name (must match appsettings EmbeddingDeploymentName)')
param embeddingDeploymentName string = 'text-embedding-3-small'

@description('Deploy Azure AI Vision for image/diagram analysis (caption, tags, objects, OCR)')
param deployVision bool = true

@description('External Azure AI Vision endpoint (required when deployVision is false)')
param externalVisionEndpoint string = ''

@description('External Azure AI Vision API key (required when deployVision is false)')
@secure()
param externalVisionKey string = ''

@description('Name of existing Azure AI Vision resource to reuse (leave empty to deploy new or use external)')
param existingVisionName string = ''

@description('Resource group of existing Vision resource (defaults to current RG)')
param existingVisionResourceGroup string = ''

@description('Deploy Azure Document Intelligence for advanced document extraction')
param deployDocumentIntelligence bool = true

@description('External Document Intelligence endpoint (required when deployDocumentIntelligence is false)')
param externalDocIntelEndpoint string = ''

@description('External Document Intelligence API key (required when deployDocumentIntelligence is false)')
@secure()
param externalDocIntelKey string = ''

@description('Name of existing Document Intelligence resource to reuse (leave empty to deploy new or use external)')
param existingDocIntelName string = ''

@description('Resource group of existing Document Intelligence resource (defaults to current RG)')
param existingDocIntelResourceGroup string = ''

@description('Azure AI Search SKU (enterprise requires standard or higher for private endpoint support)')
@allowed(['standard', 'standard2', 'standard3'])
param searchSku string = 'standard'

@description('Container image tag (e.g., latest, v1.0.0)')
param imageTag string = 'latest'

@description('Container registry username (empty = public GHCR)')
param registryUsername string = ''

@description('Container registry password (empty = public GHCR)')
@secure()
param registryPassword string = ''

@secure()
@description('API key for the selfhosted API. Auto-generated if not provided.')
param apiKey string = newGuid()

@secure()
@description('JWT signing secret. Must be at least 32 characters. Auto-generated if not provided.')
param jwtSecret string = '${newGuid()}${newGuid()}'

// ============================================================================
// VARIABLES
// ============================================================================

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = '${prefix}-sql-${uniqueSuffix}'
var storagePrefix = toLower(take(replace(prefix, '-', ''), 8))
var storageAccountName = toLower('${storagePrefix}st${take(uniqueSuffix, 12)}')
var kvPrefix = toLower(take(replace(prefix, '-', ''), 8))
var keyVaultName = '${kvPrefix}kv${take(uniqueSuffix, 8)}'
var mcpServiceKey = 'selfhosted-enterprise-mcp-service-key-${uniqueString(resourceGroup().id)}'

var registryServer = 'ghcr.io'
var embeddingModelName = 'text-embedding-3-small'

// Resource tags
var defaultTags = {
  project: 'knowz-selfhosted'
  environment: prefix
  tier: 'enterprise'
  'managed-by': 'bicep'
}
var effectiveTags = union(defaultTags, tags)

// Effective endpoints (local or external)
var effectiveOpenAiEndpoint = deployOpenAI ? cognitiveServices.properties.endpoint : externalOpenAiEndpoint
var effectiveVisionEndpoint = deployVision ? visionService.properties.endpoint : externalVisionEndpoint
var effectiveDocIntelEndpoint = deployDocumentIntelligence ? documentIntelligence.properties.endpoint : externalDocIntelEndpoint

// SQL connection string using AAD Managed Identity authentication (no password)
var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=McpKnowledge;Authentication=Active Directory Managed Identity;User Id=${managedIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

// TODO: Switch to managed identity when app code supports DefaultAzureCredential for blob access
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

// App Insights connection string
var appInsightsConnectionString = appInsights.properties.ConnectionString

// ============================================================================
// NETWORK SECURITY GROUPS
// ============================================================================

resource containerAppsNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: '${prefix}-container-apps-nsg'
  location: location
  tags: effectiveTags
  properties: {
    securityRules: [
      {
        name: 'AllowFrontDoorInbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'AzureFrontDoor.Backend'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowAllOutbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

resource privateEndpointsNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: '${prefix}-private-endpoints-nsg'
  location: location
  tags: effectiveTags
  properties: {
    securityRules: [
      {
        name: 'DenyAllInbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowAllOutbound'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

// ============================================================================
// VIRTUAL NETWORK
// ============================================================================

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: '${prefix}-vnet'
  location: location
  tags: effectiveTags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'container-apps'
        properties: {
          addressPrefix: '10.0.0.0/23'
          networkSecurityGroup: {
            id: containerAppsNsg.id
          }
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: '10.0.2.0/24'
          networkSecurityGroup: {
            id: privateEndpointsNsg.id
          }
        }
      }
    ]
  }
}

// ============================================================================
// PRIVATE DNS ZONES (6 zones for all private endpoint services)
// ============================================================================

var privateDnsZones = [
  'privatelink.database.windows.net'
  'privatelink.blob.core.windows.net'
  'privatelink.vaultcore.azure.net'
  'privatelink.openai.azure.com'
  'privatelink.cognitiveservices.azure.com'
  'privatelink.search.windows.net'
]

resource dnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [for zone in privateDnsZones: {
  name: zone
  location: 'global'
  tags: effectiveTags
}]

resource dnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [for (zone, i) in privateDnsZones: {
  parent: dnsZones[i]
  name: '${prefix}-link-${i}'
  location: 'global'
  tags: effectiveTags
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}]

// ============================================================================
// MANAGED IDENTITY
// ============================================================================

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-identity'
  location: location
  tags: effectiveTags
}

// ============================================================================
// LOG ANALYTICS WORKSPACE
// ============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${prefix}-logs'
  location: location
  tags: effectiveTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: 5
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ============================================================================
// APPLICATION INSIGHTS
// ============================================================================

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-appinsights'
  location: location
  tags: effectiveTags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ============================================================================
// STORAGE ACCOUNT (hardened: GRS, no public blob, deny-all network, shared key until MI support)
// ============================================================================

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: effectiveTags
  sku: {
    name: 'Standard_GRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // TODO: Switch to managed identity when app code supports DefaultAzureCredential for blob access
    allowSharedKeyAccess: true
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'selfhosted-files'
  properties: {
    publicAccess: 'None'
  }
}

// Private endpoint: Storage Blob
resource storagePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${prefix}-pe-storage'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-storage'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource storageDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: storagePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[1].id // privatelink.blob.core.windows.net
        }
      }
    ]
  }
}

// Storage diagnostics
resource storageDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${prefix}-storage-diagnostics'
  scope: blobService
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'StorageRead'
        enabled: true
      }
      {
        category: 'StorageWrite'
        enabled: true
      }
      {
        category: 'StorageDelete'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Transaction'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// SQL SERVER + DATABASE (AAD-only auth, private endpoint, Defender, auditing)
// ============================================================================

// The managed identity is set as SQL AAD admin so it can run EF Core migrations at startup
// and authenticate at runtime via Active Directory Managed Identity.
// The user-provided aadAdminObjectId is granted access via a deployment script below.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: effectiveTags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: managedIdentity.name
      sid: managedIdentity.properties.principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource mcpDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'McpKnowledge'
  location: location
  tags: effectiveTags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    zoneRedundant: false
  }
}

// Short-term backup retention: 35 days
resource sqlBackupShortTerm 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = {
  parent: mcpDb
  name: 'default'
  properties: {
    retentionDays: 35
  }
}

// Long-term backup retention: weekly for 26 weeks (6 months)
resource sqlBackupLongTerm 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01-preview' = {
  parent: mcpDb
  name: 'default'
  properties: {
    weeklyRetention: 'P26W'
  }
}

// SQL Server auditing to Log Analytics
resource sqlAudit 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

// Defender for SQL
resource sqlDefender 'Microsoft.Sql/servers/advancedThreatProtectionSettings@2023-08-01-preview' = {
  parent: sqlServer
  name: 'Default'
  properties: {
    state: 'Enabled'
  }
}

// SQL Vulnerability Assessment (requires Defender for SQL)
resource sqlVulnAssessment 'Microsoft.Sql/servers/vulnerabilityAssessments@2023-08-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    storageContainerPath: '${storageAccount.properties.primaryEndpoints.blob}vulnerability-assessments'
    recurringScans: {
      isEnabled: true
      emailSubscriptionAdmins: true
    }
  }
  dependsOn: [sqlDefender]
}

// Deployment script: grant the user-provided AAD admin access to the SQL database.
// The managed identity is the SQL AAD admin (for migrations), so we use it to run this script
// which grants db_owner to the user-specified admin group/user.
resource sqlPermissionScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: '${prefix}-sql-permission-script'
  location: location
  tags: effectiveTags
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    azPowerShellVersion: '11.0'
    retentionInterval: 'PT1H'
    timeout: 'PT10M'
    arguments: '-SqlServerFqdn ${sqlServer.properties.fullyQualifiedDomainName} -DatabaseName McpKnowledge -AadAdminObjectId ${aadAdminObjectId} -AadAdminDisplayName \'${aadAdminDisplayName}\''
    scriptContent: '''
      param($SqlServerFqdn, $DatabaseName, $AadAdminObjectId, $AadAdminDisplayName)
      Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser
      $token = (Get-AzAccessToken -ResourceUrl "https://database.windows.net/").Token
      $query = @"
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$AadAdminDisplayName')
        BEGIN
          CREATE USER [$AadAdminDisplayName] WITH SID = $(CONVERT(varbinary(16), '$AadAdminObjectId')), TYPE = E;
          ALTER ROLE db_owner ADD MEMBER [$AadAdminDisplayName];
        END
"@
      Invoke-Sqlcmd -ServerInstance $SqlServerFqdn -Database $DatabaseName -AccessToken $token -Query $query
    '''
  }
  dependsOn: [mcpDb]
}

// SQL diagnostics (database-level)
resource sqlDbDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${prefix}-sqldb-diagnostics'
  scope: mcpDb
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
      {
        category: 'QueryStoreRuntimeStatistics'
        enabled: true
      }
      {
        category: 'Errors'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
      }
    ]
  }
}

// Private endpoint: SQL Server
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${prefix}-pe-sql'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-sql'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource sqlDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: sqlPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[0].id // privatelink.database.windows.net
        }
      }
    ]
  }
}

// ============================================================================
// KEY VAULT (hardened: purge protection, RBAC, private endpoint)
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: effectiveTags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

// Key Vault Secrets User role for Managed Identity (read secrets at runtime)
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets Officer role for the deploying user/SP (create secrets at deploy time)
// RBAC-enabled vaults require explicit data-plane role assignment even for subscription
// Owners. Without this, the deployment fails with 403 on every secretXxx resource.
// Populated by the deploy script from `az ad signed-in-user show --query id -o tsv`.
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource kvSecretsOfficerDeployerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerObjectId)) {
  name: guid(keyVault.id, deployerObjectId, keyVaultSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
    principalId: deployerObjectId
    principalType: 'User'
  }
}

// Private endpoint: Key Vault
resource kvPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${prefix}-pe-kv'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-kv'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource kvDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: kvPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[2].id // privatelink.vaultcore.azure.net
        }
      }
    ]
  }
}

// Key Vault diagnostics
resource kvDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${prefix}-kv-diagnostics'
  scope: keyVault
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// KEY VAULT SECRETS
// ============================================================================

resource secretSqlConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ConnectionStrings--McpDb'
  properties: {
    value: sqlConnectionString
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretSearchEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAISearch--Endpoint'
  properties: {
    value: 'https://${searchService.name}.search.windows.net'
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretSearchKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAISearch--ApiKey'
  properties: {
    value: searchService.listAdminKeys().primaryKey
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretOpenAiEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAI--Endpoint'
  properties: {
    value: effectiveOpenAiEndpoint
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretOpenAiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAI--ApiKey'
  properties: {
    value: deployOpenAI ? cognitiveServices.listKeys().key1 : externalOpenAiKey
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretOpenAiDeploymentName 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAI--DeploymentName'
  properties: {
    value: chatDeploymentName
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretOpenAiEmbeddingDeploymentName 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureOpenAI--EmbeddingDeploymentName'
  properties: {
    value: embeddingDeploymentName
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretDocIntelEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureDocumentIntelligence--Endpoint'
  properties: {
    value: effectiveDocIntelEndpoint
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretDocIntelApiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureDocumentIntelligence--ApiKey'
  properties: {
    value: deployDocumentIntelligence ? documentIntelligence.listKeys().key1 : externalDocIntelKey
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretVisionEndpoint 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAIVision--Endpoint'
  properties: {
    value: effectiveVisionEndpoint
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretVisionApiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAIVision--ApiKey'
  properties: {
    value: deployVision ? visionService.listKeys().key1 : externalVisionKey
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretStorageConnection 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Storage--Azure--ConnectionString'
  properties: {
    value: storageConnectionString
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretAppInsights 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ApplicationInsights--ConnectionString'
  properties: {
    value: appInsightsConnectionString
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretApiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SelfHosted--ApiKey'
  properties: {
    value: apiKey
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretJwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SelfHosted--JwtSecret'
  properties: {
    value: jwtSecret
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

resource secretAdminPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SelfHosted--SuperAdminPassword'
  properties: {
    value: adminPassword
  }
  dependsOn: [kvSecretsOfficerDeployerRole]
}

// ============================================================================
// AZURE AI SEARCH (private endpoint, public access disabled)
// ============================================================================

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: '${prefix}-search-${location}'
  location: location
  tags: effectiveTags
  sku: {
    name: searchSku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'disabled'
  }
}

// Private endpoint: AI Search
resource searchPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: '${prefix}-pe-search'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-search'
        properties: {
          privateLinkServiceId: searchService.id
          groupIds: [
            'searchService'
          ]
        }
      }
    ]
  }
}

resource searchDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: searchPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[5].id // privatelink.search.windows.net
        }
      }
    ]
  }
}

// ============================================================================
// AZURE OPENAI (private endpoint, public access disabled)
// ============================================================================

resource cognitiveServices 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (deployOpenAI) {
  name: '${prefix}-openai-${location}'
  location: location
  tags: effectiveTags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    customSubDomainName: '${prefix}-openai-${location}'
    networkAcls: {
      defaultAction: 'Deny'
    }
  }
}

resource deploymentChat 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = if (deployOpenAI) {
  parent: cognitiveServices
  name: chatDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.2-chat'
      version: '2025-12-11'
    }
  }
}

resource deploymentMini 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = if (deployOpenAI) {
  parent: cognitiveServices
  name: 'gpt-5-mini'
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5-mini'
      version: '2025-08-07'
    }
  }
  dependsOn: [deploymentChat]
}

resource deploymentEmbedding 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = if (deployOpenAI) {
  parent: cognitiveServices
  name: embeddingDeploymentName
  sku: {
    name: 'Standard'
    capacity: 5
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModelName
      version: '1'
    }
  }
  dependsOn: [deploymentMini]
}

// Private endpoint: OpenAI
resource openAiPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = if (deployOpenAI) {
  name: '${prefix}-pe-openai'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-openai'
        properties: {
          privateLinkServiceId: cognitiveServices.id
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
}

resource openAiDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (deployOpenAI) {
  parent: openAiPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[3].id // privatelink.openai.azure.com
        }
      }
    ]
  }
}

// ============================================================================
// DOCUMENT INTELLIGENCE (private endpoint, public access disabled)
// ============================================================================

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (deployDocumentIntelligence) {
  name: '${prefix}-docintel-${location}'
  location: location
  tags: effectiveTags
  kind: 'FormRecognizer'
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    customSubDomainName: '${prefix}-docintel-${location}'
    networkAcls: {
      defaultAction: 'Deny'
    }
  }
}

// Private endpoint: Document Intelligence
resource docIntelPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = if (deployDocumentIntelligence) {
  name: '${prefix}-pe-docintel'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-docintel'
        properties: {
          privateLinkServiceId: documentIntelligence.id
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
}

resource docIntelDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (deployDocumentIntelligence) {
  parent: docIntelPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[4].id // privatelink.cognitiveservices.azure.com
        }
      }
    ]
  }
}

// ============================================================================
// AZURE AI VISION (private endpoint, public access disabled)
// ============================================================================

resource visionService 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (deployVision) {
  name: '${prefix}-vision-${location}'
  location: location
  tags: effectiveTags
  kind: 'ComputerVision'
  sku: {
    name: 'S1'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    customSubDomainName: '${prefix}-vision-${location}'
    networkAcls: {
      defaultAction: 'Deny'
    }
  }
}

// Private endpoint: Azure AI Vision
resource visionPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = if (deployVision) {
  name: '${prefix}-pe-vision'
  location: location
  tags: effectiveTags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-plsc-vision'
        properties: {
          privateLinkServiceId: visionService.id
          groupIds: [
            'account'
          ]
        }
      }
    ]
  }
}

resource visionDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (deployVision) {
  parent: visionPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'config'
        properties: {
          privateDnsZoneId: dnsZones[4].id // privatelink.cognitiveservices.azure.com (shared with DocIntel)
        }
      }
    ]
  }
}

// ============================================================================
// ROLE ASSIGNMENTS (Managed Identity -> Azure Services)
// ============================================================================

// Built-in role definition IDs
var cognitiveServicesOpenAIContributorRoleId = 'a001fd3d-188f-4b5d-821b-7da978bf7442'
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
var searchServiceContributorRoleId = '7ca78c08-252a-4471-8644-bb5ff32d4ba0'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Search: Index Data Contributor
resource searchIndexDataRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, managedIdentity.id, searchIndexDataContributorRoleId)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Search: Service Contributor
resource searchServiceRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, managedIdentity.id, searchServiceContributorRoleId)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchServiceContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// OpenAI: Cognitive Services OpenAI Contributor
resource openAiRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployOpenAI) {
  name: guid(cognitiveServices.id, managedIdentity.id, cognitiveServicesOpenAIContributorRoleId)
  scope: cognitiveServices
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage: Blob Data Contributor
resource storageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentity.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// CONTAINER APPS ENVIRONMENT (VNet-injected, internal only)
// ============================================================================

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-cae'
  location: location
  tags: effectiveTags
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: vnet.properties.subnets[0].id
      internal: true
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

// ============================================================================
// CONTAINER APPS (API, MCP, Web) — internal ingress, managed identity
// ============================================================================

// ---- API Container App ----
resource apiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-api'
  location: location
  tags: effectiveTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: empty(registryUsername) ? [] : [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: concat(empty(registryUsername) ? [] : [
        {
          name: 'registry-password'
          value: registryPassword
        }
      ], [
        {
          name: 'sql-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/ConnectionStrings--McpDb'
          identity: managedIdentity.id
        }
        {
          name: 'openai-endpoint'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureOpenAI--Endpoint'
          identity: managedIdentity.id
        }
        {
          name: 'openai-apikey'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureOpenAI--ApiKey'
          identity: managedIdentity.id
        }
        {
          name: 'search-endpoint'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAISearch--Endpoint'
          identity: managedIdentity.id
        }
        {
          name: 'search-apikey'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAISearch--ApiKey'
          identity: managedIdentity.id
        }
        {
          name: 'storage-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/Storage--Azure--ConnectionString'
          identity: managedIdentity.id
        }
        {
          name: 'selfhosted-apikey'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/SelfHosted--ApiKey'
          identity: managedIdentity.id
        }
        {
          name: 'selfhosted-jwtsecret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/SelfHosted--JwtSecret'
          identity: managedIdentity.id
        }
        {
          name: 'selfhosted-adminpassword'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/SelfHosted--SuperAdminPassword'
          identity: managedIdentity.id
        }
        {
          name: 'docintel-endpoint'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureDocumentIntelligence--Endpoint'
          identity: managedIdentity.id
        }
        {
          name: 'docintel-apikey'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureDocumentIntelligence--ApiKey'
          identity: managedIdentity.id
        }
        {
          name: 'vision-endpoint'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAIVision--Endpoint'
          identity: managedIdentity.id
        }
        {
          name: 'vision-apikey'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAIVision--ApiKey'
          identity: managedIdentity.id
        }
        {
          name: 'appinsights-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/ApplicationInsights--ConnectionString'
          identity: managedIdentity.id
        }
      ])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registryServer}/knowz-io/knowz-selfhosted-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__McpDb'
              secretRef: 'sql-connection'
            }
            {
              name: 'AzureOpenAI__Endpoint'
              secretRef: 'openai-endpoint'
            }
            {
              name: 'AzureOpenAI__ApiKey'
              secretRef: 'openai-apikey'
            }
            {
              name: 'AzureOpenAI__DeploymentName'
              value: chatDeploymentName
            }
            {
              name: 'AzureOpenAI__EmbeddingDeploymentName'
              value: embeddingDeploymentName
            }
            {
              name: 'AzureAISearch__Endpoint'
              secretRef: 'search-endpoint'
            }
            {
              name: 'AzureAISearch__ApiKey'
              secretRef: 'search-apikey'
            }
            {
              name: 'AzureAISearch__IndexName'
              value: 'knowledge'
            }
            {
              name: 'Storage__Provider'
              value: 'AzureBlob'
            }
            {
              name: 'Storage__Azure__ConnectionString'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'Storage__Azure__ContainerName'
              value: 'selfhosted-files'
            }
            {
              name: 'SelfHosted__ApiKey'
              secretRef: 'selfhosted-apikey'
            }
            {
              name: 'SelfHosted__JwtSecret'
              secretRef: 'selfhosted-jwtsecret'
            }
            {
              name: 'SelfHosted__SuperAdminPassword'
              secretRef: 'selfhosted-adminpassword'
            }
            {
              name: 'Database__AutoMigrate'
              value: 'true'
            }
            {
              name: 'MCP__ServiceKey'
              value: mcpServiceKey
            }
            {
              name: 'AzureDocumentIntelligence__Endpoint'
              secretRef: 'docintel-endpoint'
            }
            {
              name: 'AzureDocumentIntelligence__ApiKey'
              secretRef: 'docintel-apikey'
            }
            {
              name: 'AzureAIVision__Endpoint'
              secretRef: 'vision-endpoint'
            }
            {
              name: 'AzureAIVision__ApiKey'
              secretRef: 'vision-apikey'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentity.properties.clientId
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// ---- MCP Container App ----
resource mcpContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-mcp'
  location: location
  tags: effectiveTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: empty(registryUsername) ? [] : [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: empty(registryUsername) ? [] : [
        {
          name: 'registry-password'
          value: registryPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'mcp'
          image: '${registryServer}/knowz-io/knowz-selfhosted-mcp:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'Knowz__BaseUrl'
              value: 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
            }
            {
              name: 'MCP__BackendMode'
              value: 'selfhosted'
            }
            {
              name: 'MCP__ApiKeyValidationEndpoint'
              value: '/api/vaults'
            }
            {
              name: 'Authentication__ValidateApiKey'
              value: 'true'
            }
            {
              name: 'MCP__ServiceKey'
              value: mcpServiceKey
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

// ---- Web Container App ----
resource webContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-web'
  location: location
  tags: effectiveTags
  properties: {
    managedEnvironmentId: containerAppsEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: empty(registryUsername) ? [] : [
        {
          server: registryServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: empty(registryUsername) ? [] : [
        {
          name: 'registry-password'
          value: registryPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${registryServer}/knowz-io/knowz-selfhosted-web:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'API_UPSTREAM'
              value: apiContainerApp.properties.configuration.ingress.fqdn
            }
            {
              name: 'API_PROTOCOL'
              value: 'https'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

// ============================================================================
// AZURE FRONT DOOR (Standard tier with WAF)
// ============================================================================

resource wafPolicy 'Microsoft.Network/FrontDoorWebApplicationFirewallPolicies@2024-02-01' = {
  name: '${replace(prefix, '-', '')}waf'
  location: 'global'
  tags: effectiveTags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Detection'
      requestBodyCheck: 'Enabled'
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
          ruleSetAction: 'Block'
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '1.0'
        }
      ]
    }
  }
}

resource frontDoor 'Microsoft.Cdn/profiles@2024-02-01' = {
  name: '${prefix}-fd'
  location: 'global'
  tags: effectiveTags
  sku: {
    name: 'Premium_AzureFrontDoor'
  }
  properties: {
    originResponseTimeoutSeconds: 60
  }
}

// Front Door endpoint
resource fdEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-02-01' = {
  parent: frontDoor
  name: '${prefix}-endpoint'
  location: 'global'
  tags: effectiveTags
  properties: {
    enabledState: 'Enabled'
  }
}

// Origin group for Container Apps (Web)
resource fdOriginGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: frontDoor
  name: 'container-apps'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/healthz'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 30
    }
    sessionAffinityState: 'Disabled'
  }
}

// Origin: Web (default, serves / paths)
resource fdOriginWeb 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: fdOriginGroup
  name: 'web-origin'
  properties: {
    hostName: webContainerApp.properties.configuration.ingress.fqdn
    httpPort: 80
    httpsPort: 443
    originHostHeader: webContainerApp.properties.configuration.ingress.fqdn
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    sharedPrivateLinkResource: {
      privateLink: {
        id: containerAppsEnv.id
      }
      privateLinkLocation: location
      groupId: 'managedEnvironments'
      requestMessage: 'Front Door private link to Container Apps'
    }
  }
}

// Origin group for API (separate to allow different health probe)
resource fdApiOriginGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: frontDoor
  name: 'api-apps'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/health'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 30
    }
    sessionAffinityState: 'Disabled'
  }
}

// Origin: API
resource fdOriginApi 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: fdApiOriginGroup
  name: 'api-origin'
  properties: {
    hostName: apiContainerApp.properties.configuration.ingress.fqdn
    httpPort: 80
    httpsPort: 443
    originHostHeader: apiContainerApp.properties.configuration.ingress.fqdn
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    sharedPrivateLinkResource: {
      privateLink: {
        id: containerAppsEnv.id
      }
      privateLinkLocation: location
      groupId: 'managedEnvironments'
      requestMessage: 'Front Door private link to Container Apps API'
    }
  }
}

// Origin group for MCP (separate for MCP protocol paths)
resource fdMcpOriginGroup 'Microsoft.Cdn/profiles/originGroups@2024-02-01' = {
  parent: frontDoor
  name: 'mcp-origin-group'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/health'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 30
    }
    sessionAffinityState: 'Disabled'
  }
}

// Origin: MCP
// NOTE: Private link connections from Front Door to Container Apps require manual approval
// after deployment. Use: az network private-endpoint-connection approve --id <connection-id>
// This applies to ALL origins using sharedPrivateLinkResource (web, API, MCP).
resource fdOriginMcp 'Microsoft.Cdn/profiles/originGroups/origins@2024-02-01' = {
  parent: fdMcpOriginGroup
  name: 'mcp-origin'
  properties: {
    hostName: mcpContainerApp.properties.configuration.ingress.fqdn
    httpPort: 80
    httpsPort: 443
    originHostHeader: mcpContainerApp.properties.configuration.ingress.fqdn
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    sharedPrivateLinkResource: {
      privateLink: {
        id: containerAppsEnv.id
      }
      privateLinkLocation: location
      groupId: 'managedEnvironments'
      requestMessage: 'Front Door private link to Container Apps MCP'
    }
  }
}

// Security policy: attach WAF to endpoint
resource fdSecurityPolicy 'Microsoft.Cdn/profiles/securityPolicies@2024-02-01' = {
  parent: frontDoor
  name: 'waf-policy'
  properties: {
    parameters: {
      type: 'WebApplicationFirewall'
      wafPolicy: {
        id: wafPolicy.id
      }
      associations: [
        {
          domains: [
            {
              id: fdEndpoint.id
            }
          ]
          patternsToMatch: [
            '/*'
          ]
        }
      ]
    }
  }
}

// Route: /api/* -> API origin group
resource fdApiRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: fdEndpoint
  name: 'api-route'
  properties: {
    originGroup: {
      id: fdApiOriginGroup.id
    }
    supportedProtocols: [
      'Https'
    ]
    patternsToMatch: [
      '/api/*'
      '/health'
      '/swagger/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
  }
  dependsOn: [
    fdOriginApi
  ]
}

// Route: /sse/*, /message/*, /mcp/* -> MCP origin group
resource fdMcpRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: fdEndpoint
  name: 'mcp-route'
  properties: {
    originGroup: {
      id: fdMcpOriginGroup.id
    }
    supportedProtocols: [
      'Https'
    ]
    patternsToMatch: [
      '/sse/*'
      '/message/*'
      '/mcp/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
  }
  dependsOn: [
    fdOriginMcp
  ]
}

// Route: /* -> Web origin group (catch-all, lower priority)
resource fdWebRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-02-01' = {
  parent: fdEndpoint
  name: 'web-route'
  properties: {
    originGroup: {
      id: fdOriginGroup.id
    }
    supportedProtocols: [
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: 'Enabled'
    enabledState: 'Enabled'
  }
  dependsOn: [
    fdOriginWeb
    fdApiRoute
    fdMcpRoute
  ]
}

// Front Door diagnostics
resource fdDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${prefix}-fd-diagnostics'
  scope: frontDoor
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'FrontDoorAccessLog'
        enabled: true
      }
      {
        category: 'FrontDoorWebApplicationFirewallLog'
        enabled: true
      }
      {
        category: 'FrontDoorHealthProbeLog'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// OUTPUTS
// ============================================================================

// Azure AI Search
output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
output searchServiceName string = searchService.name
output searchIndexName string = 'knowledge'

// Azure OpenAI
output openAiEndpoint string = effectiveOpenAiEndpoint
output openAiResourceName string = deployOpenAI ? cognitiveServices.name : 'external'
output chatDeploymentNameOutput string = chatDeploymentName
output miniDeploymentName string = 'gpt-5-mini'
output embeddingDeploymentNameOutput string = embeddingDeploymentName

// Document Intelligence
output documentIntelligenceEndpoint string = deployDocumentIntelligence ? documentIntelligence.properties.endpoint : externalDocIntelEndpoint
output documentIntelligenceName string = deployDocumentIntelligence ? documentIntelligence.name : 'external'

// AI Configuration Summary (mode per service)
// NOTE: Enterprise template does not currently wire `existing*` params into resource resolution
// (private-endpoint + cross-RG complexity). The deploy script (selfhosted-deploy.ps1) performs
// the lookup and passes resolved endpoint/key via external* parameters. These params are
// accepted here so the portal UI can pass them without deployment failures.
output aiConfigurationSummary object = {
  openai: deployOpenAI ? 'deployed' : (existingOpenAiName != '' ? 'existing:${existingOpenAiName}' : 'external')
  vision: deployVision ? 'deployed' : (existingVisionName != '' ? 'existing:${existingVisionName}' : 'external')
  docIntel: deployDocumentIntelligence ? 'deployed' : (existingDocIntelName != '' ? 'existing:${existingDocIntelName}' : 'external')
}

// SQL Database
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = 'McpKnowledge'
output sqlServerName string = sqlServerName

// Storage
output storageAccountName string = storageAccount.name
output storageBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output frontDoorId string = frontDoor.properties.frontDoorId

// Managed Identity
output managedIdentityId string = managedIdentity.id
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityClientId string = managedIdentity.properties.clientId

// Key Vault
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri

// Monitoring
output logAnalyticsWorkspaceId string = logAnalytics.id
output logAnalyticsWorkspaceName string = logAnalytics.name
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey

// Container Apps
output apiContainerAppFqdn string = apiContainerApp.properties.configuration.ingress.fqdn
output mcpContainerAppFqdn string = mcpContainerApp.properties.configuration.ingress.fqdn
output webContainerAppFqdn string = webContainerApp.properties.configuration.ingress.fqdn

// Enterprise additions
output frontDoorEndpoint string = fdEndpoint.properties.hostName
output vnetName string = vnet.name
output vnetId string = vnet.id
