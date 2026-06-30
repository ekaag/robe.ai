using '../modules/stage.bicep'

param stageName = 'live'
param location = 'canadacentral'
param appServicePlanSkuName = 'P1v3'
param appServicePlanSkuTier = 'PremiumV3'
param enablePurgeProtection = true
