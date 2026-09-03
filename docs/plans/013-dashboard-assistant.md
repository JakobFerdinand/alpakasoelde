# Dashboard Assistant Plan

## Goal

Add a chat assistant to the internal dashboard that answers questions in German about the data the app already holds — „Wie viele Besucher hatten wir letzte Woche?", „Welche Seite lief im Juni am besten?", „Wie viele Gutscheine sind noch offen?", „Wann war Richard das letzte Mal beim Scheren?". The assistant reaches that data **only** through the existing read handlers, calls them as tools, and answers from what they return. Read-only by construction: no tool writes, deletes, or sends mail.

## Current state

- **Hosting.** Both apps are `Microsoft.Web/staticSites` on the **Free** SKU (`infrastructure/modules/static-sites.bicep:16-37`). The dashboard's API is a **managed** Functions app: `build-and-deploy-dashboard.yml:78` passes `api_location: "./api_output"`, and `src/dashboard/staticwebapp.config.json` declares `platform.apiRuntime`. There is no linked/bring-your-own backend, and on the Free SKU there cannot be one — BYO APIs are a Standard-plan feature.
- **API.** `src/dashboard-api`, .NET 10 isolated worker, `ConfigureFunctionsWorkerDefaults()` (`Program.cs:13`), one vertical slice per endpoint, every handler and store interface registered in `Program.cs`. Endpoints are `AuthorizationLevel.Function`; the SWA supplies the key and injects `x-ms-client-principal`.
- **Read surface that already exists** — this is the whole tool inventory, and all of it is now unit-tested (`src/dashboard-api-tests`):

  | Handler | Returns |
  | --- | --- |
  | `GetPageViewStats.Handler` | totals, unique paths, top paths, devices, origins, sessions, visitors, navigation types, time series |
  | `GetPageViewSessions.Handler.HandleListAsync` / `HandleDetailAsync` | session summaries; per-session event trace with dwell times |
  | `GetMessageStats.Handler` | total / spam / legit / old counts + weekly series |
  | `GetOldMessageCount.Handler` | count past an age threshold |
  | `GetMessages.Handler` | full message rows — **name, e-mail, phone, body** |
  | `GetGutscheine.Handler` | vouchers incl. `VerkauftAn` (buyer name) |
  | `GetAlpakas.Handler` / `GetAlpakaById.Handler` | alpakas, one with its event history |
  | `Events.GetHandler` | events grouped by `SharedEventId`, with alpaka names |

- **Azure OpenAI already exists.** `infrastructure/modules/openai.bicep` provisions account `openai-alpakasoelde` in `germanywestcentral` with a single `gpt-5-nano` GlobalStandard deployment. Only `src/website-api` uses it (`features/messages/SpamClassifier.cs`); `src/dashboard-api` has no OpenAI config at all — `DashboardApi.Shared.EnvironmentVariables` holds `StorageConnection` and the two storage-account keys and nothing else.
- **Frontend.** Astro 7 + Svelte 5. Pages are thin `.astro` wrappers mounting one island with `client:only="svelte"`. No React anywhere in the repo, and `AGENTS.md` forbids adding UI frameworks to the website. Icons come from `@lucide/svelte`.
- **Auth.** SWA EasyAuth restricts `/*` to roles `admin` and `collaborator` (`staticwebapp.config.json`); `DashboardNavbar.svelte` reads `/.auth/me`.

## Platform constraints

These bound every decision below.

- **45 seconds.** Static Web Apps caps *every* API backend at a 45-second request duration. Not configurable, not tier-dependent.
- **No streaming through the proxy.** `Azure/static-web-apps#1180` — "Streaming does not stream; it arrives in a single payload" — has been open since 2023-05-25 with no fix. Streaming works when the Functions host is called directly, not through `/api`. WebSockets are explicitly unsupported.
- .NET isolated *can* stream (ASP.NET Core integration: `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` + `ConfigureFunctionsWebApplication()`), but that only moves the problem to the proxy.
- **One API backend per environment**, always under `/api`. That slot belongs to `dashboard-api`, and on the Free SKU a second, linked backend is not available at all.

So: **one request, one answer, inside 45 seconds**, served by the Functions app that is already there.

## Decisions

- **No new runtime, no new UI framework.** The assistant is a vertical slice in `src/dashboard-api` — `features/assistant/Assistant.cs`, `POST /api/assistant`, `AuthorizationLevel.Function` — and a Svelte 5 island in the dashboard. Nothing about hosting, auth, CORS or deployment changes.
- **Microsoft Agent Framework (`Microsoft.Agents.AI` 1.20.0, GA) on top of `Microsoft.Extensions.AI` 10.9.0.** `ChatClientAgent` takes an `IChatClient`, instructions and the tool list in its constructor, so the agent is configured once at DI time instead of rebuilding `ChatOptions` on every request, and it runs the tool loop itself. `AIFunctionFactory.Create` (from `Microsoft.Extensions.AI`) still does the tool schemas.

  Two things settled this over using `Microsoft.Extensions.AI` alone:
  - **`AIAgent.SerializeSessionAsync` / `DeserializeSessionAsync`.** Conversation state round-trips through JSON, so the browser can hold the session and post it back — the same "no server-side session store" property this plan wants, but as a first-class abstraction rather than a hand-rolled `ChatTurn[]`.
  - **Built-in tool approval** (`ChatClientAgentOptions.DisableApprovalResponseBinding`, `ToolApprovalResponseContent`). Irrelevant for a read-only v1, but it is exactly the machinery a write tool would need („Gutschein 202512 wirklich einlösen?"), so the read-only start does not paint us into a corner.

  What is **not** used: `Microsoft.Agents.AI.Hosting` (still `1.20.0-preview.260831.1`), workflows, A2A and MCP. The core packages carry no ASP.NET Core dependency, so they run in the isolated worker as an ordinary library — none of the preview surface is on the path.

  Measured cost of MAF over plain `Microsoft.Extensions.AI`: 26 vs 16 packages in the restore closure and a 9.6 MB vs 7.8 MB publish output (+10 DLLs, including `Google.Protobuf` 480 KB and `Microsoft.ML.Tokenizers` 320 KB that nothing here uses). Acceptable, but it is the one honest argument the other way.
- **The plain `OpenAI` SDK, not `Azure.AI.OpenAI`.** `Microsoft.Extensions.AI.OpenAI` 10.9.0 pins `OpenAI [2.12.0, 2.13.0)`, while the `Azure.AI.OpenAI` 2.1.0 that `website-api` uses was built against `OpenAI` 2.1.0 — putting both in one project is a version conflict waiting to happen. Azure's own migration guidance now says to prefer the `OpenAI` SDK alone. So `dashboard-api` references `OpenAI` 2.12.0 — not 2.13.0, which `Microsoft.Extensions.AI.OpenAI` 10.9.0's `[2.12.0, 2.13.0)` range excludes and which therefore restores with an `NU1608` warning — and points a `ChatClient` at the account endpoint plus `openai/v1/`. The endpoint is read from the `OpenAiEndpoint` setting rather than hardcoded, which matters: Azure auto-suffixes the subdomain, so the real value is `https://openai-alpakasoelde-5d6ff.openai.azure.com/`, not the bare account name. `website-api` is **not** touched; the two projects are independent and `SpamClassifier` keeps working as it is.
- **Non-streaming.** `RunAsync`, one JSON reply — not `RunStreamingAsync`. The island shows a „denkt nach…" state. The 45-second budget is enforced server-side with a `CancellationTokenSource` at 40 s so a timeout returns a German error instead of an SWA 504.
- **A separate model deployment — for quota, not for capability.** `gpt-5-nano` is documented as supporting functions, tools and parallel tool calling with a 400 k context, so it is functionally fine here. The reason not to share it is `SpamClassifier`: it **fails open**, so if the assistant exhausts the deployment's TPM the spam filter starts classifying everything as legit and spam gets emailed. A second GlobalStandard deployment (`OpenAiAssistantDeployment`) isolates that blast radius. Start it on `gpt-5-nano` and step up to `gpt-5-mini` only if multi-hop tool planning proves sloppy. Same account, same region, so data stays in `germanywestcentral`.
- **Tools wrap handlers, not stores.** Each tool is a thin method that constructs the handler's `Query`/`Command` record and returns its result. No new data access, no duplicated LINQ, and every tool is already covered by handler tests.
- **Aggregates first; personal data behind an explicit decision.** v1 exposes the aggregate and operational tools. `GetMessages` (bodies, e-mail addresses, phone numbers) is **not** exposed, and `GetGutscheine` results are projected without `VerkauftAn`. See *Privacy*.
- **Every tool result is capped.** Handlers happily return 180 days of rows; the model must not receive them. Each tool takes an explicit window/limit, and the slice clamps rows (≤50) and serialised size (≤32 KB) before handing anything to the model.
- **Conversation state lives in the browser, as a serialised `AgentSession`.** Each reply carries the session JSON from `SerializeSessionAsync`; the next request posts it back and the handler rehydrates it with `DeserializeSessionAsync`. No session table, no new retention question, no new partition key — and the session blob never leaves the authenticated dashboard. Its serialised size is capped (32 KB) and the oldest turns are dropped when it grows past that.
- **The reply carries its trace.** The response includes which tools ran with which arguments, and the island renders them under the answer, so „woher kommt die Zahl?" is answerable without reading logs.
- **The reply carries its cost.** `AgentResponse.Usage` reports input, output, reasoning and cached tokens summed over every tool round of the turn, so the response passes them on and the island keeps a running total for the conversation. The money figure is an estimate: the framework has no pricing table — cost is a billing concept, not a model one — and the Azure retail price API publishes no `gpt-5-nano` meter for `germanywestcentral`, so `AssistantPricing` holds the region's Global Standard nano-tier list rates as constants. The rates travel in the response and the island shows them, so a wrong rate is visible rather than silently wrong.
- **Tool output is data, never instructions.** Referrer hosts, event comments and paths are attacker-influenced strings. The system prompt says so explicitly, and the read-only tool set bounds the damage: there is no tool that writes, deletes, mails or fetches a URL.

## Milestones (tracked)

- [x] Write the plan (`docs/plans/013-dashboard-assistant.md`)
- [x] Confirm Azure OpenAI quota for a second deployment in `germanywestcentral` — verified against the live subscription: `OpenAI.GlobalStandard.gpt-5-nano` is 20 used of 5.000, so the assistant's 10 lands at 30/5.000 and no increase is needed
- [x] `infrastructure`: parameterise `openai.bicep` for a second deployment, wire into `main.bicep`/`main.bicepparam`, `az bicep build` + `what-if`
- [x] `DASHBOARD_KEYS` in `sync-swappsettings.sh`
- [x] Key Vault secret `OpenAiAssistantDeployment` (= `assistant-nano`)
- [x] `dashboard-api`: `shared/EnvironmentVariables.cs`, `local.settings.json` placeholders, package references
- [x] `dashboard-api`: `features/assistant/AssistantTools.cs` + `Assistant.cs`, registration in `Program.cs`
- [x] `dashboard-api-tests`: fake `IChatClient`, tool-dispatch and guard-rail tests
- [x] `dashboard`: `AssistantChat.svelte`, `src/pages/assistent.astro`, navbar entry
- [x] Extend `src/dashboard-api/requests.http`
- [x] Verify builds/checks/tests (`dotnet build`/`dotnet test` under two timezones, `pnpm run check`, `check:svelte`, `pnpm test`)
- [ ] Deploy dashboard SWA

## 1. Assistant slice (`src/dashboard-api`)

New file `src/dashboard-api/features/assistant/Assistant.cs`, namespace `DashboardApi.Features.Assistant`, tabs for indentation (matching the surrounding slices).

```csharp
public sealed record AskCommand(string Question, JsonElement? Session);
public sealed record ToolTrace(string Tool, string Arguments);
public sealed record AskResult(string Reply, JsonElement Session, IReadOnlyList<ToolTrace> Tools, UsageInfo Usage);
public sealed record UsageInfo(long InputTokens, long OutputTokens, long ReasoningTokens, long CachedInputTokens,
    decimal Cost, string Currency, decimal InputPricePerMillion, decimal OutputPricePerMillion);
```

`[Function("assistant")]`, `Route = "assistant"`, POST only. It deserialises the body, rejects an empty question, one over 2 000 characters, or a session blob over 32 KB with problem-details JSON (`title`/`status`/`detail`, matching every other slice), and hands `AskCommand` to `Handler`.

`Handler(AIAgent agent, AssistantTools tools, ILogger<Handler> logger)`:

1. `AgentSession session = command.Session is { } s ? await _agent.DeserializeSessionAsync(s, ct) : await _agent.CreateSessionAsync(ct);`
2. `AgentResponse response = await _agent.RunAsync(command.Question, session, cancellationToken: cts.Token);` — `cts` is linked to the function's token with a 40-second cap.
3. `JsonElement next = await _agent.SerializeSessionAsync(session, cancellationToken: ct);`
4. Return `response.Text`, the re-serialised session, and the tool invocations recorded by `AssistantTools`.

The function logs the caller from `x-ms-client-principal` together with the question and the tools that ran — the assistant is a new lever on the data and its use should be attributable.

Registration in `Program.cs`. The round cap is the subtle part: `MaximumIterationsPerRequest` lives on `FunctionInvokingChatClient`, not on `ChatClientAgentRunOptions`, so the client is wrapped **before** the agent is built and `UseProvidedChatClientAsIs` stops `ChatClientAgent` re-wrapping it and discarding the cap.

```csharp
services.AddSingleton<IChatClient>(sp => new OpenAI.Chat.ChatClient(
        model: deployment,                                   // Azure deployment name
        credential: new ApiKeyCredential(apiKey),
        options: new OpenAIClientOptions { Endpoint = new Uri($"{endpoint}openai/v1/") })
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 4)
    .Build());

services.AddScoped<AIAgent>(sp => new ChatClientAgent(
    sp.GetRequiredService<IChatClient>(),
    new ChatClientAgentOptions
    {
        Name = "alpaka-assistent",
        UseProvidedChatClientAsIs = true,                     // keep the iteration cap above
        ChatOptions = new ChatOptions
        {
            Instructions = SystemPrompt,
            Tools = sp.GetRequiredService<AssistantTools>().All,
            MaxOutputTokens = 2000,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
        },
    }));

services.AddScoped<AssistantTools>();
services.AddScoped<Assistant.Handler>();
```

`AssistantTools` and the agent are scoped because the tools depend on the scoped read handlers and accumulate the per-request tool trace.

`AssistantTools` is scoped because it depends on the scoped read handlers and because it accumulates the per-request tool trace.

Packages added to `src/dashboard-api/dashboard-api.csproj`: `Microsoft.Agents.AI` 1.20.0, `Microsoft.Extensions.AI` 10.9.0, `Microsoft.Extensions.AI.OpenAI` 10.9.0, `OpenAI` 2.12.0 (the last three arrive transitively but are pinned explicitly, because the `Azure.AI.OpenAI` conflict above makes the exact `OpenAI` version load-bearing). `Microsoft.Agents.AI.OpenAI` is **not** referenced: it only adds OpenAI-native response conversions that nothing here calls, while dragging in `Microsoft.Extensions.AI.Evaluation`, `Microsoft.Extensions.VectorData.Abstractions` and `Microsoft.ML.Tokenizers`. Key-based auth matches the rest of the estate (`OpenAiApiKey` in Key Vault); switching this account to managed identity would be an improvement, but it is a separate change touching `website-api` too.

## 2. Tool surface

`features/assistant/AssistantTools.cs` — one method per tool, each `[Description]`-annotated in German so the model picks correctly, each returning a small record. `All` is `[AIFunctionFactory.Create(PageViewStats, name: "besucher_statistik"), …]`; the factory derives the parameter schema from the signature and serialises return values back as tool results. `CancellationToken` parameters are injected by the framework and never shown to the model. Keep each description under the service-side 1 024-character cap.

| Tool | Wraps | Parameters | Notes |
| --- | --- | --- | --- |
| `besucher_statistik` | `GetPageViewStats.Handler` | `tage` (1–180), `gruppierung` (`gesamt`/`pfad`/`gerät`/`herkunft`) | series truncated to the last 26 buckets |
| `sitzungen_liste` | `GetPageViewSessions.HandleListAsync` | `tage`, `mindestSeiten`, `limit` (≤25) | summaries only |
| `sitzung_detail` | `GetPageViewSessions.HandleDetailAsync` | `sitzungsId` | events capped at 50 |
| `nachrichten_statistik` | `GetMessageStats.Handler` | `tage` | counts + series, **no bodies** |
| `alte_nachrichten` | `GetOldMessageCount.Handler` | `tageSchwelle` | one number |
| `gutscheine` | `GetGutscheine.Handler` | `nurOffen` (bool) | projected **without** `VerkauftAn` |
| `alpakas` | `GetAlpakas.Handler` | — | id, name, birth date; image URLs stripped (SAS links are useless to the model and leak signed URLs) |
| `alpaka_detail` | `GetAlpakaById.Handler` | `alpakaId` | incl. its events |
| `ereignisse` | `Events.GetHandler` | `vonDatum?`, `bisDatum?` | filtered in the tool, capped at 50 |
| `heute` | — | — | returns today's date in `Europe/Vienna`; without it the model cannot resolve „letzte Woche" |

A shared `Clamp` helper enforces the row and byte caps and appends a `hinweis: "gekürzt"` marker when it truncates, so the model can say so rather than silently answering from a partial list.

## 3. Model and infrastructure

- `infrastructure/modules/openai.bicep` currently declares exactly one `deployments` child. Change it to take an **array** of `{name, model, version, capacity}` and loop, so the existing `gpt-5-nano` deployment and the new assistant deployment coexist. The existing deployment's name, model and version must stay byte-identical — `infra-deploy.yml` aborts the apply if what-if reports a Delete or Replace.
- New parameters in `main.bicep`/`main.bicepparam`: `openAiAssistantDeploymentName = 'assistant-nano'` on model `gpt-5-nano`, plus version and capacity. Naming the *deployment* separately from the model is what keeps the quota isolated while still starting on the cheap model.
- **Confirm quota first.** Plan `002` recorded 5 000 TPM granted for `OpenAI.GlobalStandard.gpt-5-nano` on this subscription, and quota is per subscription/region/model/deployment-type — a second deployment of the same model draws from the same pool unless the grant is raised. A tool-calling assistant sends the tool schemas plus the conversation plus every tool result on every round; budget roughly 10–20 k tokens per question. Raise the grant (or place the assistant on `gpt-5-mini`, which has its own pool) if the numbers say so — but they do not: the deployment ships at capacity 10 next to the classifier's 20, against a grant of 5.000, so the assistant stays inside the existing pool and the quota is **not** a blocker.
- `gpt-5-nano` and `gpt-5-mini` both retire 2027-02-09; the deployment version is a parameter so the bump is a one-line infra change.
- **Verified against the live subscription (2026-09-03).** Quota `OpenAI.GlobalStandard.gpt-5-nano` reads 20 used of 5.000, and the deployed `gpt-5-nano` is model `gpt-5-nano`/version `2025-08-07`/GlobalStandard/capacity 20 — byte-identical to what the template declares. `what-if` run against this branch and against `main` produces the same change set except for a single **Create** of `assistant-nano`: no Delete, no Replace, and the seven pre-existing `Modify` entries (server-defaulted `currentCapacity`, `raiPolicyName`, `versionUpgradeOption` and the static-site properties) are unchanged drift that predates this work.
- Key Vault `kv-alpakasoelde` gains `OpenAiAssistantDeployment` (secret name `OpenAiAssistantDeployment`); `OpenAiEndpoint`/`OpenAiApiKey` already exist.
- **`sync-swappsettings.sh` `DASHBOARD_KEYS` must be extended** to `(StorageConnection AZURE_STORAGE_ACCOUNT_KEY AZURE_STORAGE_ACCOUNT_NAME OpenAiEndpoint OpenAiApiKey OpenAiAssistantDeployment)`. That array replaces the SWA's entire settings list; anything missing from it is silently dropped on the next run.
- `DashboardApi.Shared.EnvironmentVariables` gains the three names; `src/dashboard-api/local.settings.json` gains empty placeholders (never real values).

## 4. Dashboard UI

- `src/pages/assistent.astro` — thin wrapper, `<AssistantChat client:only="svelte" />`, title „Assistent".
- Navbar entry „Assistent" in `DashboardNavbar.svelte`, after „Sitzungen".
- `src/components/AssistantChat.svelte`, Svelte 5 runes, matching the existing islands' shape (`let messages = $state([])`, `loading`, `error`, `onMount`-free since nothing loads up front):
  - `let session = $state<unknown>(null)` holds the opaque session blob from the last reply and is posted back with the next question; the rendered bubbles are the island's own display list and are never what the model reads. „Neues Gespräch" clears both.
  - message list with user/assistant bubbles using the farm palette tokens from `global.css`;
  - a textarea + send button; Enter sends, Shift+Enter breaks;
  - while waiting: a disabled input and a „denkt nach…" indicator (there is no token stream to show);
  - under each answer, a collapsed „Verwendete Daten" block listing the `Tools` trace and that answer's tokens and cost;
  - a quiet strip showing the conversation's running token and cost total, reset by „Neues Gespräch";
  - errors render the `detail` from the problem-details body, like the other islands;
  - a few German starter prompts as buttons on the empty state, so the tool surface is discoverable.
- Icons from `@lucide/svelte` (`Sparkles`, `Send`, `ChevronDown`). No new npm dependencies.

## 5. Privacy

- The Datenschutzerklärung already discloses Azure OpenAI (`gpt-5-nano`, Germany West Central) processing **contact-form messages for spam classification**, covered by the Microsoft DPA. That disclosure is purpose-bound.
- Sending message bodies, e-mail addresses or buyer names to the model for *assistant* purposes is a **new purpose** and would require updating `src/website/src/pages/datenschutzerklaerung.astro`. v1 avoids this: `GetMessages` is not a tool, and `gutscheine` drops `VerkauftAn`.
- Pageview data carries `SessionId`/`VisitorId`, already disclosed under plan `010`. Session tools pass them through; they are pseudonymous identifiers the dashboard already displays.
- The assistant deployment stays on the existing account in `germanywestcentral`, so no data leaves the region and no new DPA is needed. Azure OpenAI does not use customer prompts to train models.
- If a later phase wants message search, it needs: a Datenschutzerklärung update, a decision on redaction (e-mail/phone masked by default), and a note in this plan — not a quiet tool addition.

## 6. Tests

The point of putting this in .NET is that it stays testable with the harness that now exists.

- **`FakeChatClient : IChatClient`** in `dashboard-api-tests/Fakes/` — a hand-written implementation returning a scripted queue of `ChatResponse` objects and recording the messages and `ChatOptions` it was handed. `IChatClient` has four members (`GetResponseAsync`, `GetStreamingResponseAsync`, `GetService`, `Dispose`), so no mocking library is needed and none is currently referenced. Choosing MAF does not change this: `ChatClientAgent` takes an `IChatClient`, so the test builds the agent over the fake exactly as `Program.cs` builds it over the real one — `.AsBuilder().UseFunctionInvocation().Build()` plus `UseProvidedChatClientAsIs = true`, which makes the **real** tool methods execute against the in-memory store fakes, deterministically and offline: turn 0 returns a `FunctionCallContent`, turn 1 returns the final text.
- `AssistantHandlerTests`: a scripted tool call reaches the right handler with the right arguments; the final text is returned verbatim; the tool trace matches what ran; an empty question or an oversized session blob is rejected before any model call; a model that keeps calling tools stops at `MaximumIterationsPerRequest`; the 40-second cancellation surfaces as a German error rather than an exception.
- `AssistantSessionTests`: a session serialised out of one request and posted back into the next carries the earlier turns (a follow-up question like „und im Mai?" resolves against the previous one); a missing session starts a fresh conversation; a malformed session blob is rejected rather than throwing.
- `AssistantToolsTests`: each tool clamps rows and bytes and sets the `gekürzt` marker; `gutscheine` never emits `VerkauftAn`; `alpakas` never emits a signed URL; `heute` returns the Vienna date. These run against the existing in-memory store fakes, so no network and no clock dependence beyond what the handlers already have.
- No test calls Azure OpenAI.

## Verification

- `dotnet build alpakasoelde.slnx` and `dotnet test` green; the .NET suite also under `TZ=UTC` and a non-UTC zone.
- `cd src/dashboard && pnpm run check`, `pnpm run check:svelte`, `pnpm test`.
- `az bicep build --file infrastructure/main.bicep` and `az deployment group what-if …` showing **no** Delete or Replace on the existing OpenAI deployment.
- Manual matrix against `dotnet run` in `src/dashboard-api` with new `requests.http` entries: a question needing one tool, one needing two, one needing none („Wer bist du?"), one about data the tools cannot reach (must say so rather than invent), a follow-up that only makes sense with the returned session echoed back, and an over-long session blob (400).
- Browser walkthrough: ask each starter prompt, expand the „Verwendete Daten" block and check the numbers against the corresponding dashboard page — the assistant and `/pageviews` must agree.
- Squash title: `feat(dashboard): add a data assistant to the dashboard`.

## Known limitations / notes

- **No streaming, by platform.** Answers appear all at once after up to ~40 s. If that proves too slow to live with, the upgrade path that preserves EasyAuth is **Azure SignalR Service**: a negotiate endpoint stays on `/api` (so the SWA still authenticates), and the Functions app pushes tokens over the SignalR connection, bypassing the proxy. Calling the Functions host directly from the browser also works but means rebuilding role checks. Both are phase 2.
- **The 45-second cap bounds the agent loop.** `MaximumIterationsPerRequest = 4`. A question needing more is answered with „Das brauche ich in mehreren Schritten — frag mich bitte gezielter."
- **Two OpenAI SDKs now coexist in the repo** — `website-api` on `Azure.AI.OpenAI` 2.1.0, `dashboard-api` on `OpenAI` 2.13.0. They are separate projects so nothing conflicts at build time, but the eventual tidy-up is to migrate `SpamClassifier` to the `OpenAI` SDK too, which Azure's migration guidance now recommends. Out of scope here: it would touch a fail-open code path with no way to test the failure mode against the real service.
- **Reflection-based tool schemas need JIT.** Fine on the isolated worker as configured; only a Native AOT publish would require source-generated `JsonSerializerOptions` via `AIJsonUtilities`.
- **`gpt-5-nano` spends `MaxOutputTokens` on reasoning before it writes a word.** The first deployed version used 800 and answered normal questions with an empty string and `finish_reason: length` — the tools had run correctly, but the reasoning tokens had eaten the whole budget, so the island showed the „mehrere Schritte" fallback. Measured against the live deployment at a realistic prompt size (1.589 tokens): default effort burns ~768 reasoning tokens per round and 4,4 s, `Effort = Low` burns ~64 and 1,6 s for the same answer. Hence 2000 tokens and low effort — and the empty-text fallback now separates `FinishReason.Length` from a genuinely exhausted tool loop, because telling the user to „frag gezielter" when the budget was the problem sends them the wrong way.
- **The model can still be wrong about what it reads.** The tool trace is the mitigation: every number is traceable to a tool call whose equivalent is visible on a dashboard page.
- **Quota is the real dependency.** One assistant question costs orders of magnitude more tokens than one spam classification. The separate deployment protects the spam filter, but the assistant itself will 429 under a tight TPM grant — confirm quota before building.
- **`GetGutscheine`, `GetMessages` and `Events` load whole tables** (`Query<T>()` with no filter). That is the repo's documented stance at current volume, and the tools clamp what reaches the model, but the *Functions host* still materialises everything on every tool call — the same cost the dashboard pages already pay.
