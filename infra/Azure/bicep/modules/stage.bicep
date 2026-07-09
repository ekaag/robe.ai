// Per-stage resource set for the Robe.AI API: Key Vault (RBAC-authorized, secrets
// populated out-of-band — never via this template), a Windows App Service (Linux
// App Service Plans hit quota/availability limits on Free-tier SKUs) with a
// system-assigned managed identity granted "Key Vault Secrets User" on its own
// vault only, an Azure OpenAI account with a gpt-4o deployment, a
// "Cognitive Services OpenAI User" grant from the App Service identity to the
// OpenAI account (enables DefaultAzureCredential — no API key needed), and
// Application Insights wired into the App Service's connection string app setting.
// Deployed once per stage (dev/gamma/live) into that stage's own resource group
// — see ../main.bicep (all 3 stages) or this file deployed directly with a
// ../parameters/<stage>.bicepparam file (single stage).

@description('Stage name used in resource names, lowercase: dev | gamma | live')
@allowed([
  'dev'
  'gamma'
  'live'
])
param stageName string

param location string = resourceGroup().location

@description('App Service Plan SKU name, e.g. F1, S1, P1v3')
param appServicePlanSkuName string

@description('App Service Plan SKU tier, e.g. Free, Standard, PremiumV3')
param appServicePlanSkuTier string

@description('Enable Key Vault purge protection. Irreversible once enabled — keep false for dev so the vault can be deleted/recreated freely while iterating.')
param enablePurgeProtection bool = true

@description('Region for the Azure OpenAI account. Vision model quota is allocated per region; canadacentral is the default for all stages.')
param openAiLocation string = 'canadacentral'

@description('Vision model deployment capacity in thousands of tokens per minute (K TPM). 10 = 10 K TPM, suitable for dev.')
param openAiModelCapacityK int = 10

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

// ASPNETCORE_ENVIRONMENT value the app expects, matching appsettings.Dev.json /
// appsettings.Gamma.json / appsettings.Live.json in robe.api.
var aspnetEnvironmentName = '${toUpper(take(stageName, 1))}${skip(stageName, 1)}'

// Vision model deployed in every stage. Bump version here to upgrade all stages at once.
// 2024-11-20 is deprecated for new deployments; 2024-08-06 is the newest usable version
// on this subscription's gpt-4o GlobalStandard quota. Upgrade to gpt-5.1 once quota is
// requested via https://aka.ms/oai/quotaincrease.
var visionModelName = 'gpt-4o'
var visionModelVersion = '2024-05-13'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-robeai-${stageName}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: enablePurgeProtection ? true : null
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-robe-${stageName}'
  location: location
  kind: 'app'
  sku: {
    name: appServicePlanSkuName
    tier: appServicePlanSkuTier
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-robe-${stageName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-robe-${stageName}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v7.0'
      // F1 (Free) doesn't support Always On.
      alwaysOn: appServicePlanSkuTier != 'Free'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspnetEnvironmentName
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource keyVaultSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Azure OpenAI account — one per stage, in openAiLocation.
// customSubDomainName must be globally unique and match the resource name so the
// endpoint URL is predictable: https://aoai-robe-<stage>.openai.azure.com/
resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'aoai-robe-${stageName}'
  location: openAiLocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: 'aoai-robe-${stageName}'
    publicNetworkAccess: 'Enabled'
  }
}

// Vision model deployment under the account. The deployment name is stored in Key Vault
// as AzureOpenAI--DeploymentName by dev-create.sh so the app resolves it at runtime.
resource visionModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAiAccount
  name: 'gpt-4o'
  sku: {
    name: 'GlobalStandard'
    capacity: openAiModelCapacityK
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: visionModelName
      version: visionModelVersion
    }
  }
}

// Grants the App Service managed identity "Cognitive Services OpenAI User" on
// the OpenAI account. This is what lets DefaultAzureCredential call the
// inference API without an API key — separate from the Key Vault grant above.
resource openAiUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAiAccount.id, appService.id, cognitiveServicesOpenAiUserRoleId)
  scope: openAiAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output keyVaultUri string = keyVault.properties.vaultUri
output appServiceName string = appService.name
output appServiceDefaultHostName string = appService.properties.defaultHostName
output appServicePrincipalId string = appService.identity.principalId
output openAiEndpoint string = openAiAccount.properties.endpoint
output openAiDeploymentName string = visionModelDeployment.name
