# Message Stats Widget Plan

## Goal

Replace the single-line `Stats` card on the dashboard root (`src/dashboard/src/pages/index.astro`) with a proper "Nachrichten" widget: KPI tiles (total / spam / legit), a weekly stacked-bar chart of incoming messages split by `IsSpam`, a time-range toggle, and the existing "older than 6 months" count kept as an alert chip. Follows up on `docs/ideas/message-insights-dashboard.md` and extends the inbox rework from `docs/plans/003-message-inbox-ux.md`.

## Decisions

- **Chart approach:** hand-rolled stacked bars (plain divs/flexbox + design tokens). No chart library — the dashboard ships no framework components and no client JS beyond its own scripts; adding React/Recharts (e.g. Layerchart) would be a stack change and is out of scope. If richer charts are needed later, the `/stats` endpoint contract stays and a library can be layered in.
- **Data source:** new `GET /api/messages/stats` endpoint (new vertical slice in dashboard-api), so message bodies never need to cross the wire to the dashboard root. Reuses the existing `GetMessages.IReadStore` so no new storage wiring.
- **Period presets:** `4 Wochen` / `3 Monate` / `6 Monate` with weekly buckets. `6 Monate` aligns with the existing "older than 6 months" threshold.
- **Old-messages line:** kept as an alert chip inside the widget linking to `/messages`, so the cleanup nudge is not lost.
- **Layout:** the widget replaces the current `Stats` section slot on `index.astro`; no page-wide grid rework.

## Milestones (tracked)

- [x] Create git branch `feat/message-stats-widget`
- [x] Write the plan (`docs/plans/004-message-stats-widget.md`)
- [ ] Backend: add `GetMessageStats` slice (`features/messages/GetMessageStats.cs`), register handler, extend `requests.http`
- [ ] Frontend: add `MessageStats.astro`, wire it into `index.astro`, remove `Stats.astro`
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

New component `components/MessageStats.astro`; update `pages/index.astro` to use it; delete `components/Stats.astro`.

- **Section & card:** `<section class="dashboard-messages section">` with header "Nachrichten" + "Alle anzeigen" link to `/messages`, reusing the `.card`/`.container` patterns and design tokens.
- **KPI row:** three tiles — **Gesamt**, **Spam** (`IsSpam` count + share %, lucide `ShieldAlert`, `--backstein`), **Legit** (`--weidegruen`). Tiles link to `/messages`.
- **Alert chip:** "X Nachrichten älter als 6 Monate" linking to `/messages`, only rendered when `OldCount > 0`.
- **Chart:** weekly stacked bars, one column per week in the selected window — legit bar (`--weidegruen`) with the spam bar (`--backstein`) stacked on top. Rendered with plain elements/flex sizing, no canvas/SVG library. Tooltips (`title`/focusable label) give exact per-bucket numbers.
- **Legend** for the two series, WCAG AA contrast against the card background.
- **Accessibility:** a visually-hidden per-week table (week, legit, spam) for screen readers; bars are keyboard-focusable with `aria-label`.
- **Period toggle:** segmented `4 Wochen / 3 Monate / 6 Monate` (same pattern as the filter buttons in `messages.astro`), re-fetches `/api/messages/stats?days=28|90|180`.
- **States:** loading, empty (no messages at all), and a visible error row if the fetch fails (mirrors `messages.astro` states).
- **Styles:** scoped `<style>` block, two-space indentation, responsive (bars collapse on small widths), no global CSS additions.

## 3. Verification

- `dotnet build src/dashboard-api/dashboard-api.csproj`
- `cd src/dashboard && pnpm run build` (runs `astro check`)
- Manual: hit `GET /api/messages/stats` via `requests.http`, confirm shape; in the browser check KPI update on range toggle, stacked-bar rendering, tooltips, legend, alert chip, and loading/error states.
- `git status`/`git diff` review before opening the PR.

## Known limitations / notes

- Messages stored before the spam filter shipped deserialise with `IsSpam = false` and count toward "legit"; the existing age marker behaviour is unchanged.
- The bucket window is inclusive of the current week; the "old messages" alert uses the same `30 * 6` day threshold as the inbox markers.
- `dashboard-api.Tests`/`website-api.Tests` are referenced in `alpakasoelde.slnx` and `AGENTS.md` but not present in the working tree; verification relies on builds, `astro check`, and the `requests.http` samples.
- No chart dependency is added; if a richer chart library is wanted later it can be layered on without touching the endpoint contract.