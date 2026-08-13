# AI Spam Filter Plan

## Goal

Reduce spam emails from the public contact form (`src/website` →
`src/website-api/features/messages/SendMessage.cs`) by classifying each
submission with **Azure OpenAI `gpt-5-nano`** before the notification email is
sent. Spam submissions are stored with an `IsSpam` flag but never emailed;
genuine messages are unaffected. The classification must **fail open**: if the
AI call errors, the message is treated as legit and still emailed so real
inquiries are never lost.

## Decisions

- **Service:** Azure OpenAI (single account, Global Standard deployment).
- **Model:** `gpt-5-nano` — cheapest tier, purpose-built for high-volume
  classification. Verified accessible: quota `OpenAI.GlobalStandard.gpt-5-nano`
  = 5,000 TPM is granted on this subscription.
- **Region:** `germanywestcentral` (same as the rest of the estate; Global
  Standard quota confirmed there).
- **Behaviour on spam:** silently drop the email, store the record with an
  `IsSpam = true` flag in the `messages` table; keep the same
  `/nachricht-gesendet` redirect for bots.
- **On AI error:** log a warning, treat as legit, send the email anyway.
- **SDK/API:** `Azure.AI.OpenAI` (`AzureOpenAIClient`), prompt returns strict
  JSON `{"isSpam": bool}`; no output-token cap is sent because gpt-5 requires
  `max_completion_tokens` (the classifier's output is tiny).
- **Config:** `OpenAiEndpoint` + `OpenAiApiKey` + `OpenAiDeployment` live in
  `kv-alpakasoelde`, reach the Static Web App (and its Functions) via
  `sync-swappsettings.sh`.
- **SDK note:** `Azure.AI.OpenAI` 2.x (api2) is used. Earlier attempts mixed the
  beta `Azure.AI.Inference` (`ChatCompletionsClient`) and a `max_tokens` cap,
  which the model rejected (`'max_tokens' is not supported … use
  'max_completion_tokens'`), so the classifier now sends a minimal request
  without an output-token cap.
- **Key Vault secrets:** created via `az keyvault secret set` after the first
  deployment (needs the OpenAI account + key to exist), not via Bicep.
  Confirmed present in `kv-alpakasoelde` (`OpenAiEndpoint`, `OpenAiApiKey`,
  `OpenAiDeployment`).

## Milestones (tracked)

Checkboxes are updated as work progresses.

- [x] Create git branch `feat/ai-spam-filter`
- [x] Write the plan (`docs/plans/002-ai-spam-filter.md`)
- [x] Add `infrastructure/modules/openai.bicep` (account + `gpt-5-nano`
      Global Standard deployment) and wire it into `main.bicep`
- [x] Validate templates locally (`az bicep build` + `az deployment group what-if`)
- [x] Add `OpenAiEndpoint` / `OpenAiApiKey` / `OpenAiDeployment` secrets to Key
      Vault (confirmed present in `kv-alpakasoelde`);
      `WEBSITE_KEYS` in `sync-swappsettings.sh` updated
- [x] Add `OpenAiEndpoint` / `OpenAiApiKey` / `OpenAiDeployment` to
      `EnvironmentVariables.cs` and dev placeholders in `local.settings.json`
- [x] Add `Azure.AI.OpenAI` package (api2 `AzureOpenAIClient`) to
      `src/website-api/website-api.csproj`
- [x] Add `features/messages/SpamClassifier.cs` (`ISpamClassifier` +
      `OpenAiSpamClassifier`, fail-open)
- [x] Register the classifier in `src/website-api/Program.cs`
- [x] Add `IsSpam` to `shared/entities/MessageEntity.cs`
- [x] Update `SendMessage.Handler` to classify before emailing and skip the
      email for spam
- [x] Extend `requests.http` with a spam and a legit sample
- [x] Build functions (`dotnet build src/website-api/website-api.csproj`)
- [ ] Deploy and verify end to end (spam not emailed, legit emailed) — verified
      locally against real `gpt-5-nano` (`IsSpam` flags stored correctly, spam
      email skipped, legit email sent); deploy on `main` merge
- [x] Update `AGENTS.md` (new environment keys, module wiring) and README
- [ ] Open pull request and merge to `main` (PR opened, merge pending)

## 1. Infrastructure

New `infrastructure/modules/openai.bicep`:

- `Microsoft.CognitiveServices/accounts` (kind `OpenAI`, sku `S0`) in
  `germanywestcentral`.
- Child `Microsoft.CognitiveServices/accounts/deployments` for `gpt-5-nano`
  with `skuGlobalStandard`.
- Outputs the endpoint; the API key is read via `listKeys()` in the module.

`main.bicep` gains an `openai` module and, via the key vault module, two new
secrets:

- `OpenAiEndpoint`
- `OpenAiApiKey`

## 2. Config plumbing

- `infrastructure/scripts/sync-swappsettings.sh`: add `OpenAiEndpoint` and
  `OpenAiApiKey` to `WEBSITE_KEYS` so the public SWA (and its .NET isolated
  API) receives them after each deploy.
- Seed the three secrets into `kv-alpakasoelde` once (after the OpenAI account
  exists):
  - `az keyvault secret set --vault-name kv-alpakasoelde --name OpenAiEndpoint --value ...`
  - `az keyvault secret set --vault-name kv-alpakasoelde --name OpenAiApiKey --value ...`
  - `az keyvault secret set --vault-name kv-alpakasoelde --name OpenAiDeployment --value gpt-5-nano`
- `src/website-api/shared/EnvironmentVariables.cs`: add the three keys.
- `src/website-api/local.settings.json`: dev placeholders (no real secrets).

## 3. Code (`src/website-api`)

`features/messages/SpamClassifier.cs` (vertical-slice style, following the
`SendMessage.cs` pattern):

- `ISpamClassifier` interface, e.g.
  `Task<bool> IsSpamAsync(string name, string email, string message, CancellationToken ct)`.
- `OpenAiSpamClassifier` implementation using `AzureOpenAIClient`
  (`client.GetChatClient(deployment)`), a German system prompt asking for strict
  JSON `{"isSpam": bool}`; no extra parameters — gpt-5 needs
  `max_completion_tokens`, not `max_tokens`.

`SendMessage.Handler` (after validation, before storing/emailing):

1. Classify the submission.
2. Spam → store with `IsSpam = true`, skip `_emailSender.SendAsync`, log.
3. Not spam (or classifier error) → current behaviour unchanged.
4. Redirect `/nachricht-gesendet` in both cases.

`shared/entities/MessageEntity.cs`: add `bool IsSpam { get; set; }`.

`Program.cs`: register `ISpamClassifier` → `OpenAiSpamClassifier`.

## 4. Verification

- `dotnet build src/website-api/website-api.csproj`
- `az bicep build --file infrastructure/main.bicep`
- `az deployment group what-if --resource-group RG-Alpakasoelde --template-file infrastructure/main.bicep --parameters infrastructure/main.bicepparam`
- Extend `requests.http` with a spam and a legit payload.
- Cost estimate: ~$0.00005 per classified message at real volume.

## Known limitations / notes

- `gpt-5`-family access is registration-gated by Azure; non-zero quota on this
  subscription indicates access is enabled, but the definitive test is the model
  deployment succeeding. If it errors with access restrictions, submit the
  registration form (`aka.ms/openai/gpt-5/2025-08-07`) and retry.
- Global Standard deployments route requests across Azure's global
  infrastructure; data residency is regional rather than a strict
  single-region guarantee.
- The deployment name is exposed via config so a future model swap (e.g. to
  `gpt-4.1-mini` or a newer `gpt-5.x`) is a Bicep + config change only.