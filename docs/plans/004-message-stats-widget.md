# Message Stats Widget Plan

## Goal

Replace the single-line `Stats` card on the dashboard root (`src/dashboard/src/pages/index.astro`) with a proper "Nachrichten" widget: KPI tiles (total / spam / legit), a weekly stacked-bar chart of incoming messages split by `IsSpam`, a time-range toggle, and the existing "older than 6 months" count kept as an alert chip. Follows up on `docs/ideas/message-insights-dashboard.md` and extends the inbox rework from `docs/plans/003-message-inbox-ux.md`.

## Decisions

- **Chart approach:** Layerchart vertical stacked `<Bars>` chart (https://www.layerchart.com/docs/components/Bars/vertical-stacked) rendered from a Svelte component. This introduces `svelte` + `@astrojs/svelte` to the dashboard as the first framework component. Layerchart is a Svelte-native library and uses the repo's CSS variables directly via color scales; the added dependency only ships to the messages widget as client-side JS. The previous hand-rolled CSS/flex bars were removed because percentage heights failed to render reliably; Layerchart–Svelte replaces them wholesale.
- **Data source:** new `GET /api/messages/stats` endpoint (new vertical slice in dashboard-api), so message bodies never need to cross the wire to the dashboard root. Reuses the existing `GetMessages.IReadStore` so no new storage wiring.
- **Period presets:** `4 Wochen` / `3 Monate` / `6 Monate` with weekly buckets. `6 Monate` aligns with the existing "older than 6 months" threshold.
- **Old-messages line:** kept as an alert chip inside the widget linking to `/messages`, so the cleanup nudge is not lost.
- **Layout:** the widget replaces the current `Stats` section slot on `index.astro`; no page-wide grid rework.

## Milestones (tracked)

- [x] Create git branch `feat/message-stats-widget`
- [x] Write the plan (`docs/plans/004-message-stats-widget.md`)
- [x] Backend: add `GetMessageStats` slice (`features/messages/GetMessageStats.cs`), register handler, extend `requests.http`
- [x] Frontend (attempt 1): hand-rolled stacked bars in `MessageStats.astro` — removed because bars did not render reliably
- [ ] Frontend (attempt 2): Svelte chart component with Layerchart vertical stacked `<Bars>`
- [ ] Verify: `dotnet build` (dashboard-api), `pnpm run build` + `astro check` in `src/dashboard`, manual widget check
- [ ] Deploy on `main` merge

## 1. Backend (`src/dashboard-api`)

New slice `src/dashboard-api/features/messages/GetMessageStats.cs`, following the vertical-slice layout of `GetOldMessageCount.cs` (function entry, records, handler, store interface).

- Endpoint: `Function("get-message-stats")`, `HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages/stats")`, optional `days` query param (default `28`).
- Records:
  - `Query(int Days)`
  - `Result(int Total, int Spam, int Legit, int OldCount, IReadOnlyList<PeriodBucket> Series)`
  - `PeriodBucket(string Period, int Spam, int Legit)` — `Period` is an ISO week start date (`yyyy-MM-dd`).
- Handler reuses `GetMessages.IReadStore`:
  - Load all messages, bucket by `Timestamp` within the requested window (weekly buckets, anchored to start-of-week), counting `IsSpam` per bucket.
  - Fill zero-count weeks so the axis is continuous.
  - `Total`/`Spam`/`Legit` count the whole window; `OldCount` = messages older than `30 * 6` days (same constant as `GetOldMessageCount`; no refactor of the existing slice needed).
- Register `services.AddScoped<GetMessageStats.Handler>()` in `Program.cs` next to the other message handlers (store already registered at `GetMessages.IReadStore`).
- Extend `src/dashboard-api/requests.http` with a `GET {{base}}/api/messages/stats` sample (default and `?days=90`).

## 2. Frontend (`src/dashboard/src`)

Add the Astro Svelte integration and a single Svelte component `components/MessageStatsChart.svelte` that owns the widget (data fetch, KPI tiles, toggle, chart); `components/MessageStats.astro` becomes a thin wrapper mounting it with `client:only="svelte"`.

- **Dependencies:** `svelte` (+ `lucide-svelte`), `layerchart`, and `@astrojs/svelte` in `astro.config.mjs`. The chart only ships as client JS to the messages widget.
- **Data:** fetch `/api/messages/stats?days=28|90|180` client-side on mount and on toggle change.
- **Chart:** `Chart` + `Layer` + left/bottom `Axis` + `<Bars>` vertical stacked, fed by `groupStackData(series, { xKey: 'Period', stackBy: 'kind' })` where each row becomes two stacked rows (`kind: 'legit'`/`'spam'`). Colors come from CSS variables: legit `var(--weidegruen)`, spam `var(--backstein)` via the `cRange` prop. `tooltipContext={{ mode: 'band' }}` + `Tooltip.Root` show per-week counts. pattern from the vertical-stacked example.
- **Section & card:** `<section class="dashboard-messages section">` with header "Nachrichten" + "Alle anzeigen" link to `/messages`, reusing the `.card`/`.container` patterns and design tokens.
- **KPI row:** three tiles — **Gesamt**, **Spam** (`IsSpam` count + share %, lucide `ShieldAlert`, `--backstein`), **Legit** (`--weidegruen`). Tiles link to `/messages`.
- **Alert chip:** "X Nachrichten älter als 6 Monate" linking to `/messages`, only rendered when `OldCount > 0`.
- **Legend** for the two series, WCAG AA contrast against the card background.
- **Accessibility:** keep the visually-hidden per-week table (week, legit, spam) for screen readers.
- **Period toggle:** segmented `4 Wochen / 3 Monate / 6 Monate` (same pattern as the filter buttons in `messages.astro`), re-fetches `/api/messages/stats?days=28|90|180`.
- **States:** loading, empty (no messages at all), and a visible error row if the fetch fails, each owned by the Svelte component.

## 3. Verification

- `dotnet build src/dashboard-api/dashboard-api.csproj`
- `cd src/dashboard && pnpm run build` (runs `astro check`)
- Manual: hit `GET /api/messages/stats` via `requests.http`, confirm shape; in the browser check KPI update on range toggle, Layerchart stacked-bar rendering, tooltip, legend, alert chip, and loading/error states.
- `git status`/`git diff` review before opening the PR.

## Known limitations / notes

- Messages stored before the spam filter shipped deserialise with `IsSpam = false` and count toward "legit"; the existing age marker behaviour is unchanged.
- The bucket window is inclusive of the current week; the "old messages" alert uses the same `30 * 6` day threshold as the inbox markers.
- `dashboard-api.Tests`/`website-api.Tests` are referenced in `alpakasoelde.slnx` and `AGENTS.md` but not present in the working tree; verification relies on builds, `astro check`, and the `requests.http` samples.
- Layerchart is the first framework component in the dashboard (AGENTS.md previously mandated Astro-only); this is an explicit decision to get a maintainable chart. The Svelte bundle only loads on the dashboard root for the widget.
- Layerchart's default CSS variables can be themed via the repo tokens so the chart matches the design system; keep WCAG AA contrast for the legend and tooltip.