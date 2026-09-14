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
