// Orchestrates all 3 stages in one deployment: rg-robe-dev / rg-robe-gamma /
// rg-robe-live, each with its own Key Vault, App Service, Azure OpenAI account
// (gpt-5-mini deployment), and Application Insights (see modules/stage.bicep).
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
//   az keyvault secret set --vault-name kv-robeai-dev --name AzureOpenAI--DeploymentName --value gpt-5-mini
//   (repeat for kv-robeai-gamma / kv-robeai-live)
// No API key is needed — the App Service managed identity authenticates to Azure
// OpenAI via the "Cognitive Services OpenAI User" RBAC grant in stage.bicep.

targetScope = 'subscription'

param location string = 'canadacentral'

@description('Region for all Azure OpenAI accounts. Must have gpt-5-mini GlobalStandard quota allocated.')
param openAiLocation string = 'canadacentral'

@description('Region for all Azure Static Web Apps. SWA is not available in canadacentral.')
param staticWebAppLocation string = 'eastus2'

@description('Entra External ID (CIAM) authority URL shared across all stages. Set once the tenant is configured.')
param entraAuthority string = ''

@description('Entra External ID (CIAM) application (client) ID for the frontend.')
param entraClientId string = ''

@description('Entra API scope the frontend requests, e.g. api://<client-id>/access_as_user.')
param entraApiScope string = ''

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
    staticWebAppSkuName: 'Standard'
    staticWebAppLocation: staticWebAppLocation
    entraAuthority: entraAuthority
    entraClientId: entraClientId
    entraApiScope: entraApiScope
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
    staticWebAppSkuName: 'Standard'
    staticWebAppLocation: staticWebAppLocation
    entraAuthority: entraAuthority
    entraClientId: entraClientId
    entraApiScope: entraApiScope
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
    staticWebAppSkuName: 'Standard'
    staticWebAppLocation: staticWebAppLocation
    entraAuthority: entraAuthority
    entraClientId: entraClientId
    entraApiScope: entraApiScope
  }
}

output devKeyVaultUri string = devStage.outputs.keyVaultUri
output devAppServiceHostName string = devStage.outputs.appServiceDefaultHostName
output devOpenAiEndpoint string = devStage.outputs.openAiEndpoint
output devOpenAiDeploymentName string = devStage.outputs.openAiDeploymentName
output devStaticWebAppName string = devStage.outputs.staticWebAppName
output devStaticWebAppHostName string = devStage.outputs.staticWebAppHostName

output gammaKeyVaultUri string = gammaStage.outputs.keyVaultUri
output gammaAppServiceHostName string = gammaStage.outputs.appServiceDefaultHostName
output gammaOpenAiEndpoint string = gammaStage.outputs.openAiEndpoint
output gammaOpenAiDeploymentName string = gammaStage.outputs.openAiDeploymentName
output gammaStaticWebAppName string = gammaStage.outputs.staticWebAppName
output gammaStaticWebAppHostName string = gammaStage.outputs.staticWebAppHostName

output liveKeyVaultUri string = liveStage.outputs.keyVaultUri
output liveAppServiceHostName string = liveStage.outputs.appServiceDefaultHostName
output liveOpenAiEndpoint string = liveStage.outputs.openAiEndpoint
output liveOpenAiDeploymentName string = liveStage.outputs.openAiDeploymentName
output liveStaticWebAppName string = liveStage.outputs.staticWebAppName
output liveStaticWebAppHostName string = liveStage.outputs.staticWebAppHostName
