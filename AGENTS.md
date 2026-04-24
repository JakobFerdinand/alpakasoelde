# Repository Guidelines

## Project Structure & Module Organization
- `src/website`: Astro marketing site with pages in `src/pages`, shared UI in `src/components`, and static assets under `public/`.
- `src/dashboard`: Internal Astro dashboard; place screens in `src/pages` and reusable pieces in `src/components`.
- `src/dashboard-api`: .NET 10 isolated Azure Functions for data ingestion and storage, co-locating triggers with their entity models.
- `src/website-api`: Public-facing Azure Functions that mirror the patterns from `src/dashboard-api`.
- `infrastructure/table-storage.bicep`: Bicep template that provisions the shared Azure Table Storage resources.
- `.slnx` solution: use `alpakasoelde.slnx` to open all projects together; `global.json` pins .NET SDK 10.0.0 with the new test runner.

## Build, Test, and Development Commands
- `cd src/website && npm install && npm run dev` — launches the marketing site with hot reload.
- `cd src/website && npm run build` — runs `astro check` and builds to `dist/`.
- `cd src/dashboard && npm install && npm run dev` — starts the internal dashboard; run `npm run build` before shipping changes.
- `dotnet build src/dashboard-api/dashboard-api.csproj` then `cd src/dashboard-api && func start` — compile and serve the dashboard API locally (Azure Functions Core Tools required).
- `dotnet build src/website-api/website-api.csproj` then `cd src/website-api && func start` — same workflow for the public API facade.
- Tests: `dotnet test src/dashboard-api.Tests/dashboard-api.Tests.csproj` and `dotnet test src/website-api.Tests/website-api.Tests.csproj`; extend the slice-specific test suites when adding handlers or stores.

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
- Run `astro check` (via `npm run build`) before committing to catch type and template errors early.

## Testing Guidelines
- Frontend validation comes from `astro check` during `npm run build`; run it before opening a PR.
- Exercise Azure Functions with the REST samples in each `requests.http`; extend them alongside new endpoints.
- No coverage target exists yet, but add unit tests when introducing new services or parsers.

## Commit & Pull Request Guidelines
- Follow the repo pattern: short, imperative commit subjects (e.g., “Add robots.txt configuration”) and append issue or PR numbers in parentheses when available.
- PRs should summarise the change, call out deployment or infrastructure impacts, and attach screenshots or clips for UI tweaks.
- Link Azure Boards or GitHub issues where relevant and confirm the commands above have been executed.

## Environment & Configuration
- Never commit secrets; supply `StorageConnection`, `AZURE_STORAGE_ACCOUNT_NAME`, and `AZURE_STORAGE_ACCOUNT_KEY` via `local.settings.json` or user secrets.
- Website email settings: `EmailSenderAddress`, `ReceiverEmailAddresses` (semicolon-separated), and `EmailConnection`.
- Table usage: `alpakas`, `events`, and `messages` tables with partition keys `AlpakaPartition` (alpakas), `ContactPartition` (messages), and AlpakaId per row for events; ensure the storage account from `infrastructure/table-storage.bicep` exists or is mocked locally.
- Ensure the storage resources from `infrastructure/table-storage.bicep` exist (or are substituted) before running the functions locally.
