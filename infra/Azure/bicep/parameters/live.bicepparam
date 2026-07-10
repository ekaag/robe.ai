using '../modules/stage.bicep'

param stageName = 'live'
param location = 'canadacentral'
param appServicePlanSkuName = 'P1v3'
param appServicePlanSkuTier = 'PremiumV3'
param enablePurgeProtection = true

param staticWebAppSkuName = 'Standard'
param staticWebAppLocation = 'eastus2'

// param entraAuthority = 'https://vestraoauth.ciamlogin.com/vestraoauth.onmicrosoft.com'
// param entraClientId  = 'd4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9'
// param entraApiScope  = 'api://d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9/access_as_user'
