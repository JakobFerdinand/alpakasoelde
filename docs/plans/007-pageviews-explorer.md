# Configurable Pageviews Explorer Plan

## Goal

Turn the dashboard "Seitenaufrufe" page into a metrics explorer similar to the Azure Portal "const analysis" (metrics) view: choose the granularity (week / day / hour), group by a property (total / page / device category / origin domain), and pick the chart type (stacked columns / grouped columns / lines / area). Pageviews per day and per hour become visible as line charts, stacked columns, and individual lines per page.

## Decisions

- **One configurable chart replaces the three fixed weekly charts** (paths, devices, origins). KPI tiles and the tables (Top-Seiten, Gerätekategorien, Herkunftsdomains) stay.
- **Granularity limits:** hour is only offered for the 28-day period (max 672 buckets). For 90/180 days the hour option is disabled; selecting a longer period while hour is active falls back to day.
- **Backend shape:** extend the existing `GET /api/pageviews/stats` endpoint with `granularity` (`week|day|hour`) and `groupBy` (`path|total|device|origin`). The fixed `Series`/`PathSeries`/`DeviceSeries`/`OriginSeries` response fields are replaced by one generic `Series` of `(Period, Group, Count)`; the aggregates `Total`, `UniquePaths`, `TopPaths`, `Devices`, `Origins` stay for the KPI tiles and tables. Defaults keep the current view (`week` + `path`) to avoid breaking the page.
- **Chart rendering:** stays in `PageViewStats.svelte` (layerchart). Stacked bars use the existing `groupStackData`; grouped bars use `stackOffsetSeparated` (layerchart `groupBy` option); line/area render one series per group on a shared y scale (individual lines per page).
- **No tests:** no unit test project exists in this repo; verification relies on builds, `astro check`, and the `requests.http` samples.
- **Svelte stays** for this interactive page, consistent with the existing island migration (see `005-svelte-islands-migration.md`).

## Milestones (tracked)

- [ ] Backend: extend `GetPageViewStats.cs` with `granularity`/`groupBy` query params, generic bucketing (week/day/hour, total/path/device/origin), hour clamp to 28 days, refactored `Result`
- [ ] Backend: update `requests.http` samples and response excerpt
- [ ] Frontend: add granularity / group-by / chart-type controls to `PageViewStats.svelte` (hour disabled for 90/180 days)
- [ ] Frontend: render stacked bars, grouped bars, line, and area chart types with per-granularity axis/tooltip labels
- [ ] Frontend: remove the three fixed weekly chart sections, keep KPI tiles + tables, add sr-only table for the explorer chart
- [ ] Verify: `cd src/dashboard-api && dotnet run` and exercise the extended `requests.http` samples
- [ ] Verify: `cd src/dashboard && pnpm run build` (runs `astro check`)
- [ ] Manual: all combinations of 4 chart types × 4 groupings × 3 granularities × 3 periods; hour disabled on 90/180 days

## 1. Backend (`src/dashboard-api/features/pageviews/GetPageViewStats.cs`)

- Extend the function to read `granularity` (default `week`) and `groupBy` (default `path`); invalid values fall back to the defaults. Extend `Query` accordingly.
- Clamp `days` to 28 when `granularity = hour`.
- Replace the four fixed bucketing loops with one generic loop producing `Series` of `Bucket(string Period, string? Group, int Count)`:
  - `total` → one row per period, `Group = null`
  - `path` → top 6 paths plus `"Übrige"` (existing `ChartPathsLimit` logic)
  - `device` → the 4 fixed `DeviceCategories`, always present
  - `origin` → top 6 domains plus `"Übrige"`, external referrers only (existing logic)
- Period format and gap-filling step per granularity:
  - `week` → `yyyy-MM-dd` (Monday), step 7 days (unchanged behaviour)
  - `day` → `yyyy-MM-dd`, step 1 day
  - `hour` → `yyyy-MM-dd'T'HH:mm` (UTC), step 1 hour
- Refactor `Result`: drop `Series`, `PathSeries`, `DeviceSeries`, `OriginSeries`; add `Granularity`, `GroupBy`, and generic `Series`. Keep `Total`, `UniquePaths`, `TopPaths`, `Devices`, `Origins`.
- Keep `GetWeekStart`, `GetDeviceCategory`, `IsInternalReferrer` helpers; the read store and 180-day lookback are unchanged.

## 2. Backend samples (`src/dashboard-api/requests.http`)

- Add requests with `granularity=day`, `granularity=hour`, `groupBy=total`, `groupBy=device`, `groupBy=origin` combinations.
- Update the response excerpt to the new `series` shape.

## 3. Frontend (`src/dashboard/src/components/PageViewStats.svelte`)

### State and loading

- New state: `granularity` (`week|day|hour`, default `week`), `groupBy` (`path|total|device|origin`, default `path`), `chartType` (`bars-stacked|bars-grouped|line|area`, default `bars-stacked`).
- `load()` fetches `/api/pageviews/stats?days=..&granularity=..&groupBy=..`; any control change refetches using the existing `AbortController` pattern.
- Hour option is disabled (styled, `disabled` attribute) when `days > 28`; selecting 90/180 days while hour is active falls back to `day`.

### Controls

Segmented buttons matching the existing `.period-toggle` style, laid out in a toolbar row:

- Zeitgranularität: Stunde / Tag / Woche
- Gruppierung: Gesamt / Seite / Gerätekategorie / Herkunftsdomain
- Diagrammtyp: Säulen gestapelt / Säulen gruppiert / Linien / Fläche
- Keep the existing period toggle (4 Wochen / 3 Monate / 6 Monate)

### Chart data and rendering

- Legend keys and colors derived from the distinct `Group` values (existing pattern); `total` renders a single series with the primary color.
- Stacked bars: `groupStackData(rows, { xKey: 'Period', stackBy: 'Group' })` + `<Bars />` (unchanged).
- Grouped bars: `groupStackData` with the `groupBy` option (or `stackOffsetSeparated`) + `<Bars />`.
- Line / area: one `<Line>` / `<Area>` per group series on the shared y scale; `total` renders a single line/area.
- Shared `Axis` (left grid + bottom rule), `Highlight`, `Tooltip` with `Gesamt` row (sum over groups), `format="integer"`.
- X-axis and tooltip period labels per granularity: week → `Woche ab dd.mm.yyyy` (existing `formatWeekLabel`), day → `dd.mm.yyyy`, hour → `dd.mm. HH:MM`.

### Content changes

- Remove the three fixed weekly chart sections (path stacked bars, device weekly chart, origin weekly chart) and their sr-only tables.
- Keep: KPI tiles (Gesamt / Meistbesuchte Seite / Einzigartige Seiten), Top-Seiten table, Gerätekategorien distribution, Herkunftsdomains table.
- Add an sr-only table for the explorer chart (period, group, count) to preserve accessibility parity.

## 4. Verification

- `cd src/dashboard-api && dotnet run`, then hit the extended `requests.http` samples (each granularity × groupBy combination; hour with `days=90` must be clamped to 28 days).
- `cd src/dashboard && pnpm run build` (runs `astro check`).
- Manual in dev: all 4 chart types × 4 groupings × 3 granularities × 3 periods; hour disabled on 90/180 days; tooltip totals and legends correct for line/area.
- `git status` / `git diff` review before opening the PR; commit message `feat(dashboard): add configurable pageviews explorer`.

## Known limitations / notes

- No unit test project exists; no unit tests are added in this change.
- Hour granularity is capped at 28 days to keep the bucket count manageable (672 points max).
- The 180-day lookback of the read store is unchanged; `week` remains the only granularity useful for the full 180-day window.
- The old `PathSeries`/`DeviceSeries`/`OriginSeries` response fields are removed; only the dashboard consumes this endpoint, so no external compatibility concern.