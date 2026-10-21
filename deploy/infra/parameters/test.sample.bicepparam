using '../main.bicep'

param environment = 'test'
param functionAppName = 'fn-np-t-idbinvest-offboarding-facade'
param servicePlanName = 'asp-np-t-idbinvest-offboarding'
param storageAccountName = 'stnptidoffreplace'
param keyVaultName = 'kv-np-t-idbi-offboarding'
param logAnalyticsName = 'law-np-t-idbinvest-offboarding'
param appInsightsName = 'ai-np-t-idbinvest-offboarding'
param functionAppClientId = 'REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param functionAppAudience = 'api://REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param apimManagedIdentityClientId = 'REPLACE-WITH-APIM-MANAGED-IDENTITY-CLIENT-ID'
param databricksHost = 'https://REPLACE.azuredatabricks.net'
param databricksWarehouseId = 'REPLACE-WAREHOUSE-ID'
param dataMotionBaseUrl = 'REPLACE-WITH-DATAMOTION-TEST-BASE-URL'
param dataMotionWarehouse = 'warehouse_idbi_offboarding'
param dataMotionSubscriptionKeySecretUri = 'https://REPLACE.vault.azure.net/secrets/datamotion-subscription-key/REPLACE-VERSION'
param azureFilesConnectionSecretUri = 'https://REPLACE.vault.azure.net/secrets/function-content-storage/REPLACE-VERSION'
param contentShareName = 'offboardingcontent'
param deployOrchestrator = false
param orchestratorFunctionAppName = 'fn-np-t-idbinvest-offboarding-orchestrator'
param orchestratorContentShareName = 'offboardingorchestrator'
param orchestratorFunctionAppClientId = 'REPLACE-WITH-ORCHESTRATOR-APP-REGISTRATION-CLIENT-ID'
param orchestratorFunctionAppAudience = 'api://REPLACE-WITH-ORCHESTRATOR-APP-REGISTRATION-CLIENT-ID'
param dataExposureBaseUrl = 'REPLACE-WITH-INTERNAL-DATA-EXPOSURE-API-BASE-URL'
param dataExposureResourceScope = 'api://REPLACE-WITH-DATA-EXPOSURE-APP-REGISTRATION-CLIENT-ID/.default'
param dataExposureSubscriptionKeySecretUri = ''
param functionPublicNetworkAccess = true
param storagePublicNetworkAccess = true
param keyVaultPublicNetworkAccess = true
param tags = {
  system: 'offboarding'
  owner: 'IDB Invest'
}
param vnetIntegrationSubnetId = ''
param enablePrivateEndpoints = false
param privateEndpointSubnetId = ''
param functionPrivateDnsZoneId = ''
param blobPrivateDnsZoneId = ''
param tablePrivateDnsZoneId = ''
param queuePrivateDnsZoneId = ''
param filePrivateDnsZoneId = ''
param keyVaultPrivateDnsZoneId = ''
