// UAT sizing baseline (proposed, to be confirmed): Azure App Service, custom container,
// B1 Linux, one instance, Always On, system-assigned managed identity, no Key Vault.
// B1 has no deployment slots; move to Standard or Premium if UAT must test slot swaps.
//
//   az deployment group create -g <resource-group> -f infra/main.bicep -p infra/uat.bicepparam

@description('Short environment name, used in resource names and as ASPNETCORE_ENVIRONMENT (UAT or Production).')
@allowed(['UAT', 'Production'])
param environmentName string = 'UAT'

param location string = resourceGroup().location

@description('Globally unique name of the Web App.')
param appName string

@description('App Service plan SKU. B1 = 1 vCPU, 1.75 GB RAM.')
param planSku string = 'B1'

@description('Existing Azure Container Registry that holds the release image (same resource group).')
param acrName string

@description('Image name and tag in the registry, for example aegi-vas-dashboard:<git-sha>.')
param image string

param tenantId string = tenant().tenantId

@description('Client ID (GUID) of the API app registration.')
param apiClientId string

@description('Application ID URI of the API app registration.')
param apiAudience string = 'api://${apiClientId}'

@description('Client ID of the SPA app registration.')
param spaClientId string

@description('Fabric SQL analytics endpoint host, for example <id>.datawarehouse.fabric.microsoft.com.')
param fabricSqlEndpoint string

@description('Lakehouse name.')
param fabricDatabase string

@description('Application Insights connection string. Empty turns telemetry off.')
param appInsightsConnectionString string = ''

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${appName}'
  location: location
  kind: 'linux'
  sku: {
    name: planSku
    capacity: 1
  }
  properties: {
    reserved: true // Linux
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/${image}'
      // Pull the image with the managed identity, so the registry needs no admin password.
      acrUseManagedIdentityCreds: true
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      // Liveness only. /health/ready also checks Fabric and must not be used here,
      // or a Fabric outage would take the single instance out of rotation.
      healthCheckPath: '/health/live'
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: environmentName }
        { name: 'Authentication__Mode', value: 'Entra' }
        { name: 'Authentication__TenantId', value: tenantId }
        { name: 'Authentication__ApiAudience', value: apiAudience }
        { name: 'Authentication__ApiClientId', value: apiClientId }
        { name: 'Authentication__SpaClientId', value: spaClientId }
        { name: 'Reporting__Source', value: 'Fabric' }
        { name: 'Fabric__SqlEndpoint', value: fabricSqlEndpoint }
        { name: 'Fabric__Database', value: fabricDatabase }
        { name: 'Fabric__Credential', value: 'ManagedIdentity' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
      ]
    }
  }
}

// AcrPull for the Web App's identity.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, app.id, 'acrpull')
  scope: acr
  properties: {
    principalId: app.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

output appUrl string = 'https://${app.properties.defaultHostName}'
@description('Add this identity to the Fabric workspace (lowest role that can read the gold views) and grant SELECT on the reporting views.')
output managedIdentityPrincipalId string = app.identity.principalId
