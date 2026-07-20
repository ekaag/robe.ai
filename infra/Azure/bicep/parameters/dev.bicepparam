using '../modules/stage.bicep'

// F1 (Free): 60 CPU-min/day cap, no custom domain/SSL, no autoscale, no
// deployment slots, no SLA. Fine for low-volume dev testing.
param stageName = 'dev'
param location = 'canadacentral'
param appServicePlanSkuName = 'F1'
param appServicePlanSkuTier = 'Free'
param enablePurgeProtection = false
param openAiLocation = 'eastus'
param storageSkuName = 'Standard_LRS'
// canadacentral hit a capacity outage for zone-redundant Cosmos accounts — eastus works.
param cosmosLocation = 'eastus'

// SWA Standard is required for Next.js SSR (App Router, dynamic routes).
// SWA is not available in canadacentral — use eastus2.
param staticWebAppSkuName = 'Standard'
param staticWebAppLocation = 'eastus2'

// Entra External ID (CIAM) config for the frontend. Populate once the tenant is set up.
// These values come from .env.local.example in the frontend — same tenant for all stages.
// param entraAuthority = 'https://vestraoauth.ciamlogin.com/vestraoauth.onmicrosoft.com'
// param entraClientId  = 'd4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9'
// param entraApiScope  = 'api://d4cd6bf9-9a9e-44e6-b7e5-12660e5e32d9/access_as_user'
