using '../main.bicep'

param environment = 'prod'
param functionAppName = 'fn-p-idbinvest-offboarding'
param servicePlanName = 'asp-p-idbinvest-offboarding'
param storageAccountName = 'stpidoffreplace'
param keyVaultName = 'kv-p-idbi-offboarding'
param logAnalyticsName = 'log-p-idbinvest-offboarding'
param appInsightsName = 'appi-p-idbinvest-offboarding'
param functionAppClientId = 'REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param functionAppAudience = 'api://REPLACE-WITH-FUNCTION-APP-REGISTRATION-CLIENT-ID'
param apimManagedIdentityClientId = 'REPLACE-WITH-APIM-MANAGED-IDENTITY-CLIENT-ID'
param databricksHost = 'https://REPLACE.azuredatabricks.net'
param databricksWarehouseId = 'REPLACE-WAREHOUSE-ID'
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
param keyVaultPrivateDnsZoneId = ''
