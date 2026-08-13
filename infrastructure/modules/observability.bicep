@description('Name of the Log Analytics workspace to adopt.')
param workspaceName string

@description('Name of the Application Insights component to adopt.')
param insightsName string

@description('Region of the workspace and insights component.')
param location string

@description('Workspace retention in days.')
param workspaceRetentionInDays int = 30

@description('Application Insights retention in days.')
param insightsRetentionInDays int = 90

@description('Name of the budget notification action group.')
param budgetActionGroupName string = 'alpakasoelde-budget-actions'

@description('Email address used for budget notifications (shared with the alert email receiver).')
param budgetNotificationEmail string

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: workspaceRetentionInDays
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: insightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: insightsRetentionInDays
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: workspace.id
  }
}

resource budgetActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: budgetActionGroupName
  location: location
  properties: {
    groupShortName: 'Alpakasoelde'
    enabled: true
    emailReceivers: [
      {
        name: 'Email_-EmailAction-'
        emailAddress: budgetNotificationEmail
        useCommonAlertSchema: false
      }
    ]
    azureAppPushReceivers: [
      {
        name: 'Email_-AzureAppAction-'
        emailAddress: budgetNotificationEmail
      }
    ]
  }
}

output workspaceId string = workspace.id
output insightsName string = insights.name
output budgetActionGroupId string = budgetActionGroup.id