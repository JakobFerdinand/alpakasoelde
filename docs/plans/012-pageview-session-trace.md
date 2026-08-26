# Pageview Session Trace Plan

## Goal

Add a read-only drill-down to the dashboard so an individual browsing session can be inspected the way a trace is inspected in an OpenTelemetry viewer: pick a session from a list, see every pageview as a span on a timeline, follow the user's navigation step by step (path, entry referrer, navigation type, dwell time between views). Purely additive on the **read** side — the website and its ingestion (`src/website-api`) are untouched, no new data is collected or written, and the Datenschutzerklärung needs no change because no new identifiers are introduced beyond what plan `010` already ships and discloses.

## Current state

- **Data:** every row in the `pageviews` table already carries everything needed for journey reconstruction: `PartitionKey = Pv|{yyyy-MM-dd}`, `RowKey` = random GUID, `Path`, `ReferrerHost?`, `ViewportWidth`, `SessionId?` (UUID in `sessionStorage['lt-session']`, per tab), `VisitorId?` (UUID in `localStorage['lt-visitor']`), `NavigationType?` (`navigate|reload|back_forward`), plus the system-managed `Timestamp` (= server write time, the only ordering signal since RowKey is not time-sortable). Entity: `src/dashboard-api/shared/entities/PageViewEntity.cs`.
- **Read side:** `src/dashboard-api/features/pageviews/GetPageViewStats.cs` — `GET /api/pageviews/stats` (:23-58) already loads up to 180 days of entities fully into memory via `TablePageViewReadStore.GetAllAsync` (:85-101, filter `PartitionKey ge 'Pv|{start}' and le 'Pv|{today}'`) and aggregates in LINQ. It computes distinct `Sessions`/`Visitors` (:153-163) — the aggregation primitive this plan needs — but never exposes individual sessions. Documented stance (plan `006`): data volume is small; full materialization, no continuation-token plumbing anywhere in the repo.
- **Dashboard UI:** `src/pages/pageviews.astro` wraps `PageViewStats.svelte` (`client:only="svelte"`); `StatsResult` type at `PageViewStats.svelte:17`, three parallel fetches of `/api/pageviews/stats` (:151+), toolbar/KPI/charts, `layerchart` for series charts. All fetches use same-origin relative `/api/...` paths (SWA proxies to the linked Functions app behind EasyAuth roles `admin`/`collaborator`).
- **Drill-down precedents:** list → detail via URL param exists in `Alpakas.svelte` (keyboard-accessible row links → `/alpakas?id=…`) and `AlpakaDetail.svelte` (reads `?id=` on mount, loading/error states, back link). `Modal.svelte` exists but no drawer/timeline pattern yet. Dates are formatted with `formatTimestamp` from `src/utils/formatters.ts` (`de-AT`, `Europe/Vienna`). Icons come from `@lucide/svelte`; charts from `layerchart` ^2.2.0.
- **Tests:** none exist (see `AGENTS.md`); verification = `dotnet build` both APIs, `pnpm run build` both Astro apps, `requests.http` matrices.

## Decisions

- **Reuse the existing store, don't duplicate it.** The new handlers inject the already-registered `GetPageViewStats.IPageViewReadStore` (Program.cs registers it at the `GetPageViewStats` line) and get the identical 180-day in-memory snapshot the stats endpoint uses. Only new handler registrations go into `Program.cs`.
- **Two GET endpoints, one vertical-slice file**, following the multi-handler precedent in `features/events/Events.cs`:
  - `GET /api/pageviews/sessions` — session summaries for a window.
  - `GET /api/pageviews/sessions/{sessionId}` — full ordered event trace; 404 when unknown.
  Both `[HttpTrigger(AuthorizationLevel.Function, "get", …)]`, same-origin `/api` routing unchanged, no CORS implications.
- **Grouping happens in memory:** window-filter events by system `Timestamp` (exactly like `HandleAsync` :119-121), drop rows with blank `SessionId` (ungroupable — storage-blocked browsers sent `''`), group the rest by trimmed `SessionId` (Ordinal), sort events inside each session by `Timestamp` then `RowKey` for deterministic order.
- **Dwell time is derived, never stored:** per event, seconds until the next event of the same session; `null` on the last event („offen“). Computed in the handler so the UI stays dumb.
- **Window semantics:** the list endpoint mirrors the stats endpoint's contract — `days` (default 28, clamped to 180) or explicit `from`/`to`. A session whose events straddle midnight/window edges appears with the events inside the window only; counts are therefore window-relative. The **detail** endpoint deliberately ignores `days` and always scans the full 180-day lookback: session IDs are unique, so this is cheap and guarantees a complete trace even for midnight-crossings.
- **Session quality signals stay visible:** the list response reports `UngroupedPageViews` (rows without session id in the window) so nobody mistakes the session list for the full traffic picture. Bounce filtering is a `minPages` parameter (default 1 = show everything), not a hidden assumption.
- **No server-side session stitching:** a session lives exactly as long as its tab (that's what `sessionStorage` means). No 30-minute idle-timeout merging is attempted — it would fabricate journeys that never happened. Cross-session grouping is offered honestly via `VisitorId` filtering instead (a `visitor` query param on the list endpoint, surfaced as a „weitere Sitzungen dieses Besuchers“ link in the UI).
- **Waterfall without new dependencies:** the OTel-style timeline is plain HTML/CSS — one flex/grid row per event, bar offset = `(t − t₀) / duration`, width = `dwell / duration` (percentage-based, minimum visual width). `layerchart` stays reserved for the aggregate series charts; no npm additions.
- **Separate page instead of cramming into `PageViewStats`:** new thin wrapper `src/pages/sitzungen.astro` + island `SessionFlow.svelte`, new navbar entry „Sitzungen“. Matches the one-screen-per-page convention (`messages`, `gutscheine`, `alpakas`) and keeps the heavy stats page untouched. Optional nicety: make the existing „Sitzungen“ KPI tile in `PageViewStats.svelte` link to `/sitzungen`.
- **Read-only guarantee:** no POST/PUT/DELETE triggers added, `website-api` untouched, no schema/entity changes, no infra changes (`main.bicep` untouched — the Functions app and tables already exist).

## Milestones (tracked)

- [ ] Write the plan (`docs/plans/012-pageview-session-trace.md`)
- [ ] `dashboard-api`: session list + detail endpoints in `features/pageviews/GetPageViewSessions.cs`
- [ ] `dashboard`: `SessionFlow.svelte` (list + trace view), `src/pages/sitzungen.astro`, navbar entry
- [ ] Extend `src/dashboard-api/requests.http`
- [ ] Verify builds + manual matrix; deploy dashboard SWA (ships API + frontend together)

## 1. Read API (`src/dashboard-api`)

New file `src/dashboard-api/features/pageviews/GetPageViewSessions.cs`, namespace `DashboardApi.Features.PageViews`, injecting `ILogger<T>` + `GetPageViewStats.IPageViewReadStore`:

```csharp
public sealed record SessionSummary(
    string SessionId, string? VisitorId, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt,
    int PageViews, int DurationSeconds, string EntryPath, string ExitPath,
    string? EntryReferrerHost, string DeviceCategory);

public sealed record SessionEvent(
    DateTimeOffset TimestampUtc, string Path, string? ReferrerHost,
    string? NavigationType, string DeviceCategory, double? DwellSeconds);

public sealed record SessionListResult(
    int WindowDays, IReadOnlyList<SessionSummary> Sessions, bool Truncated, int UngroupedPageViews);

public sealed record SessionDetailResult(
    SessionSummary Summary, IReadOnlyList<SessionEvent> Events);
```

**Function 1 — `get-pageview-sessions`, Route `pageviews/sessions`.** Query params parsed exactly like the stats function (`GetPageViewStats.cs:27-52`): `days` (default 28, clamp 180) xor `from`/`to` (`DateOnly` pair → `windowStart`), plus `minPages` (default 1, clamp 1–100), `limit` (default 50, clamp 1–200), optional `visitor` (trimmed, ≤64 chars) and `path` (exact match against normalized path). Handler:

1. Load all entities, window-filter on `Timestamp >= windowStart` (reuse the :119-121 pattern).
2. Drop blank `SessionId` rows; count them into `UngroupedPageViews`.
3. Group remaining rows by `SessionId.Trim()` (Ordinal). Per session: `StartedAt` = min `Timestamp`, `LastSeenAt` = max, `PageViews` = count, `EntryPath`/`ExitPath` = `NormalizePath` of first/last event (reuse the private helper — either duplicate the tiny static locally or make `GetPageViewStats.NormalizePath` `internal`), `EntryReferrerHost` = first event's `ReferrerHost` (kept raw; internal-referrer suppression is a stats concern, here honesty wins), `DeviceCategory` = category of the first event's `ViewportWidth` (reuse the `<600/<1024/<1920` switch).
4. Apply `minPages`, `visitor` (Ordinal compare on trimmed `VisitorId`) and `path` filters, sort by `LastSeenAt` desc, take `limit`, set `Truncated` accordingly.

**Function 2 — `get-pageview-session-by-id`, Route `pageviews/sessions/{sessionId}`.** Validate the route param: non-empty, ≤64 chars after trim (mirrors `SanitizeIdentifier` on the write side), otherwise 400. Load the full store snapshot, filter `SessionId.Trim() == id` (Ordinal), 404 if empty. Build the same summary as above (from the complete, unwindowed event set), then project events ordered by `Timestamp`, then `RowKey`:

```csharp
events[i].DwellSeconds = i < events.Count - 1
    ? (events[i + 1].Timestamp - events[i].Timestamp).TotalSeconds   // round to 1 decimal
    : null;
```

Both entries write responses with `req.CreateResponse(HttpStatusCode.OK)` + `WriteAsJsonAsync`, matching `GetPageViewStats.Run` (:54-57).

**Program.cs:** add `services.AddScoped<GetPageViewSessions.Handler>();` (single handler class with both function entries, like `Events.GetHandler/AddHandler`) beside the existing registrations — the store registration already exists.

**requests.http:** add blocks for `GET /api/pageviews/sessions?days=28&minPages=2&limit=50`, the `from`/`to` variant, the `visitor=`/`path=` filtered variants, a valid `GET /api/pageviews/sessions/{sessionId}` and a bogus-id case expecting 404.

## 2. Dashboard UI (`src/dashboard`)

**Page shell:** `src/pages/sitzungen.astro` — thin wrapper importing `DashboardLayout` and mounting `<SessionFlow client:only="svelte" />` (copy `pageviews.astro:1-8` verbatim, title „Sitzungen“). Add „Sitzungen“ to `DashboardNavbar.svelte` between „Statistik“ and „Gutscheine“ (desktop links + mobile menu).

**Island:** new `src/components/SessionFlow.svelte`, Svelte 5 runes, scoped styles, global utility classes (`.card`, `.data-table`, `.table-wrapper`, `.loading-text`, status classes). Structure:

- **State:** `sessions`, `detail`, `loading`, `error`, period preset (`$state<'7'|'28'|'90'|'180'>('28')`), `minPages` toggle („Alle“ / „≥ 2 Seiten“), optional `visitorFilter`, `selectedSessionId` initialized from `URLSearchParams(window.location.search).get('id')` (the `AlpakaDetail` pattern) so traces are deep-linkable; selection updates the URL via `history.replaceState`.
- **Fetches:** `loadSessions()` calls `/api/pageviews/sessions?days=…&minPages=…` (+ `&visitor=…` when set) with the `AbortController`-swap idiom from `PageViewStats.svelte:112+`; `openSession(id)` fetches `/api/pageviews/sessions/${encodeURIComponent(id)}`. Errors surface as German `role="alert"` text („Sitzungen konnten nicht geladen werden.“), states rendered inside the table like `MessagesPage.svelte`.
- **List view:** KPI strip (Anzahl Sitzungen, Ø Seiten pro Sitzung, Ø Dauer, Bounce-Anteil = share with 1 page) followed by a `.data-table`: Zuletzt aktiv (`formatTimestamp`), Dauer, Seiten, Einstieg → Ausstieg, Referrer, Gerät, Besucher (first 8 chars of `VisitorId`, full value in `title`). Rows are keyboard-accessible links (`role="link"` + Enter/Space, `tabindex="0"`, the `Alpakas.svelte` pattern) calling `openSession(id)`.
- **Trace view (the OTel part):** header card with summary chips (Seiten, Dauer, Einstieg, Ausstieg, Referrer, Gerät, Besucher — with a „← Zurück zur Liste“ back link and a „Sitzungen dieses Besuchers“ button that sets `visitorFilter` and returns to the list), then the waterfall:
  - Container = CSS grid, one row per event: gutter (index + `formatTimestamp`) | track | dwell label right-aligned.
  - Track: `position:relative`; bar `position:absolute; left:{offset}%; width:{width}%` where `offset = (ts − t₀)/duration·100`, `width = max(dwell/duration·100, 2%)` (last event gets a fixed small marker). Colors by `NavigationType`: `navigate` → `--himmelblau`, `reload` → `--bluetenhonig`, `back_forward` → `--backstein`, missing → neutral gray; a small legend sits above the chart.
  - Gaps > 30 min between consecutive events render a dashed separator + lucide `TimerOff` hint „Lücke 42 min“ (computed client-side from timestamps).
  - Below the waterfall, a plain `.data-table` repeats the events as steps: #, Zeitpunkt, Pfad, Typ (German labels: „Aufruf“/„Neu geladen“/„Vor/Zurück“/„–“), Verweildauer, Referrer, Gerät. New `formatDuration(seconds)` helper in `src/utils/formatters.ts` (`de-AT`: „45 s“, „3 min 20 s“, „> 24 h“ for absurd tab lifetimes).
  - Mobile (<768px): hide the track column, stack gutter + dwell vertically — the step table remains the canonical view (responsive-table precedent from `EventList.svelte`).
- Accessibility: waterfall rows get `aria-label` with the step summary; the section heading uses `aria-live="polite"` result counts like the other pages.

**Optional cross-link:** in `PageViewStats.svelte`, wrap the „Sitzungen“ KPI tile (:338 area) in an `<a href="/sitzungen">` styled invisibly — cheap entry point from the aggregates to the journeys.

## 3. Rollout

Single PR, single deploy target: `build-and-deploy-dashboard.yml` publishes `dashboard-api` + `dashboard` together to the dashboard SWA. No ordering constraints, no feature flags — the endpoints are anonymous-free (`AuthorizationLevel.Function` behind EasyAuth-routed same-origin calls) and the UI simply renders nothing meaningful until real session data exists in the selected window.

## Verification

- `cd src/dashboard-api && dotnet build` and `cd src/website-api && dotnet build` (untouched, must stay green).
- `cd src/dashboard && pnpm run build` (type/template check) — plus `pnpm run check:svelte` if wiring changed.
- Manual matrix with `dotnet run` + extended `requests.http`:
  - List defaults (`days=28`): sessions sorted by `LastSeenAt` desc; `UngroupedPageViews` ≥ 0; bounce filter removes 1-page sessions when `minPages=2`.
  - `from`/`to` window equals the equivalent `days` result.
  - Detail: events strictly ascending, `DwellSeconds` null only on the last event, sums consistent with `DurationSeconds`; unknown id → 404, malformed id → 400.
  - Midnight-crossing session: detail shows events from both day partitions.
- Browser walkthrough against local Functions host: list → click row → waterfall renders, legend colors match types, >30-min gaps flagged, back link restores list state, direct load of `/sitzungen?id=…` opens the trace (deep link), keyboard-only traversal works, contrast AA on all new chips/bars.
- `git status` / `git diff` review before PR; squash title: `feat(dashboard): add session flow explorer with otel-style trace view for pageviews`.

## Known limitations / notes

- **`sessionStorage` semantics bound the fidelity:** a second browser tab splits one human visit into two sessions, and a tab left open for weeks produces one giant session — durations are honest per session ID, not per human visit. The visitor filter mitigates but does not fix this.
- **Ordering rests on server write `Timestamp`**, not client event time. `sendBeacon` is fire-and-forget, so occasional lost beacons show up as inflated dwell times/gaps rather than missing steps.
- Rows with blank ids (blocked storage/private mode, pre-plan-010 rows) are invisible to journeys by construction; they're only counted in `UngroupedPageViews` so the numbers reconcile with the stats page.
- `/terrapreta` uses `WorkshopLayout` and emits no beacons today — sessions silently start/end around it.
- The detail endpoint rescans the full 180-day snapshot per request. Consistent with the existing stats store and fine at current volume; if the table ever grows, swap the store for a `SessionId`-targeted strategy (e.g., a secondary lookup table written at ingest time) — deliberately out of scope here since it touches the write path.
- No pagination by design (window + `limit` + `Truncated` flag follows the repo's full-materialization stance); narrow the window instead of paging.
