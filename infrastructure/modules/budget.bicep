targetScope = 'subscription'

@description('Name of the resource group that holds the budget action group.')
param resourceGroupName string

@description('Name of the budget.')
param budgetName string = 'Alpakasoelde-Budget'

@description('Monthly budget amount in EUR.')
param amount int = 3

@description('Budget start date (ISO 8601).')
param startDate string

@description('Budget end date (ISO 8601).')
param endDate string

@description('Action group notified on threshold breach.')
param actionGroupName string = 'alpakasoelde-budget-actions'

var actionGroupResourceId = '${subscription().id}/resourceGroups/${resourceGroupName}/providers/Microsoft.Insights/actionGroups/${actionGroupName}'

var notifications = {
  actual_GreaterThan_20_Percent: {
    enabled: true
    operator: 'GreaterThan'
    threshold: 20
    contactGroups: [
      actionGroupResourceId
    ]
    contactEmails: []
    contactRoles: []
  }
  actual_GreaterThan_80_Percent: {
    enabled: true
    operator: 'GreaterThan'
    threshold: 80
    contactGroups: [
      actionGroupResourceId
    ]
    contactEmails: []
    contactRoles: []
  }
  actual_GreaterThan_100_Percent: {
    enabled: true
    operator: 'GreaterThan'
    threshold: 100
    contactGroups: [
      actionGroupResourceId
    ]
    contactEmails: []
    contactRoles: []
  }
}

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
      endDate: endDate
    }
    notifications: notifications
  }
}

output budgetId string = budget.id