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

module budget './modules/budget.bicep' = {
  name: 'budget'
  params: {
    resourceGroupName: resourceGroupName
    budgetName: budgetName
    amount: amount
    startDate: startDate
    endDate: endDate
    actionGroupName: actionGroupName
  }
}