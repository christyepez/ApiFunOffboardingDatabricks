targetScope = 'resourceGroup'

@allowed(['dev', 'test', 'prod'])
param environment string
param location string = resourceGroup().location
param functionAppName string
param servicePlanName string
param storageAccountName string
param keyVaultName string
param logAnalyticsName string
param appInsightsName string
param functionAppClientId string
param functionAppAudience string
param apimManagedIdentityClientId string
param databricksHost string
param databricksWarehouseId string
param azureFilesConnectionSecretUri string
param contentShareName string
param linuxFxVersion string = 'DOTNET-ISOLATED|10.0'
param functionPublicNetworkAccess bool = true
param storagePublicNetworkAccess bool = true
param keyVaultPublicNetworkAccess bool = true
param vnetIntegrationSubnetId string = ''
param enablePrivateEndpoints bool = false
param privateEndpointSubnetId string = ''
param functionPrivateDnsZoneId string = ''
param blobPrivateDnsZoneId string = ''
param filePrivateDnsZoneId string = ''
param queuePrivateDnsZoneId string = ''
param tablePrivateDnsZoneId string = ''
param keyVaultPrivateDnsZoneId string = ''
param alertActionGroupIds array = []
param tags object = {}

var commonTags = union(tags, {
  application: 'offboarding'
  environment: environment
  managedBy: 'bicep'
})

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: commonTags
  properties: {
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: commonTags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: commonTags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    // Elastic Premium uses Azure Files for content. Shared Key remains enabled only for that runtime dependency;
    // the connection string itself is referenced from Key Vault and never stored in source control/app settings.
    allowSharedKeyAccess: true
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: storagePublicNetworkAccess ? 'Enabled' : 'Disabled'
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource contentShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: contentShareName
  properties: {
    accessTier: 'TransactionOptimized'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: commonTags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: environment == 'prod'
    softDeleteRetentionInDays: 90
    publicNetworkAccess: keyVaultPublicNetworkAccess ? 'Enabled' : 'Disabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: servicePlanName
  location: location
  tags: commonTags
  kind: 'linux'
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  tags: commonTags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: functionPublicNetworkAccess ? 'Enabled' : 'Disabled'
    virtualNetworkSubnetId: empty(vnetIntegrationSubnetId) ? null : vnetIntegrationSubnetId
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      vnetRouteAllEnabled: !empty(vnetIntegrationSubnetId)
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: '@Microsoft.KeyVault(SecretUri=${azureFilesConnectionSecretUri})' }
        { name: 'WEBSITE_CONTENTSHARE', value: contentShare.name }
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'Databricks__Host', value: databricksHost }
        { name: 'Databricks__WarehouseId', value: databricksWarehouseId }
        { name: 'Databricks__AuthenticationMode', value: 'DefaultAzureCredential' }
        { name: 'Databricks__OAuthScope', value: '2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default' }
        { name: 'Databricks__WaitTimeoutSeconds', value: '30' }
        { name: 'Databricks__PollIntervalMilliseconds', value: '750' }
        { name: 'Databricks__MaxPollSeconds', value: '30' }
        { name: 'Databricks__MaxResultChunks', value: '100' }
      ]
    }
  }
}

resource functionDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'offboarding-function-diagnostics'
  scope: functionApp
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        categoryGroup: 'allLogs'
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

resource function5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${functionAppName}-http5xx'
  location: 'global'
  tags: commonTags
  properties: {
    description: 'Offboarding Function returned repeated HTTP 5xx responses.'
    severity: 2
    enabled: true
    scopes: [functionApp.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xxThreshold'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    autoMitigate: true
    actions: [for actionGroupId in alertActionGroupIds: {
      actionGroupId: actionGroupId
    }]
  }
}
resource authSettings 'Microsoft.Web/sites/config@2022-09-01' = {
  parent: functionApp
  name: 'authsettingsV2'
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'Return401'
      excludedPaths: [
        '/api/health'
      ]
    }
    httpSettings: {
      requireHttps: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: functionAppClientId
          openIdIssuer: '${az.environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [functionAppAudience]
          defaultAuthorizationPolicy: {
            allowedApplications: [apimManagedIdentityClientId]
          }
        }
      }
    }
    login: {
      tokenStore: {
        enabled: false
      }
    }
  }
}

resource functionPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${functionAppName}-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      {
        name: '${functionAppName}-connection'
        properties: {
          privateLinkServiceId: functionApp.id
          groupIds: ['sites']
        }
      }
    ]
  }
}

resource functionDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(functionPrivateDnsZoneId)) {
  parent: functionPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      { name: 'function', properties: { privateDnsZoneId: functionPrivateDnsZoneId } }
    ]
  }
}

resource blobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${storageAccountName}-blob-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      { name: '${storageAccountName}-blob-connection', properties: { privateLinkServiceId: storage.id, groupIds: ['blob'] } }
    ]
  }
}

resource blobDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(blobPrivateDnsZoneId)) {
  parent: blobPrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'blob', properties: { privateDnsZoneId: blobPrivateDnsZoneId } }] }
}

resource filePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${storageAccountName}-file-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      { name: '${storageAccountName}-file-connection', properties: { privateLinkServiceId: storage.id, groupIds: ['file'] } }
    ]
  }
}

resource fileDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(filePrivateDnsZoneId)) {
  parent: filePrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'file', properties: { privateDnsZoneId: filePrivateDnsZoneId } }] }
}

resource queuePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${storageAccountName}-queue-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      { name: '${storageAccountName}-queue-connection', properties: { privateLinkServiceId: storage.id, groupIds: ['queue'] } }
    ]
  }
}

resource queueDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(queuePrivateDnsZoneId)) {
  parent: queuePrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'queue', properties: { privateDnsZoneId: queuePrivateDnsZoneId } }] }
}

resource tablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${storageAccountName}-table-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      { name: '${storageAccountName}-table-connection', properties: { privateLinkServiceId: storage.id, groupIds: ['table'] } }
    ]
  }
}

resource tableDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(tablePrivateDnsZoneId)) {
  parent: tablePrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'table', properties: { privateDnsZoneId: tablePrivateDnsZoneId } }] }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (enablePrivateEndpoints) {
  name: '${keyVaultName}-pe'
  location: location
  tags: commonTags
  properties: {
    subnet: { id: privateEndpointSubnetId }
    privateLinkServiceConnections: [
      { name: '${keyVaultName}-connection', properties: { privateLinkServiceId: keyVault.id, groupIds: ['vault'] } }
    ]
  }
}

resource keyVaultDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (enablePrivateEndpoints && !empty(keyVaultPrivateDnsZoneId)) {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'vault', properties: { privateDnsZoneId: keyVaultPrivateDnsZoneId } }] }
}

var storageBlobDataOwnerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var storageQueueDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageBlobDataOwnerRoleId)
  scope: storage
  properties: {
    roleDefinitionId: storageBlobDataOwnerRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageQueueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageQueueDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: storageQueueDataContributorRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageTableDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: storageTableDataContributorRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppId string = functionApp.id
output functionAppPrincipalId string = functionApp.identity.principalId
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output keyVaultUri string = keyVault.properties.vaultUri
output appInsightsConnectionString string = appInsights.properties.ConnectionString