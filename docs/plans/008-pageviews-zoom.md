# Pageviews Explorer Zoom Plan

## Goal

Extend the pageviews explorer from `007-pageviews-explorer.md` with time-axis zoom: drag a selection over the chart to zoom into that timeframe, zoom and pan via mouse wheel and trackpad pinch (x axis only), and reset the zoom back to the full period.

## Decisions

- **Zoom is client-side rendering only** — no backend changes. `GET /api/pageviews/stats` keeps returning the full period; zooming re-scales the x axis on the existing data. KPI tiles, tables, and the sr-only table are unaffected.
- **Drag-select zoom via layerchart `brush`:** `brush={{ axis: 'x' }}` on all four chart branches (raw `Chart` for stacked/grouped bars, `LineChart`/`AreaChart` composites for line/area). Dragging creates a selection; releasing zooms the x domain to it. Works with band scales (categorical `Period` keys) via `ChartState.zoomToBrush`.
- **Wheel and trackpad-pinch zoom via `transform`:** `transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale' }}` restricts zoom/pan to the x axis in data space; the trackpad pinch (ctrl+wheel) is always active when `scrollMode` is not `none`. With `mode: 'domain'`, the brush end handler zooms through the shared transform state, so both interactions operate on one zoom state.
- **Reset affordances:** clicking the empty chart area clears the brush and resets the zoom (brush `clickToReset`, default true; with domain-mode transform this calls `transform.reset()`), double-clicking the selection or frame resets too, and a small "Zoom zurücksetzen" button next to the chart toolbar (visible only while zoomed, tracked via `onTransform`/`context.transform`) calls `TransformState.reset()`.
- **Zoom limits:** `scaleExtent: [1, maxScale]` caps zoom-in so at least a few periods stay visible (const, e.g. 56× for hour granularity); brush `minExtent: { x: 1 }` prevents degenerate empty selections (category counts on band scales).
- **Touch devices:** drag-select zoom works; two-finger pinch is not available while the brush is enabled (layerchart disables transform pointer gestures when `brush` is active). Trackpad pinch (ctrl+wheel) and wheel zoom work on desktop. Accepted limitation — see Known limitations.

## Milestones (tracked)

- [ ] Add `brush` and `transform` props to the stacked bars, grouped bars, line, and area branches in `PageViewStats.svelte`
- [ ] Add "Zoom zurücksetzen" button, visible only while zoomed, wired to the bound chart `context` / `onTransform`
- [ ] Configure `scaleExtent`, brush `minExtent`, verify band snapping while zoomed (bars and line/area) and tooltip/labels in the zoomed state
- [ ] Verify: `cd src/dashboard && pnpm run build` (runs `astro check`), `pnpm run check:svelte`
- [ ] Manual: drag-select zoom, wheel zoom, trackpad pinch, all three reset paths, per chart type × granularity × grouping

## 1. Frontend (`src/dashboard/src/components/PageViewStats.svelte`)

- Add to each chart branch (stacked/grouped bars on raw `Chart`, line/area on the composites — both forward `brush`/`transform` via props):
  - `brush={{ axis: 'x', minExtent: { x: 1 } }}`
  - `transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale', scaleExtent: [1, maxScale] }}`
- Bind the chart context (`bind:context={chartCtx}`) on all four branches; the composites already expose it via their `bind:context`.
- Reset button: maintain a `zoomed` flag from `onTransform` (scale > 1 or translate.x ≠ 0) or `context.transform`; render "Zoom zurücksetzen" in the toolbar row only while `zoomed`; on click call `chartCtx.transform.reset()`.
- Keep the period/granularity/grouping/chart-type controls; changing any of them refetches and re-renders the chart, which naturally resets the zoom state (fresh chart instance).
- X-axis tick labels keep the existing `formatAxisLabel` per granularity; no changes to tooltip snippets or legends.

## 2. Verification

- `cd src/dashboard && pnpm run build` (runs `astro check`) and `pnpm run check:svelte` — 0 errors / 0 warnings.
- Manual in dev: for each chart type (stacked/grouped bars, line, area) and each granularity (week/day/hour):
  - drag-select a small range → chart zooms to it; brush selection clears after release
  - drag-select again while zoomed → zooms into the next selection; edges stay clamped to the data domain
  - mouse wheel / trackpad pinch → x-axis zoom/pan only, y axis unchanged
  - reset via click on empty area, double-click on selection, and the "Zoom zurücksetzen" button
  - tooltip (incl. Gesamt row), legend, and Highlight still correct while zoomed; labels stay readable at 56× zoom
- `git status` / `git diff` review before opening the PR; commit message `feat(dashboard): add time zoom to pageviews explorer`.

## Known limitations / notes

- Two-finger touch pinch is unavailable while the brush is active (layerchart pointer-gesture lock); drag-select zoom, wheel zoom, and trackpad pinch (ctrl+wheel) remain available. Revisit if layerchart lifts the lock or if a touch-pinch implementation is desired (would require custom pointer handling).
- Double-clicking the chart frame selects the full range (zoom-out view) and double-clicking the selection resets — both layerchart defaults, kept as additional reset paths.
- Zoom is not persisted and resets on any control change, refetch, or re-render; no URL/state persistence planned.
- Band-scale zooming snaps to period boundaries; sub-period zoom is impossible by design (data is bucketed).