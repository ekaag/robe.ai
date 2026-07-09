using '../modules/stage.bicep'

// F1 (Free): 60 CPU-min/day cap, no custom domain/SSL, no autoscale, no
// deployment slots, no SLA. Fine for low-volume dev testing.
param stageName = 'dev'
param location = 'canadacentral'
param appServicePlanSkuName = 'F1'
param appServicePlanSkuTier = 'Free'
param enablePurgeProtection = false
param openAiLocation = 'eastus'
