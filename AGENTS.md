# AGENTS.md

Alpakasölde: a public Astro marketing site and an internal Astro+Svelte dashboard, each paired with its own .NET 10 isolated Azure Functions API in its own Azure Static Web App, with all data in Azure Table/Blob Storage.

## Commands

- Astro projects (`src/website`, `src/dashboard`) use `pnpm` only; both also carry a stale `package-lock.json`, and CI caches `pnpm-lock.yaml`.
- `pnpm dev` / `pnpm run check` / `pnpm run build` per Astro project — `build` is plain `astro build`, so type and template errors only surface from `pnpm run check` (CI runs both, separately).
- `cd src/dashboard && pnpm run check:svelte` runs `svelte-check`; CI does not, so run it yourself after touching `.svelte`.
- `cd src/<api> && dotnet run` starts a Functions host from the correct output directory; `dotnet build` + `func start` does not work for isolated-worker projects.
- `dotnet build alpakasoelde.slnx` builds both APIs plus the xUnit projects in `src/website-api-tests` and `src/dashboard-api-tests`, which `dotnet test` runs through the Microsoft.Testing.Platform runner pinned in `global.json` — that runner needs `dotnet test --project <csproj>`, not a bare path, when targeting one project.
- `pnpm test` runs Vitest in both Astro projects (`test/**/*.test.ts`, components through Astro's Container API), and `pnpm run test:e2e` runs Playwright on Chromium in `src/website` (`e2e/`, starting its own dev server on port 4331 via `astro.config.e2e.mjs`); both deploy workflows run them, so a failing test blocks the deploy.
- Exercise API endpoints by hand through each project's `requests.http`, and extend it when adding an endpoint.
- Infra: `az bicep build --file infrastructure/main.bicep` (likewise `main-subscription.bicep`) validates; `az deployment group what-if --resource-group RG-Alpakasoelde --template-file infrastructure/main.bicep --parameters infrastructure/main.bicepparam` previews.

## Formatting

- Prettier is configured but the tree was never reformatted (28 files in website, 32 in dashboard fail `format:check`) and CI does not check it, so format only what you touched with `pnpm exec prettier --write <paths>` and never run `pnpm run format`.
- C# indentation is mixed — tabs in most slices, spaces in `gutscheine/`, `GetAlpakaById.cs`, and both `Program.cs` — so match the file you are editing.
- Namespaces are inconsistent by design of history: entities use `dashboard_api.shared.entities` / `website_api.shared.entities`, slices use `DashboardApi.Features.X`; don't renormalize one side alone.

## Website — `src/website`

- Pages in `src/pages`, components in `src/components`, `Layout.astro` and `WorkshopLayout.astro` as layouts, farm palette tokens in `src/styles/global.css`.
- Astro-only: never introduce React, Vue, or Svelte here, keep `<script>` blocks rare, and prefer CSS (`:has`, form-control toggles) for UI state.
- The JSON-LD in `Head.astro` must keep `is:inline set:html={JSON.stringify(...)}`; anything else escapes the payload into invalid structured data.
- `public/sitemap.xml` is hand-written (currently only `/`) because no sitemap integration exists, so new public pages need a manual entry.
- Pages prefixed with `_` such as `_yoga.astro` are intentionally unrouted drafts.
- The pageview beacon in `Layout.astro` fires only on `alpakasoelde.at`/`www.alpakasoelde.at`, so analytics cannot be exercised locally or on a PR preview.
- The contact form posts urlencoded to `/api/send-message` and follows the 303 to read `response.url`, and must keep working with JS disabled, so the endpoint may not become a JSON 200.

## Dashboard — `src/dashboard`

- `src/pages/*.astro` are thin wrappers mounting Svelte islands with `client:only="svelte"`; logic lives in `src/components`, helpers in `src/utils`, charts on `layerchart` + `d3-scale`.
- Use `@lucide/svelte` in Svelte and `lucide-astro` in `.astro` for icons; add no other icon source.
- Data comes from same-origin `/api/...` fetches resolved by the SWA linked API, and there is no base-URL setting, so a dashboard running on `pnpm dev` alone has no API.
- Access control is SWA EasyAuth: `staticwebapp.config.json` limits `/*` to roles `admin` and `collaborator`, and `DashboardNavbar.svelte` reads `/.auth/me`.
- API JSON casing is inconsistent, which is why `utils/gutschein.ts` normalizes camelCase and PascalCase keys — reuse those normalizers instead of trusting one casing.

## APIs — `src/website-api`, `src/dashboard-api`

- One vertical slice per endpoint file under `features/<area>/`, holding the `[Function]` class, `Command`/`Query`/`Result` records, store interfaces, and `Handler`; shared table entities in `shared/entities`.
- Every handler and store interface must be registered in that project's `Program.cs`, otherwise the function fails to resolve only at runtime.
- website-api endpoints are `AuthorizationLevel.Anonymous` because the public site calls them; dashboard-api endpoints are `AuthorizationLevel.Function` because SWA injects the key — keep each side as it is.
- Handlers return a validation/result record that the function maps to problem-details JSON with `title`/`status`/`detail`, and both frontends surface `detail`.
- Environment-variable names belong in each project's `shared/EnvironmentVariables.cs`, and a new setting also needs a Key Vault secret (with `_` written as `-`) plus an entry in the key arrays of `infrastructure/scripts/sync-swappsettings.sh`, which replaces the SWA's entire settings list and therefore silently drops anything missing from those arrays.
- `dashboard-api/features/assistant/` exposes the read handlers as agent tools over Azure OpenAI (`Microsoft.Agents.AI` + the plain `OpenAI` SDK, pinned to 2.12.0 because `Microsoft.Extensions.AI.OpenAI` 10.9.0 excludes 2.13.0), is read-only by construction, and keeps conversation state in the browser as a serialised `AgentSession` rather than in a table.
- `gpt-5-nano` is a reasoning model whose reasoning tokens are drawn from `MaxOutputTokens`, so that budget must leave room for the answer itself (2000 with `ReasoningEffort.Low`; at 800 it returned `finish_reason: length` and no text at all).
- `GetMessages` is deliberately absent from that tool surface and `gutscheine` drops `VerkauftAn`, because the Datenschutzerklärung's Azure OpenAI disclosure is purpose-bound to spam classification — adding either needs a privacy-policy change first.
- The two APIs now use different OpenAI SDKs (`website-api` on `Azure.AI.OpenAI` 2.1.0, `dashboard-api` on `OpenAI` 2.12.0); they are separate projects, so keep the versions apart rather than unifying one side alone.
- `features/messages/SpamClassifier.cs` fails open: missing config or any Azure OpenAI error classifies as legit and still emails, while spam is stored with `IsSpam = true` and never emailed.
- Alpaka images sit in a private `alpakas` blob container and are served as short-lived SAS URLs signed with `AZURE_STORAGE_ACCOUNT_NAME`/`AZURE_STORAGE_ACCOUNT_KEY`, so those are required beyond `StorageConnection` and a key rotation must be followed by `sync-swappsettings.sh`.
- Both `local.settings.json` are committed with empty placeholder values — never paste real connection strings or keys into them.

## Storage invariants

- Partition keys: `alpakas` → `AlpakaPartition`, `messages` → `ContactPartition`, `gutscheine` → `GutscheinePartition`, `events` → the AlpakaId, `pageviews` → `Pv|{yyyy-MM-dd}`.
- A Gutschein's RowKey is its Gutscheinnummer in `{year}{NN}` form, and the next number is guessed client-side in `utils/gutschein.ts`, so concurrent creates can collide.
- One dashboard event covering several alpakas is N rows sharing a `SharedEventId`; write them together and group by it when reading.
- The `pageviews` table also holds a `Cleanup`/`last` marker row driving the 36-month purge that piggybacks on writes, so always query it with a `PartitionKey ge 'Pv|…' and le 'Pv|…'` range rather than scanning.
- Stores call `CreateIfNotExistsAsync` before writing, which is why `pageviews` works despite being absent from the `tables` list in `infrastructure/main.bicepparam`; declare any new table in both places.

## Infrastructure & deploy

- `infrastructure/main.bicep` adopts every existing resource in resource group `RG-Alpakasoelde` in place, `main-subscription.bicep` owns only the cost budget, and secrets live in Key Vault `kv-alpakasoelde`.
- `infra-deploy.yml` aborts the apply when what-if reports any Delete or Replace, so a change forcing resource replacement blocks the pipeline instead of destroying anything — reshape the change.
- `sync-swappsettings.sh` re-applies SWA app settings from the vault after every infra deploy and after a storage-key rotation, while `seed-keyvault.sh` pushes the other direction and overwrites vault secrets with whatever the SWAs currently hold, so run it only for the initial seed.
- `build-and-deploy-website.yml` and `-dashboard.yml` are path-filtered per app+API pair and upload a prebuilt `dist/` plus a `dotnet publish` output with `skip_app_build`/`skip_api_build`, so any pre-deploy step must be added to the workflow rather than an SWA build hook.
- Both `staticwebapp.config.json` declare `apiRuntime: dotnet-isolated:9.0` while the projects target `net10.0`; this deploys fine today, so leave it alone unless a deploy actually fails on it.
- `infrastructure/*.json` are gitignored `az bicep build` artifacts that no deploy reads.

## Docs & commits

- Design docs go to `docs/plans/` numbered in creation order (next is `014-`), looser notes to `docs/ideas/`, never the repository root.
- Commits and PR titles follow Karma/Conventional form `<type>(<scope>): <subject>` with a lowercase imperative subject and no trailing period, scoped by area (`website`, `dashboard`, `website-api`, `dashboard-api`, `infra`); the PR title becomes the squash-merge message.

## Maintaining this file

- When a change invalidates a line here or teaches you a costly lesson, update AGENTS.md in the same change set.
- Prefer deleting over adding and pointers over prose: anything discoverable by reading the file a bullet points to does not belong here.
- One sentence per bullet, current state only, no history or changelog entries.
