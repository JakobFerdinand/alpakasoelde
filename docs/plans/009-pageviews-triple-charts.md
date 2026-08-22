# Pageviews Triple Charts Plan

## Goal

Restore the three per-dimension pageviews charts (Seite / Gerätekategorie / Herkunftsdomain) that existed before `007`, while keeping the explorer features from `007`/`008`: shared granularity, chart type, and period controls plus time zoom. All three charts zoom in sync.

## Decisions

- **Frontend-only change** — no backend changes. The existing `GET /api/pageviews/stats` is called three times in parallel (`groupBy=path|device|origin`) with the same `days`/`granularity`. All three responses share identical `Period` buckets by construction (same backend bucketing loop), so x domains align 1:1.
- **Shared controls:** one toolbar with Zeitraum / Granularität / Diagrammtyp applies to all three charts at once. The Gruppierung toggle and the `Gesamt` grouping are dropped; totals stay visible via the Gesamt tooltip row and KPI tiles.
- **Synchronized zoom:** layerchart 2.2.0 `TransformState` exposes public `setScale(value, opts?)`, `setTranslate(point, opts?)`, and `reset()`. The parent binds all three chart contexts; `onTransform` of any chart mirrors `{scale, translate}` onto the other two behind a `syncing` guard flag to prevent feedback loops. Identical height (300), padding, and width make pixel-space transforms equivalent across charts.
- **Reset affordances:** one "Zoom zurücksetzen" toolbar button (visible while any chart is zoomed) resets all three contexts. Empty-area click / double-click reset on a chart propagates through the same sync path.
- **Component extraction:** the 4-branch renderer (stacked bars, grouped bars, line, area incl. tooltip snippet, axes, highlight, legend, brush + domain transform props) moves into a reusable `PageViewSeriesChart.svelte`; `PageViewStats.svelte` becomes the parent orchestrating data loading, toolbar, sections, and zoom sync.
- **Accessibility parity:** each chart section regains its sr-only table (period, group, count); section headings describe dimension and granularity.
- **Empty datasets:** device/origin sections hide when their series are empty; sync skips missing contexts.

## Milestones (tracked)

- [ ] Add plan doc (`009`)
- [ ] Extract `PageViewSeriesChart.svelte` with the four chart-type branches and zoom props
- [ ] Rework `PageViewStats.svelte`: three parallel fetches, shared toolbar without grouping toggle, three chart sections with sr-only tables
- [ ] Implement synchronized zoom (context array, `syncing` guard, shared reset button)
- [ ] Verify: `cd src/dashboard && pnpm run check && pnpm run check:svelte`
- [ ] Manual: drag/wheel zoom on each chart mirrors to the other two; all reset paths; empty device/origin datasets don't break sync; hour fallback on 90/180 days

## 1. `PageViewSeriesChart.svelte`

- Props: series rows (`{ Period, Group, Count }[]`), derived keys/colors passed in or computed internally, `$bindable` chart `context`, `ontransform` callback, `chartType`.
- Contains the stacked/grouped/line/area branches moved as-is from the current component (tooltip with Gesamt row, band-scale x axis, brush `minExtent: { x: 1 }`, transform `mode: 'domain'` with `scaleExtent: [1, 56]`).

## 2. `PageViewStats.svelte`

- State: `days`, `granularity`, `chartType`, three datasets (`path/device/origin`), combined loading/error handling via `Promise.all` + AbortController.
- Toolbar: Zeitraum (28/90/180), Granularität (Stunde/Tag/Woche, hour disabled > 28 days with day fallback), Diagrammtyp (4 options), zoom reset button.
- Sections: „Seitenaufrufe nach Seite", „Gerätekategorien", „Herkunftsdomains" — legend, chart, sr-only table each; existing tables (Top-Seiten, Gerätekategorien-Balken, Herkunftsdomains) keep their positions relative to their sections.

## 3. Verification

- `cd src/dashboard && pnpm run check && pnpm run check:svelte` — 0 errors / warnings.
- Manual matrix: 3 charts × 4 chart types × 3 granularities × 3 periods; drag-zoom on any chart mirrors instantly; wheel/pinch zoom; reset via empty-area click, double-click, and toolbar button; hour disabled/fallback on 90/180 days.
- Commit message: `feat(dashboard): restore triple pageviews charts with synced zoom`.
