# Repository Guidelines

## Project Structure & Module Organization
- `src/website`: Astro marketing site with pages in `src/pages`, shared UI in `src/components`, and static assets under `public/`.
- `src/dashboard`: Internal Astro dashboard; place screens in `src/pages` and reusable pieces in `src/components`.
- `src/dashboard-api`: .NET 9 isolated Azure Functions for data ingestion and storage, co-locating triggers with their entity models.
- `src/website-api`: Public-facing Azure Functions that mirror the patterns from `src/dashboard-api`.
- `infrastructure/table-storage.bicep`: Bicep template that provisions the shared Azure Table Storage resources.

## Build, Test, and Development Commands
- `cd src/website && npm install && npm run dev` — launches the marketing site with hot reload.
- `cd src/website && npm run build` — runs `astro check` and builds to `dist/`.
- `cd src/dashboard && npm install && npm run dev` — starts the internal dashboard; run `npm run build` before shipping changes.
- `dotnet build src/dashboard-api/dashboard-api.csproj` then `cd src/dashboard-api && func start` — compile and serve the dashboard API locally (Azure Functions Core Tools required).
- `dotnet build src/website-api/website-api.csproj` then `cd src/website-api && func start` — same workflow for the public API facade.

## Coding Style & Naming Conventions
- Use two-space indentation in Astro/TS files, PascalCase component filenames, and keep copy in dedicated `.astro` or `.md` fragments.
- Co-locate styles with the component and rely on the shared CSS variables exposed by the layout.
- In C#, keep one public type per file, use PascalCase for types, camelCase for locals, and `const` for shared environment keys.

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
- Ensure the storage resources from `infrastructure/table-storage.bicep` exist (or are substituted) before running the functions locally.
