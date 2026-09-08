using '../main.bicep'

param environment = 'test'
param functionAppName = 'fn-np-t-idbinvest-offboarding'
param servicePlanName = 'asp-np-t-idbinvest-offboarding'
param storageAccountName = 'stnptidoffreplace'
param keyVaultName = 'kv-np-t-idbi-offboarding'
param logAnalyticsName = 'log-np-t-idbinvest-offboarding'
param appInsightsName = 'appi-np-t-idbinvest-offboarding'
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
