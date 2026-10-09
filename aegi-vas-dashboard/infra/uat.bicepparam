using './main.bicep'

// UAT values. Everything in <angle brackets> is to be supplied by AEGI.
param environmentName = 'UAT'
param appName = '<uat-web-app-name>'
param planSku = 'B1'
param acrName = '<container-registry-name>'
param image = 'aegi-vas-dashboard:<git-sha>'
param apiClientId = '<uat-api-client-id>'
param spaClientId = '<uat-spa-client-id>'
param fabricSqlEndpoint = '<uat-workspace>.datawarehouse.fabric.microsoft.com'
param fabricDatabase = '<uat-lakehouse-name>'
