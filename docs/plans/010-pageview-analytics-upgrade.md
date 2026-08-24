# Pageview Analytics Upgrade Plan

## Goal

Upgrade the first-party pageview pipeline (plan `006`) with session tracking, a persistent visitor ID, and reload/back-forward marking, while gating collection to production hostnames and normalizing paths. Every new field is optional end-to-end so each deploy step is independently safe; Table Storage is schemaless, existing rows simply read back null — no migration. Because the new IDs contradict the current Datenschutzerklärung, updating that page is **mandatory** and part of this plan.

## Current state

- **Client:** inline `sendBeacon` on `load` in `src/website/src/layouts/Layout.astro:92-103`, payload `{ path, referrerHost, viewportWidth }`.
- **Frontend shape:** classic Astro MPA — no view transitions / client router anywhere in `src/website`. Every navigation is a full page load, so exactly one beacon fires per navigation; reloads and back/forward navigations already fire `load` and are already counted today (unmarked). No SPA dedupe reasoning applies.
- **Write side:** `src/website-api/features/pageviews/PageView.cs` — anonymous `POST /api/pageview` (:25-28), `Payload(Path?, ReferrerHost?, ViewportWidth?)` (:72), manual `System.Text.Json` validation (:149-181: path must start with `/`, ≤200 chars; referrer host ≤200 chars; viewport width range-checked), entity built at :174-181 into daily partitions `Pv|{yyyy-MM-dd}` with 36-month lazy purge (:101-136).
- **Entities:** `src/website-api/shared/entities/PageViewEntity.cs:6-16` and its dashboard twin `src/dashboard-api/shared/entities/PageViewEntity.cs:6-16` (`Path`, `ReferrerHost?`, `ViewportWidth`).
- **Read side:** `src/dashboard-api/features/pageviews/GetPageViewStats.cs` — `GET /api/pageviews/stats?days&granularity&groupBy` (:23-48), loads ≤180 days of entities into memory (:71-87), aggregates `Total`/`UniquePaths`/`TopPaths`/`Devices`/`Origins`/`Series` (:98-222). `IsInternalReferrer()` (:224-230) already filters internal + preview referrers — but only for origin attribution; preview traffic otherwise pollutes all metrics.
- **Dashboard UI:** `src/dashboard/src/pages/pageviews.astro` wraps `PageViewStats.svelte` (`client:only="svelte"`); Svelte component fetches the endpoint three times (one per `groupBy`, `PageViewStats.svelte:112-119`) and renders toolbar, `kpi-row` tiles (:253-272), charts, tables.
- **Privacy:** `src/website/src/pages/datenschutzerklaerung.astro` §5 (:58-69) states "keine IP-Adressen, keine Cookies und keine Browser-Identifikatoren … eine Identifizierung einzelner Besucher ist nicht möglich" (:66); §7 (:78-79) says "Keine Cookies oder Tracking". Both need correcting once browser-storage IDs are introduced.
- **Production hosts:** `alpakasoelde.at` + `www.alpakasoelde.at` (`infrastructure/main.bicepparam:35-37`). Preview/local hosts are everything else (`localhost`, `*.azurestaticapps.net`).
- **Tests:** `AGENTS.md` lists `dotnet test` suites for `dashboard-api.Tests`/`website-api.Tests`, but those projects do not exist in the working tree (documented in `006` → Known limitations). Verification = builds, `astro check`, `requests.http`.

## Decisions

- **IDs without cookies:** `sessionId` = random UUID held in `sessionStorage` key `lt-session` (new UUID per browser session/tab lifetime); `visitorId` = random UUID in `localStorage` key `lt-visitor` (stable across visits). Both generated with `crypto.randomUUID()`. Storage access wrapped in try/catch — blocked storage (strict privacy modes) simply omits the field.
- **Reloads are marked, never dropped:** `navigationType` comes from the Navigation Timing API (`performance.getEntriesByType('navigation')[0].type`), restricted to `navigate | reload | back_forward`; anything else (`prerender`, missing entry) omits the field. Rows keep counting toward totals regardless of type.
- **Production gating is a client-side allowlist:** the snippet only beacons on the two production hostnames. Allowlisting beats blacklisting: `localhost` and every `*.azurestaticapps.net` preview deployment are excluded by construction, including future ones. `IsInternalReferrer()` stays on the read side as defense-in-depth.
- **Path normalization on both write and read:** strip trailing slashes (except root `/`) in the write handler so future rows are clean, and normalize group keys during stats aggregation so historical `/foo/` vs `/foo` rows merge in `UniquePaths`/`TopPaths`/series until they age out.
- **Lenient validation for the new optional fields:** length-capped free strings (≤64 chars); an unknown `navigationType` value nulls the field instead of returning `400` — deliberate deviation from the strict style of the existing fields (:163-167) so a future marking glitch can never discard the whole pageview row. Unknown JSON properties are ignored by `System.Text.Json`, which also guarantees old-client/new-server compatibility.
- **Full-stack surfacing:** the dashboard gains `Sitzungen` (distinct `SessionId`), `Besucher` (distinct `VisitorId`), and a navigation-type breakdown as KPI material; series/grouping logic is untouched.

## Milestones (tracked)

- [ ] Write the plan (`docs/plans/010-pageview-analytics-upgrade.md`)
- [ ] `website-api`: extend `PageView` slice (payload/command/validation/entity/path normalization)
- [ ] `dashboard-api`: extend `PageViewEntity`, aggregate sessions/visitors/navigation types
- [ ] `dashboard`: new KPI tiles in `PageViewStats.svelte`
- [ ] Update Datenschutzerklärung §5/§7 (**must ship before the client step reaches production**)
- [ ] `website`: rewrite beacon snippet (host allowlist, IDs, `navigationType`)
- [ ] Verify builds + `requests.http` matrix; deploy steps 1→5 in order

## 1. Write API (`src/website-api`)

`src/website-api/features/pageviews/PageView.cs`:

- `Payload` (:72) and `Command` (:74) gain `string? SessionId`, `string? VisitorId`, `string? NavigationType` (all nullable).
- `Handler` (:145-187):
  - New constants beside :149-151: `IdMaxLength = 64`, `AllowedNavigationTypes = ["navigate", "reload", "back_forward"]`.
  - Normalize the path right after the existing checks: `path.Length > 1 ? path.TrimEnd('/') : path` (root `/` untouched), applied before the entity is built (:178).
  - New-field handling after the strict checks: trim/cap `SessionId`/`VisitorId` at 64 chars (over-long → null); map `NavigationType` to itself when whitelisted, otherwise null. Never produce a `ValidationProblem` for these.
  - Entity build (:174-181) stores the three values; empty/null stay `null`.
- `src/website-api/shared/entities/PageViewEntity.cs`: add `public string? SessionId { get; set; }`, `VisitorId`, `NavigationType`.
- DI in `Program.cs:18-19` is unchanged (same handler/store signatures). Extend `src/website-api/requests.http:28-46` with samples that include/exclude the new fields and use a trailing-slash path.

## 2. Read API (`src/dashboard-api`)

`src/dashboard-api/features/pageviews/GetPageViewStats.cs`:

- Mirror the entity additions in `src/dashboard-api/shared/entities/PageViewEntity.cs` (nullable → old rows deserialize as null automatically).
- `Result` (:52) gains `int Sessions`, `int Visitors`, `IReadOnlyList<NavigationCount> Navigations` with a new `record NavigationCount(string Type, int Count)` covering `navigate`/`reload`/`back_forward` (only types actually present in the window).
- In `HandleAsync` (:98-222):
  - Add a `NormalizePath(string)` helper; use it for `UniquePaths` (:110), `TopPaths` grouping (:112-118), `chartPaths` (:139), and the series group lookup (:166) so historical variants merge.
  - Compute `Sessions`/`Visitors` as distinct non-null `SessionId`/`VisitorId` over `inWindow` (:105-107); rows predating the upgrade contribute nothing rather than skewing counts.
  - Build `Navigations` by grouping non-null `NavigationType` values over `inWindow`.
- `TablePageViewReadStore` (:67-87) needs no query changes — the partition filter is unchanged.
- Register nothing new (`Program.cs:29,45` unchanged); refresh the `GET /api/pageviews/stats` sample block in `src/dashboard-api/requests.http:18-64`.

## 3. Dashboard UI (`src/dashboard`)

`src/dashboard/src/components/PageViewStats.svelte`:

- Extend the `StatsResult` type (:15-24) with `Sessions: number`, `Visitors: number`, `Navigations: { Type: string; Count: number }[]`.
- Add two KPI tiles to the `kpi-row` (:253-272): „Sitzungen“ (`Sessions`, e.g. `Users` icon) and „Besucher“ (`Visitors`, e.g. `Repeat` icon — icons come from the installed `@lucide/svelte` package already imported at :4). Optionally annotate the Gesamt tile's subtitle with the reload/back-forward share derived from `Navigations`.
- No changes to fetching (:112-119), toolbar, charts, tables, or zoom sync — the response change is additive. `pageviews.astro` stays as-is.

## 4. Privacy page (`src/website/src/pages/datenschutzerklaerung.astro`) — mandatory

- Bump „Letztes Update“ (:9).
- Rework §5 (:58-69):
  - Bullet list (:60-65): add the session/visitor random IDs (Browser-Speicher `sessionStorage`/`localStorage`, rein zufällige UUIDs, kein Cookie), and the navigation type (Art des Seitenaufrufs: normal / neu geladen / Zurück-Navigation).
  - Replace the identifier claim at :66 — no longer „keine Browser-Identifikatoren“/„Identifizierung nicht möglich“, but accurate pseudonymity: zufällige, gerätegebundene Kennungen ohne Bezug zur Person, keine IP-Adressen, keine Cookies, keine Drittanbieter, kein Cross-Site-/Werbe-Tracking.
  - Re-check the Art. 17/21 paragraph (:69): records are now linkable across visits via a random ID, so soften „können … nicht ausgeübt werden“ accordingly (still pseudonymous; IDs cannot be resolved to a person by us).
- Rework §7 (:78-79): „keine Cookies“ remains true (Web Storage ist kein Cookie), but the „Tracking“ sentence must disclose the first-party pseudonymous IDs described in §5.

## 5. Rollout order

Each step deploys safely on its own; order matters only for data completeness:

1. `website-api` write side (accepts new fields, normalizes incoming paths) — old beacons unaffected, extra JSON properties ignored anyway.
2. `dashboard-api` read side (entity + aggregation) — additive response fields, null-safe against old rows.
3. `dashboard` KPI tiles.
4. Datenschutzerklärung — must be live **before** step 5 collects anything in production.
5. `website` snippet (allowlist + IDs + `navigationType`) — flips on collection; also silences localhost/preview traffic immediately.

## Verification

- `cd src/website && pnpm run build` (runs `astro check`)
- `cd src/dashboard && pnpm run build`
- `dotnet build src/website-api/website-api.csproj && dotnet build src/dashboard-api/dashboard-api.csproj`
- Manual matrix via `dotnet run` + the extended `requests.http` files:
  - Old-shape POST (three fields only) → `204`, row has null `SessionId`/`VisitorId`/`NavigationType`.
  - New-shape POST → row carries IDs and type; over-long ID or bogus type → row stored with those fields nulled.
  - `path: "/alpaka-wanderungen/"` and `"/alpaka-wanderungen"` → single merged `TopPaths` entry in `GET /api/pageviews/stats`.
  - Stats response exposes plausible `Sessions`/`Visitors`/`Navigations`; dashboard renders the new tiles.
- Beacon checks in the browser Network tab: production hostnames send all fields; `http://localhost:4321` and a `*.azurestaticapps.net` preview send nothing; hard reload marks `navigationType: "reload"`; back/forward marks `back_forward`; first-ever visit creates both storage keys, subsequent pages reuse them, new tab/session rotates `lt-session`.
- `git status` / `git diff` review before opening the PR; squash-merge title following Karma schema: `feat(analytics): add session tracking, visitor ids, reload marking, and prod gating to pageviews`.

## Known limitations / notes

- `visitorId` is best-effort: clearing browser data, storage-blocked private modes, or cross-browser usage split visitors — treat `Sessions`/`Visitors` as lower bounds.
- Rows written before this upgrade have null IDs/type; aggregates ignore them for the new metrics but keep counting them in totals, exactly the "optional end-to-end" contract.
- Preview deployments currently pollute totals/devices/paths (only origins are filtered via `IsInternalReferrer`, `GetPageViewStats.cs:224-230`); the client-side allowlist fixes the source going forward, historical rows remain.
- Unrelated observation (out of scope): `src/website/staticwebapp.config.json:3-11` rewrites unmatched routes to `/index.html` with status 200, so requests to nonexistent paths still beacon a path and appear in stats; revisit separately if 404 hygiene matters.
