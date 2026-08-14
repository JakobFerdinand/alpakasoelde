# Cookie-free Website Analytics Plan

## Goal

Gain insight into usage of `src/website` (how many page views, which pages are viewed, where visitors come from) without cookies, without a cookie banner, and without third-party analytics. Chosen approach: a first-party `POST /api/pageview` endpoint in `website-api` fed by a tiny `navigator.sendBeacon` snippet, stored in Azure Table Storage, viewed via a new dashboard page. No personal data is collected: no IP addresses, no cookies, no browser fingerprints, no user IDs.

## Decisions

- **No cookies, no banner:** the site stays cookieless by design (current §6 of the Datenschutzerklärung). The analytics track only pseudonymous, non-identifying data, so no consent banner is required; legal basis is Art. 6 Abs. 1 lit. f DSGVO (legitimate interest in improving the website).
- **First-party over SaaS:** alternatives considered — self-hosted Umami/Plausible/GoatCounter (extra infrastructure to run), hosted SaaS (third-party DPAs, recurring cost), Application Insights client SDK (poor analytics UX). The custom endpoint reuses the existing Function App, storage account, and SWA→API routing, so nothing new to operate and data stays in the existing Microsoft AVV.
- **Data model:** one Table Storage entity per page view. Partition key `Pv|{yyyy-MM-dd}` (daily partitions for cheap range scans), RowKey GUID. Fields: `Path`, `ReferrerHost` (origin only, never the full referrer URL, to avoid leaking query-string PII), `ViewportWidth`, `Timestamp`. No IP, no user agent, no identifiers.
- **Transport:** `navigator.sendBeacon` on `load` — fire-and-forget, does not delay page load, survives page unload. Same-origin `/api/pageview` routes through the existing SWA API integration (same as the contact form).
- **Retention:** rows are deleted after 12 months (automated cleanup is follow-up work; manual partition deletion via Storage Explorer until then).
- **Dashboard:** new `GET /api/pageviews/stats?days=` mirroring the `GetMessageStats` slice, rendered in a new Svelte page with KPI tiles, per-page table, and a weekly bar chart.

## Milestones (tracked)

- [x] Create git branch `feat/website-analytics`
- [x] Write the plan (`docs/plans/006-website-analytics.md`)
- [x] Backend: add `PageViewEntity` + `PageView` slice in `website-api`, register handler, extend `requests.http`
- [x] Client: add `sendBeacon` snippet to `src/website/src/layouts/Layout.astro`
- [x] Privacy: update `src/website/src/pages/datenschutzerklaerung.astro` (new §5, rework old §6, renumber)
- [x] Dashboard: add `GetPageViewStats` slice in `dashboard-api`, register handler, extend `requests.http`
- [x] Dashboard: add `PageViewStats.svelte`/wrapper/page + nav link
- [ ] Verify: `dotnet build` (both APIs), `pnpm run build` + `astro check` (website and dashboard), manual check via `requests.http`
- [ ] Deploy on `main` merge

## 1. Backend (`src/website-api`)

New entity `src/website-api/shared/entities/PageViewEntity.cs`, mirroring `MessageEntity.cs`:

- `ITableEntity` with `Path` (string), `ReferrerHost` (string?), `ViewportWidth` (int), `Timestamp` (`DateTimeOffset?`, storage-set), `PartitionKey = "Pv|{yyyy-MM-dd}"`, `RowKey = Guid.NewGuid().ToString()`.

New slice `src/website-api/features/pageviews/PageView.cs`, following the vertical-slice layout of `SendMessage.cs` (function entry, command, store interface, handler):

- Endpoint: `Function("pageview")`, `HttpTrigger(AuthorizationLevel.Anonymous, "post")`, route `/api/pageview`.
- Body: JSON (`application/json`, sendBeacon Blob) with `path`, `referrerHost`, `viewportWidth`. Parse with `System.Text.Json`; validation: `path` must start with `/`, path and referrer host capped at 200 chars, else `400`.
- Handler builds the entity (client-provided values only — the server adds nothing about the requester, notably no IP) and writes it through `IPageViewWriteStore → TablePageViewStore` (`GetTableClient("pageviews")` + `CreateIfNotExistsAsync`, matching `Events.cs`); table is auto-created, no Bicep changes.
- Response: `204 No Content` (sendBeacon never reads it, but keeps it minimal).
- Register `PageView.Handler` and `PageView.IPageViewWriteStore → PageView.TablePageViewStore` in `Program.cs` next to `SendMessage`.
- Extend `src/website-api/requests.http` with a `POST /api/pageview` sample.

## 2. Client (`src/website`)

Add an inline `<script is:inline>` at the end of `src/website/src/layouts/Layout.astro` (beside the navbar script):

- On `load` (guarded by `navigator.sendBeacon` availability), beacon the JSON payload to `/api/pageview`:
  - `path: location.pathname`
  - `referrerHost: document.referrer ? new URL(document.referrer).host : ''` (origin only)
  - `viewportWidth: screen.width`
- No cookies, no localStorage, no IDs; ~10 lines, runs on every page through the shared layout.

## 3. Privacy (`src/website/src/pages/datenschutzerklaerung.astro`)

- Bump "Letztes Update" to August 2026.
- Insert new section "5. Pseudonyme Besuchsstatistik": per page view a cookie-free record is sent (page path, referred-website host only, screen width, time); no IP addresses, cookies, or browser identifiers are stored; no identification possible; stored in Azure Table Storage under the existing Microsoft Auftragsverarbeitungsvertrag; auto-deleted after 12 months; legal basis Art. 6 Abs. 1 lit. f DSGVO; right to object.
- Rework old §6 "Keine Cookies oder Tracking": keep "no cookies", change "keine Website-Analyse-Tools" to clarify analytics are pseudonymous, first-party, and cookie-free — still no banner required.
- Renumber following sections (old §5 "Serverprotokolle & Application Insights" stays as its own §6, tracking stays excluded from Application Insights).

## 4. Dashboard backend (`src/dashboard-api`)

New slice `src/dashboard-api/features/pageviews/GetPageViewStats.cs`, mirroring `GetMessageStats.cs` (function entry, records, handler, store):

- Endpoint: `Function("get-pageview-stats")`, `HttpTrigger(AuthorizationLevel.Function, "get", Route = "pageviews/stats")`, optional `days` query param (default `28`, presets 28/90/180).
- Records: `Query(int Days)`, `Result(int Total, IReadOnlyList<PathCount> TopPaths, IReadOnlyList<PeriodBucket> Series)`, `PathCount(string Path, int Count)`, `PeriodBucket(string Period, int Count)` — `Period` is an ISO week start date (`yyyy-MM-dd`).
- Store: `IPageViewReadStore` querying the `pageviews` table with a partition-key prefix filter covering the requested day range (`Pv|`), materialised in memory (data volume is small).
- Handler: total in window, paths sorted by count descending, weekly buckets with zero-filled weeks for a continuous axis.
- Register handler + store in `Program.cs`; extend `src/dashboard-api/requests.http`.

## 5. Dashboard frontend (`src/dashboard`)

- New page `src/dashboard/src/pages/pageviews.astro` wrapping `PageViewStats` with `client:only="svelte"`, mirroring `messages.astro`.
- New `src/dashboard/src/components/PageViewStats.astro` (thin wrapper) + `PageViewStats.svelte`:
  - Fetch `/api/pageviews/stats?days=28|90|180` on mount and on period toggle (segmented `4 Wochen / 3 Monate / 6 Monate`, same pattern as `MessageStatsChart.svelte`).
  - KPI tiles (Gesamt page views) + per-page table (path, count) + weekly bar chart via Layerchart (existing dashboard dependency), theme tokens from shared CSS variables, sr-only table for accessibility.
  - Loading, empty, and error states. German UI copy.
- Add nav link in `DashboardNavbar.svelte`.

## 6. Verification

- `dotnet build src/website-api/website-api.csproj` and `dotnet build src/dashboard-api/dashboard-api.csproj`
- `python3 -m http.server` style check not needed; instead hit both new endpoints via the extended `requests.http` files (`dotnet run` in each API project).
- `cd src/website && pnpm run build` (runs `astro check`)
- `cd src/dashboard && pnpm run build` (runs `astro check`)
- Manual: beacon fires on page load (Network tab), entity appears in `pageviews` table, stats endpoint aggregates, dashboard page renders chart/table and updates on period toggle.
- `git status`/`git diff` review before opening the PR.

## Known limitations / notes

- `dashboard-api.Tests`/`website-api.Tests` are not present in the working tree (their references were removed from `alpakasoelde.slnx`; `AGENTS.md` still lists test commands for them); verification relies on builds, `astro check`, and the `requests.http` samples.
- Referrer is recorded as host only; visitors arriving with query-string parameters (e.g. UTM) are not attributed to campaigns — intentional to keep data non-identifying.
- The dashboard stats store reads the last 180 days of daily partitions; the `days` query parameter is clamped to 180 (covers all presets 28/90/180).
- Raw rows live in daily partitions; automated 12-month cleanup and the retention claim in the Datenschutzerklärung need a follow-up (timer function or manual partition deletion).
- The dashboard page itself is behind SWA EasyAuth (admin/collaborator), so no visitor data is exposed publicly. The `pageviews` table is not linked to `alpakas`/`events`/`messages` data in any way.
