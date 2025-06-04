# Copilot Instructions for alpakasoelde

## 1. Project Overview

-   **Project Name**: alpakasoelde
-   **Description**: A lightweight homepage built with [Astro](https://astro.build) v5.8, styled with vanilla CSS, deployed to Azure Static Web Apps.
-   **Back-end**: Uses Azure Functions targeting .NET 9 (dotnet-isolated runtime) for any serverless endpoints or APIs.
-   **Hosting**: Azure Static Web Apps for the front end; built-in Functions for back end.
-   **Structure**:
    -   Front-end source: `/src` (pages in `/src/pages`, components in `/src/components`)
    -   Global styles: `/src/styles/global.css`
    -   Back-end Azure Functions: `/Api` (C# project, e.g., `SendMessageFunction.cs`)
    -   Astro configuration: `astro.config.mjs` (defines site URL: `https://alpakasoelde.at`)
    -   TypeScript configuration: `tsconfig.json` (extends `astro/tsconfigs/strict`)
    -   Azure Static Web App runtime configuration: `staticwebapp.config.json` (e.g., API runtime)

## 2. Technologies & Dependencies

-   **Front-end**: Astro `^5.8.0` (see `package.json`), vanilla CSS.
-   **Back-end**: Azure Functions with .NET 9 (C#), using the dotnet-isolated worker model.
    -   Key C# packages (see `Api/Api.csproj`): `Microsoft.Azure.Functions.Worker.Sdk` (e.g., version `1.17.0`), `Microsoft.Azure.Functions.Worker.Extensions.Http`, `Azure.Data.Tables`.
-   **Deployment**: Azure Static Web Apps, via GitHub Actions workflow defined in `.github/workflows/ci.yml`. Another workflow `.github/workflows/deploy-storage.yml` exists for deploying Azure Table Storage via Bicep.
-   **TypeScript**: Used for Astro project configuration and type checking, as indicated by `tsconfig.json`.

## 3. C# Coding Conventions & Best Practices

-   Always use target-typed `new` expressions (e.g., `new ClassName()` can be `new()`).
-   Apply pattern matching for input validation and control flow (e.g., `is`, `switch` expressions).
-   Embrace functional programming patterns:
    -   Favor immutable `record` types for data models or DTOs (e.g., `public sealed record MessageEntity(...)`).
    -   Use `readonly` fields and `init`-only properties wherever possible.
    -   Employ primary constructors in `record` types to unify property initialization.
-   Prefer expression-bodied members for methods, properties, and constructors when they simplify code.
-   Leverage LINQ for any `IEnumerable` or `IAsyncEnumerable` data transformations.
-   Keep Azure Functions handlers concise—use minimal boilerplate and focus on pure-function logic.
-   Dependency injection is configured via `ConfigureFunctionsWorkerDefaults()` in `Api/Program.cs`. Follow existing patterns for injecting services (e.g., `ILoggerFactory` in `SendMessageFunction`).
-   When new classes or functions are added, ensure they follow the project’s existing `namespace` structure (e.g., `namespace Api;`) and folder conventions.

## 4. Astro & Vanilla CSS Best Practices

-   For Astro components (e.g., in `src/components` or `src/pages`), maintain file naming consistent with Astro’s file conventions (`.astro`). Global CSS is centralized in `src/styles/global.css`.
-   Use scoped CSS classes within Astro components to keep styles isolated. For global/vanilla CSS, prefer utility-style naming (e.g., a simple hyphenated convention like `.contact-content`). Avoid deeply nested selectors.
-   Design tokens (colors) are defined as CSS variables in `:root` within `src/styles/global.css` (e.g., `--warm-honey`, `--vanilla-cream`). Reference these uniformly across component styles.
-   Encourage small, reusable CSS files or co-locate styles within Astro components. Astro’s built-in CSS bundling will handle optimization.
-   Optimize for performance by minimizing unused CSS and keeping specificity low.

## 5. Testing & Validation

-   No dedicated test frameworks (e.g., Vitest, Playwright for front-end, or xUnit/MSTest for C#) are apparent in the current project structure or CI pipeline.
-   The CI pipeline in `.github/workflows/ci.yml` does not include explicit `npm run test` or `dotnet test` steps. If adding tests, these steps would need to be added to the CI workflow.

## 6. CI/CD & Deployment Guidelines

-   The primary GitHub Actions workflow for CI/CD is `.github/workflows/ci.yml`.
    -   Astro build command: `npm run build` (defined in `package.json`, output to `dist/`).
    -   Azure Functions build command: `dotnet publish Api/Api.csproj -c Release -o api_output`.
-   Azure Static Web Apps deployment is managed by the `Azure/static-web-apps-deploy@v1` action in the CI workflow. This action uploads the Astro build output (from `dist/`) and the Functions API output (from `api_output/`).
-   The `staticwebapp.config.json` file contains runtime platform configuration for Azure Static Web Apps, such as the API runtime version (`dotnet-isolated:9.0`).
-   When modifying deployment configuration, it should never remove existing environment variables (e.g., function app keys, secrets in GitHub Actions) unless explicitly requested.

## 7. Restrictions & Do-Not-Modify Rules

-   Do not manually edit any generated Azure Functions files (e.g., `function.json` automatically created by the Functions SDK). Instead, use attribute annotations (e.g., `[Function("...")]`, `[HttpTrigger(...)]`) or update C# function classes.
-   Do not alter `astro.config.mjs` imports (currently `import { defineConfig } from 'astro/config';`) unless updating Astro versions or adding integrations—let the existing bundler settings stand.
-   Preserve any existing custom GitHub Actions or workflow files (e.g., `.github/workflows/ci.yml`, `.github/workflows/deploy-storage.yml`); if changes are needed, append or update steps rather than replacing entire jobs.
-   Do not hardcode secrets (e.g., connection strings, API keys) in code—always reference environment variables (e.g., via `Environment.GetEnvironmentVariable` in C# or `import.meta.env` in Astro if configured, or through Azure App Settings for deployed functions).

## 8. Let Copilot Inspect for Details

-   Scan the directory structure (e.g., `src/pages`, `src/components`, `src/styles`, `Api/`), existing scripts (in `package.json`), and configuration files (`astro.config.mjs`, `tsconfig.json`, `Api/Api.csproj`, `staticwebapp.config.json`, `.github/workflows/*.yml`) to gather missing details or confirm existing patterns.
-   Note current version numbers: Astro `^5.8.0`, .NET SDK for Functions `9.0.x` (target framework `net9.0`).
-   Reference custom NPM scripts from `package.json` (e.g., `npm run build`, `npm run dev`) when describing build or development steps.
