// Orchestrates all 3 stages in one deployment: rg-robe-dev / rg-robe-gamma /
// rg-robe-live, each with its own Key Vault + App Service + Application Insights
// (see modules/stage.bicep). Subscription-scoped because it creates the resource
// groups themselves.
//
// Deploy:
//   az login
//   az account set --subscription <subscription-id>
//   az deployment sub create --location eastus --template-file infra/Azure/bicep/main.bicep
//
// To deploy a single stage instead, use modules/stage.bicep directly with the
// matching parameters/<stage>.bicepparam file against an existing resource group.
//
// This template never sets secret values — after deploy, populate each vault:
//   az keyvault secret set --vault-name kv-robe-dev --name AzureOpenAI--Endpoint --value <...>
//   az keyvault secret set --vault-name kv-robe-dev --name AzureOpenAI--ApiKey --value <...>
//   az keyvault secret set --vault-name kv-robe-dev --name AzureOpenAI--DeploymentName --value <...>
//   (repeat for kv-robe-gamma / kv-robe-live)

targetScope = 'subscription'

param location string = 'eastus'

resource rgDev 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-robe-dev'
  location: location
}

resource rgGamma 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-robe-gamma'
  location: location
}

resource rgLive 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-robe-live'
  location: location
}

module devStage 'modules/stage.bicep' = {
  name: 'deploy-stage-dev'
  scope: rgDev
  params: {
    stageName: 'dev'
    location: location
    appServicePlanSkuName: 'F1'
    appServicePlanSkuTier: 'Free'
    enablePurgeProtection: false
  }
}

module gammaStage 'modules/stage.bicep' = {
  name: 'deploy-stage-gamma'
  scope: rgGamma
  params: {
    stageName: 'gamma'
    location: location
    appServicePlanSkuName: 'S1'
    appServicePlanSkuTier: 'Standard'
    enablePurgeProtection: true
  }
}

module liveStage 'modules/stage.bicep' = {
  name: 'deploy-stage-live'
  scope: rgLive
  params: {
    stageName: 'live'
    location: location
    appServicePlanSkuName: 'P1v3'
    appServicePlanSkuTier: 'PremiumV3'
    enablePurgeProtection: true
  }
}

output devKeyVaultUri string = devStage.outputs.keyVaultUri
output devAppServiceHostName string = devStage.outputs.appServiceDefaultHostName
output gammaKeyVaultUri string = gammaStage.outputs.keyVaultUri
output gammaAppServiceHostName string = gammaStage.outputs.appServiceDefaultHostName
output liveKeyVaultUri string = liveStage.outputs.keyVaultUri
output liveAppServiceHostName string = liveStage.outputs.appServiceDefaultHostName
