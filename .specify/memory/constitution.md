<!--
Sync Impact Report:
- Version change: none → 1.0.0
- Modified principles: Initial establishment
- Added sections: All sections (initial constitution)
- Removed sections: None
- Templates requiring updates: ⚠ No .specify/templates/ directory found (project uses AGENTS.md for guidance)
- Follow-up TODOs: None
-->

# Alpakasoelde Constitution

## Core Principles

### I. Minimal Dependencies
Every application MUST minimize external dependencies to reduce maintenance burden and
security surface area. Dependencies are justified only when they provide substantial value
that outweighs implementation cost. Prefer platform features (Astro built-ins, .NET BCL,
Azure SDK) over third-party libraries.

**Rationale**: Small homepage projects gain agility and longevity through simplicity.
Fewer dependencies mean fewer breaking changes, security patches, and upgrade cycles.

### II. Static-First Architecture
Static web apps (Astro-based) serve as the primary user interface, with server logic
reserved exclusively for data operations requiring persistence or external integration.
All content, styling, and client interactions MUST be resolved at build time whenever
possible.

**Rationale**: Azure Static Web Apps provide cost-effective hosting with CDN distribution.
Pre-rendered content delivers superior performance and SEO compared to dynamic rendering.

### III. Vertical Slice Organization
Azure Functions MUST follow vertical slice architecture: each function file co-locates
command/query models, handler logic, dependencies, and HTTP trigger definition. Shared
entities live in dedicated `shared/entities` folders and are reused across slices.

**Rationale**: Vertical slices improve discoverability and reduce coupling. Developers
can understand an entire feature by reading a single file, and changes remain localized.

### IV. Infrastructure as Code
All Azure resources MUST be defined in Bicep templates under `infrastructure/`. Manual
portal configurations are prohibited. Storage accounts, tables, and application settings
are provisioned via CI/CD workflows.

**Rationale**: Reproducible infrastructure prevents drift between environments and enables
disaster recovery through version-controlled templates.

### V. Type Safety & Modern C#
C# code MUST leverage modern language features: target-typed `new`, pattern matching,
`record` types, primary constructors, expression-bodied members, and LINQ for collections.
Immutability is preferred via `readonly` fields and `init`-only properties.

**Rationale**: Modern C# reduces boilerplate, prevents mutation bugs, and aligns with
functional programming best practices. Type safety catches errors at compile time.

## Technology Stack Requirements

The project MUST adhere to the following technology choices:

- **Frontend**: Astro v5.8+ with vanilla CSS (no CSS frameworks)
- **Backend**: Azure Functions targeting .NET 10 (dotnet-isolated runtime)
- **Storage**: Azure Table Storage for persistence
- **Hosting**: Azure Static Web Apps for frontend, integrated Functions for backend
- **CI/CD**: GitHub Actions for build, test, and deployment automation
- **Styling**: CSS custom properties defined in `:root` for design tokens

Rationale: This stack balances modern capabilities with operational simplicity. Astro
provides excellent performance for content sites, .NET 10 offers robust serverless
capabilities, and Azure services integrate seamlessly for small-scale deployments.

## Development Workflow

All code changes MUST follow this workflow:

1. **Pre-build validation**: Run `astro check` for frontend, `dotnet build` for backend
2. **Local testing**: Exercise functions via `requests.http` files; verify UI changes
3. **Commit standards**: Imperative mood subjects, issue references in parentheses
4. **Pull request**: Summary of changes, deployment impacts, and screenshots for UI work
5. **No secrets**: Use `local.settings.json` (gitignored) or Azure App Settings

Rationale: Consistent workflow reduces review friction and prevents configuration leaks.
The project prioritizes working code over extensive automated testing given its scale.

## Governance

This constitution supersedes all other development practices. Amendments require:

1. Documented rationale for the change
2. Impact assessment on existing code and templates
3. Version increment following semantic versioning (MAJOR.MINOR.PATCH)
4. Update of `LAST_AMENDED_DATE` to amendment date

**Compliance**: All pull requests MUST align with Core Principles. Complexity
beyond the stated architecture requires explicit justification. Runtime development
guidance is maintained in `AGENTS.md`.

**Version**: 1.0.0 | **Ratified**: 2025-11-24 | **Last Amended**: 2025-11-24
