using '../modules/stage.bicep'

param stageName = 'gamma'
param location = 'eastus'
param appServicePlanSkuName = 'S1'
param appServicePlanSkuTier = 'Standard'
param enablePurgeProtection = true
