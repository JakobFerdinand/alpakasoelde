# Repository Guidelines

## Project Structure & Module Organization
- `src/website`: Astro marketing site with pages in `src/pages`, shared UI in `src/components`, and static assets under `public/`.
- `src/dashboard`: Internal Astro dashboard; place screens in `src/pages` and reusable pieces in `src/components`.
- `src/dashboard-api`: .NET 10 isolated Azure Functions for data ingestion and storage, co-locating triggers with their entity models.
- `src/website-api`: Public-facing Azure Functions that mirror the patterns from `src/dashboard-api`.
- `infrastructure/`: Bicep templates that adopt the whole Azure estate in place (storage, Key Vault `kv-alpakasoelde`, Communication/Email services, both Static Web Apps, Azure OpenAI `gpt-5-nano` for the contact-form spam filter, observability). `main.bicep` is resource-group scoped; `main-subscription.bicep` covers the cost budget. Secrets live in the Key Vault; `scripts/seed-keyvault.sh` migrates existing SWA app settings once, and `scripts/sync-swappsettings.sh` re-applies them from the vault after deploys.
- `.slnx` solution: use `alpakasoelde.slnx` to open all projects together; `global.json` pins .NET SDK 10.0.0 with the new test runner.
- `docs/plans/`: design/roadmap docs kept for reference, numbered in creation order (`001-infrastructure-as-code.md`, `002-ai-spam-filter.md`); add new plans here instead of the repository root.

## Build, Test, and Development Commands
- Use `pnpm` (not `npm`) for all package manager commands in the Astro projects.
- `cd src/website && pnpm install && pnpm dev` — launches the marketing site with hot reload.
- `cd src/website && pnpm run build` — runs `astro check` and builds to `dist/`.
- `cd src/dashboard && pnpm install && pnpm dev` — starts the internal dashboard; run `pnpm run build` before shipping changes.
- `cd src/dashboard-api && dotnet run` — compiles and serves the dashboard API locally (Azure Functions Core Tools required). `dotnet run` builds the project and starts the Functions host from the correct output directory, so prefer it over `dotnet build` + `func start` for .NET isolated projects.
- `cd src/website-api && dotnet run` — same workflow for the public API facade.
- Infrastructure: `az bicep build --file infrastructure/main.bicep` and `az bicep build --file infrastructure/main-subscription.bicep` validate the templates; `az deployment group what-if --resource-group RG-Alpakasoelde --template-file infrastructure/main.bicep --parameters infrastructure/main.bicepparam` previews resource-group changes. Deploy with `az deployment group create ...` and `az deployment sub create --location westeurope --template-file infrastructure/main-subscription.bicep --parameters infrastructure/main-subscription.bicepparam`; the `infra-deploy.yml` workflow runs these on `main`. After deploys (or after rotating the storage key), re-apply SWA settings with `bash infrastructure/scripts/sync-swappsettings.sh`.

## Coding Style & Naming Conventions
- Use two-space indentation in Astro/TS files, PascalCase component filenames, and keep copy in dedicated `.astro` or `.md` fragments.
- Co-locate styles with the component and rely on the shared CSS variables exposed by the layout.
- In C#, keep one public type per file, use PascalCase for types, camelCase for locals, and `const` for shared environment keys.
- Azure Functions follow a vertical-slice layout: define command/query records, handler, interfaces (stores/utilities), and function entry in the same file; prefer dependency injection via `Program.cs`.
- Shared table entities live under `src/*/shared/entities`; reuse them from slices instead of duplicating.
- Prefer modern CSS capabilities (e.g., `:has`, form/visibility toggles) over JavaScript for UI state where possible; keep client-side scripts lean.
- Dashboard UI: whenever an icon is needed, use the installed Astro Lucide icon pack instead of introducing other icon sources.

## Astro Best Practices (website & dashboard)
- Use `.astro` components exclusively; do not introduce React, Vue, Svelte, or other framework components. Ship zero client JS by default.
- Use scoped `<style>` blocks inside each component; avoid global CSS except for design tokens and resets in `global.css`.
- Ensure colour contrast meets WCAG AA; explicitly set foreground colours when backgrounds change to prevent inheritance issues.
- Keep visual patterns consistent: when multiple sections share a layout (headers, cards, lists), extract or align their markup and styles so they match.
- Validate props with TypeScript interfaces (`export interface Props { ... }`) at the top of the frontmatter.
- Minimise client-side `<script>` tags; prefer Astro's static rendering and use `client:*` directives only when necessary.
- Use Astro's `<Image />` component for optimised image delivery; avoid raw `<img>` tags for local assets.
- Leverage Astro content collections for structured data (blog posts, product catalogues) instead of loose JSON or frontmatter duplication.
- Run `astro check` (via `pnpm run build`) before committing to catch type and template errors early.

## Testing Guidelines
- Frontend validation comes from `astro check` during `pnpm run build`; run it before opening a PR.
- Exercise Azure Functions with the REST samples in each `requests.http`; extend them alongside new endpoints.
- No unit test project exists yet; validation relies on builds, `astro check`, and the `requests.http` samples.

## Commit & Pull Request Guidelines
- Use Karma-style commit messages (also known as Conventional Commits): `<type>(<scope>): <subject>` with a lowercase, imperative subject without a trailing period, an optional body of bullet points, and a footer referencing issue/PR numbers. Allowed types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`, `style`, `ci`, `build`, `revert`. Examples: `feat(website-api): add AI spam filter to contact form`, `fix(dashboard): correct event sorting`.
- PR titles — and therefore the PR's merge/squash commit message — must follow the same Karma schema; use the branch's aggregate `<type>(<scope>): <summary>` as the squash-merge title.
- PRs should summarise the change, call out deployment or infrastructure impacts, and attach screenshots or clips for UI tweaks.
- Link Azure Boards or GitHub issues where relevant and confirm the commands above have been executed.

## Environment & Configuration
- Never commit secrets; supply `StorageConnection`, `AZURE_STORAGE_ACCOUNT_NAME`, and `AZURE_STORAGE_ACCOUNT_KEY` via `local.settings.json` or user secrets.
- Website email settings: `EmailSenderAddress`, `ReceiverEmailAddresses` (semicolon-separated), and `EmailConnection`.
- Contact-form spam filter: `OpenAiEndpoint`, `OpenAiApiKey`, and `OpenAiDeployment` (model `gpt-5-nano`) classify submissions before the notification email is sent; the AI call fails open, so on error a message is treated as legit and still emailed. Spam rows are stored with `IsSpam = true` and never emailed. See `features/messages/SpamClassifier.cs` and `infrastructure/modules/openai.bicep`.
- Table usage: `alpakas`, `events`, and `messages` tables with partition keys `AlpakaPartition` (alpakas), `ContactPartition` (messages), and AlpakaId per row for events; storage is provisioned via `infrastructure/modules/storage.bicep` (adopted in place).
- Ensure the storage resources from `infrastructure/modules/storage.bicep` exist (or are substituted) before running the functions locally.
