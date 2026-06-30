// Per-stage resource set for the Robe.AI API: Key Vault (RBAC-authorized, secrets
// populated out-of-band — never via this template), a Windows App Service (Linux
// App Service Plans hit quota/availability limits on Free-tier SKUs) with a
// system-assigned managed identity granted "Key Vault Secrets User" on its own
// vault only, and Application Insights wired into the App Service's connection
// string app setting. Deployed once per stage (dev/gamma/live) into that stage's
// own resource group — see ../main.bicep (all 3 stages) or this file deployed
// directly with a ../parameters/<stage>.bicepparam file (single stage).

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

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ASPNETCORE_ENVIRONMENT value the app expects, matching appsettings.Dev.json /
// appsettings.Gamma.json / appsettings.Live.json in robe.api.
var aspnetEnvironmentName = '${toUpper(take(stageName, 1))}${skip(stageName, 1)}'

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

output keyVaultUri string = keyVault.properties.vaultUri
output appServiceName string = appService.name
output appServiceDefaultHostName string = appService.properties.defaultHostName
output appServicePrincipalId string = appService.identity.principalId
