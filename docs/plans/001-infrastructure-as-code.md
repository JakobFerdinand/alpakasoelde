# Infrastructure as Code Plan

## Goal

Bring the Azure estate for Alpakasoelde under Infrastructure as Code using **Bicep**
and auto-deploy infrastructure changes through **GitHub Actions**. Only changes to
`infrastructure/**` trigger the infra deployment; the existing app build-and-deploy
workflows stay untouched.

## Current Azure estate (resource group `RG-Alpakasoelde`)

| Resource | Name | Notes |
| --- | --- | --- |
| Static Web App (public) | `alpakasoelde` | Free, `westeurope`; custom domains `alpakasoelde.at` + `www.alpakasoelde.at`; built-in .NET isolated API; appsettings contain plain-text storage + ACS keys |
| Static Web App (dashboard) | `alpakasoelde-dashboard` | Free, `westeurope`; domain `dashboard.alpakasoelde.at`; same storage appsettings |
| Storage account | `alpakasoelde` | Standard_LRS / StorageV2, `germanywestcentral`; tables `alpakas`, `events`, `gutscheine`, `messages`; blob containers `alpakas`, `event-documents`; HTTPS-only |
| Communication Services | `acs-alpakasoelde` | dataLocation `germany`; source of the `EmailConnection` used by the website API |
| Email services | `alpakasoelde` | domains `AzureManagedDomain`, `kontakt.alpakasoelde.at`; sender `DoNotReply@kontakt.alpakasoelde.at` |
| Application Insights | `alpakasoelde-insights` | linked to the Log Analytics workspace |
| Log Analytics | `Alpakasoelde-LogAnalyticsWorkspace` | 30-day retention |
| Action groups / budget | Smart Detection, `alpakasoelde-budget-actions` | subscription-level cost budget |

Deployment today happens via the auto-generated Static Web App GitHub-integration
workflows. There is no IaC in the repo yet (AGENTS.md references a
`infrastructure/table-storage.bicep` that does not exist).

## Approach: adopt existing resources in place

Bicep declares all resources with their existing names, resource group, location,
and SKU, so the first deployment is an idempotent adopt with no recreation or
downtime. `what-if` is used to verify this before applying.

## Milestones (tracked)

Checkboxes are updated as work progresses.

- [x] Create git branch `feat/infrastructure-as-code`
- [x] Write infrastructure plan (`docs/plans/001-infrastructure-as-code.md`)
- [x] Finalize plan decisions (Key Vault name, budget values, seed script,
      storage-key rotation, `what-if` PR job)
- [x] Scaffold Bicep templates (`main.bicep`, `main-subscription.bicep`, modules,
      `*.bicepparam`, `bicepconfig.json`)
- [x] Validate templates locally (`az bicep build` + `az deployment group what-if`)
- [x] Write `infrastructure/scripts/seed-keyvault.sh`
- [x] Write `.github/workflows/infra-deploy.yml` (`what-if` PR job + deploy on main)
- [x] Manual: create service principal + OIDC federated credential, add GitHub
      secrets (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`)
- [x] Seed Key Vault with existing SWA settings (run `seed-keyvault.sh`)
- [x] First deploy: review `what-if` → apply → verify sites stay live and app
      settings are restored
- [x] Rotate storage account key; sync new key into Key Vault; re-apply SWA settings
- [x] Update `AGENTS.md` and README (fix stale `table-storage.bicep` reference, add
      deployment commands)
- [ ] Open pull request and merge to `main`

## 1. Bicep structure under `infrastructure/`

```
infrastructure/
  main.bicep              # RG-scoped orchestrator (targetScope = resourceGroup)
  main.bicepparam         # values: RG name, location, resource names, custom domains
  main-subscription.bicep # subscription-scoped deployment (cost budget)
  main-subscription.bicepparam
  bicepconfig.json        # lint rules
  scripts/
    seed-keyvault.sh      # one-off: reads existing SWA settings into kv-alpakasoelde (no output of secrets)
  modules/
    storage.bicep         # storage account + tables + blob containers (adopt in place)
    communication.bicep   # CommunicationServices + EmailServices + 2 domains
    static-sites.bicep    # both SWAs + customDomains + Application Insights wiring
    keyvault.bicep        # NEW kv-alpakasoelde (secrets for app settings)
    observability.bicep   # Log Analytics workspace + App Insights component + action groups
    budget.bicep          # subscription cost budget + `alpakasoelde-budget-actions` action group
```

Details:

- Tables (`alpakas`, `events`, `gutscheine`, `messages`) and blob containers
  (`alpakas`, `event-documents`) are declared via `tableServices/tables` and
  `blobServices/containers`.
- `communication.bicep` reproduces the email domains and the `linkedDomains`
  wiring on the Communication Services resource. Domain DNS verification remains a
  documented manual step; Bicep cannot verify DNS records.
- Custom domains are declared as `staticSites/customDomains` child resources, but
  gated on a `what-if` review first: if `what-if` reports a destructive `Replace`,
  they are left out of IaC initially and stay portal-managed (they are already
  configured).
- A new Key Vault `kv-alpakasoelde` is introduced as the secret source.
- The subscription-scoped deployment `main-subscription.bicep` provisions the cost
  budget and its action group via `az deployment sub create`.

## 2. Secret management

Static Web App app settings do not support Key Vault references, so:

- A new **Key Vault `kv-alpakasoelde`** is created and seeded with the existing
  secrets: `StorageConnection`, `AZURE_STORAGE_ACCOUNT_KEY`, `EmailConnection`,
  `EmailSenderAddress`, `ReceiverEmailAddresses`, `AZURE_STORAGE_ACCOUNT_NAME`.
- A documented one-off **seed script** (`infrastructure/scripts/seed-keyvault.sh`)
  reads the current SWA app settings and writes them into the Key Vault, without
  printing secret values to the console.
- The infra workflow reads the secrets from Key Vault and sets the full SWA app
  settings after each deployment (`az staticwebapp appsettings set` is a full
  overwrite, so the complete current settings are reproduced to avoid wiping them).
- As a hardening step after the switchover, the **storage account key is rotated**
  and the new key synced into the Key Vault, so legacy key copies in the SWA
  settings become stale.
- Secrets live in Key Vault, never in Bicep files or in the repository.

## 3. GitHub Actions – infra auto-deploy

New workflow `.github/workflows/infra-deploy.yml`:

- **Triggers:** `push` to `main` with paths `infrastructure/**`, and `pull_request`
  for a `what-if` preview job; both also support `workflow_dispatch`.
- **Deploy identity (one-time setup):**
  1. Create a service principal for the repo.
  2. Add an OIDC federated credential for this GitHub repository.
  3. Grant `Contributor` on `RG-Alpakasoelde` and Key Vault secret-read access.
  4. Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` in repo secrets.
- **Jobs:**
  - `what-if` (pull requests): `Azure/login@v2` → `az bicep build` →
    `az deployment group what-if` → post the diff as a PR comment, so infra PRs
    show their impact before merge.
  - `deploy` (main): `Azure/login@v2` → `az bicep build` →
    `az deployment group what-if` → `az deployment group create --confirm-with-what-if`
    → `az deployment sub create` for the budget → set SWA app settings from Key Vault.

## 4. Rollout order (safe, no downtime)

1. **One-time prep:** create service principal + OIDC credential, add GitHub
   secrets; run `infrastructure/scripts/seed-keyvault.sh` to populate
   `kv-alpakasoelde` with the existing SWA settings. If the subscription has
   never used Key Vault, register the provider first (`az provider register -n
   Microsoft.KeyVault`). Note: Key Vault secret names only allow alphanumerics
   and hyphens, so setting names are stored with `_` replaced by `-`.
2. Commit the Bicep templates plus `infra-deploy.yml`; run a `workflow_dispatch`-ed
   `what-if` to confirm no destructive changes on the Static Web Apps or storage
   (the adoption hotspot).
3. Deploy; verify the sites stay live, app settings are restored, and the function
   APIs still answer.
4. **Rotate** the storage account key, update the `StorageConnection` /
   `AZURE_STORAGE_ACCOUNT_KEY` secrets in Key Vault, and let the infra workflow
   re-apply the new values to the SWA app settings.
5. Update `AGENTS.md` (fix the old `infrastructure/table-storage.bicep` reference and
   add the `az deployment group/sub …` commands) and README.

## 5. Known limitations (kept manual, documented)

- Dashboard GitHub auth provider and the `admin`/`collaborator` role-to-user mapping
  are portal-managed and cannot be fully configured via Bicep.
- Email domain DNS verification records.

## Decisions

- **Bicep** (RG-scoped `main.bicep` + subscription-scoped `main-subscription.bicep`),
  adopting existing resources in place.
- **Key Vault:** named `kv-alpakasoelde`; all existing SWA app settings migrate in
  via the seed script; infra workflow re-applies them after each deploy.
- **App settings:** sourced from Key Vault and applied by the infra workflow;
  existing app build-and-deploy workflows stay untouched.
- **Budget:** included in IaC; adopts the existing `Alpakasoelde-Budget` —
  €3/month, monthly grain, notifications at 20/80/100% to
  `alpakasoelde-budget-actions`.
- **Storage key rotation:** rotate after migration and sync the new key into Key
  Vault so legacy copies go stale.
- **Pull requests:** infra changes run a `what-if` job that posts the diff as a PR
  comment.