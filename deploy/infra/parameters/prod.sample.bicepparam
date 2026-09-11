using '../main.bicep'

param environment = 'prod'
param functionAppName = 'fn-p-idbinvest-offboarding-facade'
param servicePlanName = 'asp-p-idbinvest-offboarding'
param storageAccountName = 'stpidoffreplace'
param keyVaultName = 'kv-p-idbi-offboarding'
param logAnalyticsName = 'law-p-idbinvest-offboarding'
param appInsightsName = 'ai-p-idbinvest-offboarding'
param functionAppClientId = 'REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param functionAppAudience = 'api://REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param apimManagedIdentityClientId = 'REPLACE-WITH-APIM-MANAGED-IDENTITY-CLIENT-ID'
param databricksHost = 'https://REPLACE.azuredatabricks.net'
param databricksWarehouseId = 'REPLACE-WAREHOUSE-ID'
param azureFilesConnectionSecretUri = 'https://REPLACE.vault.azure.net/secrets/function-content-storage/REPLACE-VERSION'
param contentShareName = 'offboardingcontent'
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
