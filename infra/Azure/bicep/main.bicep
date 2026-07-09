// Orchestrates all 3 stages in one deployment: rg-robe-dev / rg-robe-gamma /
// rg-robe-live, each with its own Key Vault, App Service, Azure OpenAI account
// (gpt-4o deployment), and Application Insights (see modules/stage.bicep).
// Subscription-scoped because it creates the resource groups themselves.
//
// Deploy:
//   az login
//   az account set --subscription <subscription-id>
//   az deployment sub create --location canadacentral --template-file infra/Azure/bicep/main.bicep
//
// To deploy a single stage instead, use modules/stage.bicep directly with the
// matching parameters/<stage>.bicepparam file against an existing resource group.
//
// After deploy, populate each vault with the two remaining secrets (the endpoint
// is emitted as a Bicep output and set by dev-create.sh automatically; populate
// the others by hand for gamma/live):
//   az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--Endpoint --value <from output>
//   az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--DeploymentName --value gpt-4o
//   (repeat for kv-robeai-gamma / kv-robeai-live)
// No API key is needed — the App Service managed identity authenticates to Azure
// OpenAI via the "Cognitive Services OpenAI User" RBAC grant in stage.bicep.

targetScope = 'subscription'

param location string = 'canadacentral'

@description('Region for all Azure OpenAI accounts. Must have GPT-4o quota allocated.')
param openAiLocation string = 'canadacentral'

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
    openAiLocation: openAiLocation
    openAiModelCapacityK: 10
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
    openAiLocation: openAiLocation
    openAiModelCapacityK: 30
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
    openAiLocation: openAiLocation
    openAiModelCapacityK: 100
  }
}

output devKeyVaultUri string = devStage.outputs.keyVaultUri
output devAppServiceHostName string = devStage.outputs.appServiceDefaultHostName
output devOpenAiEndpoint string = devStage.outputs.openAiEndpoint
output devOpenAiDeploymentName string = devStage.outputs.openAiDeploymentName

output gammaKeyVaultUri string = gammaStage.outputs.keyVaultUri
output gammaAppServiceHostName string = gammaStage.outputs.appServiceDefaultHostName
output gammaOpenAiEndpoint string = gammaStage.outputs.openAiEndpoint
output gammaOpenAiDeploymentName string = gammaStage.outputs.openAiDeploymentName

output liveKeyVaultUri string = liveStage.outputs.keyVaultUri
output liveAppServiceHostName string = liveStage.outputs.appServiceDefaultHostName
output liveOpenAiEndpoint string = liveStage.outputs.openAiEndpoint
output liveOpenAiDeploymentName string = liveStage.outputs.openAiDeploymentName
