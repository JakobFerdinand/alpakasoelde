# Pageviews Custom Date Range Plan

## Goal

Extend the pageviews time-range picker with a "7 Tage" preset, make "4 Wochen" the default, and add a custom date span picker so users can query arbitrary date ranges beyond the fixed presets.

## Current state

- **Period presets** in `PageViewStats.svelte:32-36`:
  ```
  { label: '4 Wochen', days: 28 },
  { label: '3 Monate', days: 90 },
  { label: '6 Monate', days: 180 },
  ```
- **State:** a single `days` number drives the API call; `setPeriod(days)` at `:125-131` updates `days` and triggers `load()`.
- **API:** `GET /api/pageviews/stats?days={days}&granularity={granularity}&groupBy={groupBy}` (`GetPageViewStats.cs:27-31`). `days` is clamped to `Math.Min(requestedDays, 180)`. The read store loads up to 180 days of partitions.
- **Granularity limit:** hour is disabled when `days > 28` (`hourDisabled = days > 28`).
- **Existing helpers:** `formatDateForInput()` in `src/dashboard/src/utils/formatters.ts:36-44` returns `yyyy-mm-dd` for `<input type="date">`.

## Decisions

- **Add "7 Tage" (7 days)** as the first preset button. Presets become: 7 Tage, 4 Wochen (default), 3 Monate, 6 Monate.
- **4 Wochen stays the default** — the API default is already `days=28`, matching the current behaviour.
- **Custom date range replaces the `days` query param** with `from` and `to` date params (ISO `yyyy-MM-dd`). The API reads both and computes `days` as `(to - from).Days + 1`. This avoids changing the aggregation logic — the handler already filters by `DateTimeOffset.Now.AddDays(-days)`. The only change is computing the window start/end from explicit dates instead of "now minus N days".
- **Custom range UI:** a row of two `<input type="date">` fields ("Von" / "Bis") plus an "Anwenden" button, shown below the preset toggle when the user clicks a "Benutzerdefiniert" toggle or when none of the preset buttons are active. When a custom range is active, the preset buttons are visually de-emphasised (none active); selecting a preset clears the custom dates.
- **Granularity clamp:** the hour-disabled logic stays based on the computed `days` value; custom ranges longer than 28 days still disable hour.
- **Max range:** capped at 180 days (server-side clamp). The "Bis" input max is today; "Von" min is 180 days before "Bis".
- **No backend breaking change:** existing `days` param remains supported; `from`/`to` are additive. Clients that only send `days` work unchanged.

## Milestones (tracked)

- [ ] Backend: extend `GetPageViewStats.cs` with `from` / `to` query params, compute days from date range
- [ ] Backend: update `requests.http` samples with `from`/`to` examples
- [ ] Frontend: add "7 Tage" preset, reorder presets, keep "4 Wochen" default
- [ ] Frontend: add custom date range picker UI (Von / Bis inputs + Anwenden button)
- [ ] Frontend: wire custom date range to API call (`from`/`to` params instead of `days`)
- [ ] Frontend: sync preset selection and custom range state (selecting a preset clears custom dates, custom dates de-emphasise presets)
- [ ] Verify: `cd src/dashboard-api && dotnet run` and exercise extended `requests.http` samples
- [ ] Verify: `cd src/dashboard && pnpm run build` (runs `astro check`)

## 1. Backend (`src/dashboard-api/features/pageviews/GetPageViewStats.cs`)

### Function entry (`Run`, lines 23-48)

- After the existing `days` parsing (:27-31), add optional `from` / `to` query param parsing:
  ```csharp
  string? fromParam = req.Query["from"];
  string? toParam = req.Query["to"];

  if (DateOnly.TryParse(fromParam, out DateOnly from) && DateOnly.TryParse(toParam, out DateOnly to) && to >= from)
  {
      int computedDays = (to.DayNumber - from.DayNumber) + 1;
      days = Math.Min(computedDays, TableLookbackDays);
  }
  ```
- When `from`/`to` are provided, they override the `days` value. The handler already computes the window as `DateTimeOffset.UtcNow.AddDays(-days)`, so the effective window shifts to `(to - days + 1)` through `to`. This is slightly different from what the user expects (they expect "from" to be the start), so we also pass the resolved `from` date as a `WindowStart` in the Query.

### Query record

- Extend: `public sealed record Query(int Days, string Granularity, string GroupBy, DateTimeOffset? WindowStart = null);`
- When `from` is provided, compute `WindowStart = from.ToDateTimeOffset(TimeOnly.MinValue)` (UTC). The handler uses this instead of `DateTimeOffset.UtcNow.AddDays(-days)`.

### Handler (`HandleAsync`)

- At the top where `inWindow` is computed (:98-107), replace the fixed `DateTimeOffset.UtcNow.AddDays(-query.Days)` with:
  ```csharp
  DateTimeOffset windowStart = query.WindowStart ?? DateTimeOffset.UtcNow.AddDays(-query.Days);
  var inWindow = all.Where(e => e.Timestamp >= windowStart);
  ```
- Everything downstream stays unchanged.

### No other changes

- `TablePageViewReadStore` stays as-is (reads up to 180 days of partitions, handler filters the window).
- `Result` shape is unchanged.

## 2. Backend samples (`src/dashboard-api/requests.http`)

- Add request samples with `?days=7` for the new 7-day preset.
- Add request samples with `?from=2026-07-01&to=2026-08-25` for a custom date range.
- Update the response excerpt to note the new parameter support.

## 3. Frontend (`src/dashboard/src/components/PageViewStats.svelte`)

### State

- `periods` array reordered and extended:
  ```ts
  const periods = [
    { label: '7 Tage', days: 7 },
    { label: '4 Wochen', days: 28 },
    { label: '3 Monate', days: 90 },
    { label: '6 Monate', days: 180 },
  ];
  ```
- New state variables:
  ```ts
  let customRange = $state(false);
  let fromDate = $state('');
  let toDate = $state('');
  ```
- `days` stays at 28 as default (unchanged).

### Computed values

- `computedDays` derived: when `customRange && fromDate && toDate`, compute `(Date.parse(toDate) - Date.parse(fromDate)) / 86400000 + 1`, clamped to 180 and minimum 1. Otherwise use `days`.
- `hourDisabled` re-derived from `computedDays > 28`.
- `hasCustomDates` derived: `customRange && fromDate !== '' && toDate !== '' && Date.parse(toDate) >= Date.parse(fromDate)`.

### Fetching

- `fetchStats` at `:133-140`: when `hasCustomDates`, build the URL with `from=${fromDate}&to=${toDate}` instead of `days=${days}`:
  ```ts
  const params = new URLSearchParams({ granularity, groupBy });
  if (hasCustomDates) {
    params.set('from', fromDate);
    params.set('to', toDate);
  } else {
    params.set('days', String(days));
  }
  const res = await fetch(`/api/pageviews/stats?${params}`, { signal });
  ```

### Period toggle UI (`:243-255`)

- The four preset buttons render as before. `is-active` is now:
  ```svelte
  class:is-active={!customRange && days === period.days}
  ```
- Clicking a preset sets `customRange = false` and calls `setPeriod(period.days)`.

### Custom date range UI

- Add a new row below the period toggle (or as part of the toolbar):
  ```svelte
  <button
    type="button"
    class="period-button"
    class:is-active={customRange}
    aria-pressed={customRange}
    onclick={() => { customRange = !customRange; }}
  >
    Benutzerdefiniert
  </button>

  {#if customRange}
    <div class="custom-date-range">
      <label class="date-label">
        Von
        <input
          type="date"
          bind:value={fromDate}
          max={toDate || todayIso}
        />
      </label>
      <label class="date-label">
        Bis
        <input
          type="date"
          bind:value={toDate}
          min={fromDate}
          max={todayIso}
        />
      </label>
      <button
        type="button"
        class="period-button"
        disabled={!hasCustomDates}
        onclick={() => load()}
      >
        Anwenden
      </button>
    </div>
  {/if}
  ```
- `todayIso` is a module-level constant: `new Date().toISOString().slice(0, 10)`.

### Loading feedback

- When the user clicks "Anwenden", `load()` fires. No separate debounce needed since the button explicitly triggers the fetch.
- When `customRange` is toggled off, revert to the previously selected preset `days` value (already tracked by the `days` state variable).

### Styles

- Add `.custom-date-range` styles (flex row, gap, aligned with toolbar):
  ```css
  .custom-date-range {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    align-self: flex-start;
  }

  .date-label {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-size: 0.9rem;
    font-weight: 600;
    color: var(--taubenblau);
  }

  .date-label input[type="date"] {
    border: 1px solid rgba(0, 32, 73, 0.15);
    border-radius: 0.375rem;
    padding: 0.4rem 0.6rem;
    font-family: inherit;
    font-size: 0.85rem;
    color: var(--taubenblau);
    background: var(--schurwolle);
  }

  .date-label input[type="date"]:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: -1px;
  }
  ```
- Date inputs use the existing CSS variables for colours and inherit the dashboard typography.

## 4. Verification

- `cd src/dashboard-api && dotnet run` and exercise:
  - `GET /api/pageviews/stats?days=7&granularity=day&groupBy=path`
  - `GET /api/pageviews/stats?from=2026-08-01&to=2026-08-25&granularity=day&groupBy=path`
  - `GET /api/pageviews/stats?from=2026-02-01&to=2026-08-25&granularity=week&groupBy=path` (exceeds 180 days → clamped)
  - `GET /api/pageviews/stats?days=7&granularity=hour&groupBy=path` (hour enabled)
  - `GET /api/pageviews/stats?from=2026-07-01&to=2026-08-25&granularity=hour&groupBy=path` (custom range > 28 days → hour disabled)
- `cd src/dashboard && pnpm run build` (runs `astro check`).
- Manual in dev: all four presets render correctly; "7 Tage" shows day granularity data; custom date range fetches the correct window; selecting a preset after custom range reverts; hour is disabled for custom ranges > 28 days; date inputs constrain each other (Von <= Bis, Bis <= today).
- `git status` / `git diff` review before opening the PR; commit message follows Karma schema.

## Known limitations / notes

- No unit test project exists; no unit tests are added in this change.
- The custom range is limited to 180 days max, matching the existing server-side clamp. A range wider than 180 days silently truncates to the most recent 180 days.
- Granularity "hour" is limited to 28 days regardless of whether the range is a preset or custom — this is an existing constraint.
- The `from`/`to` params are additive to the existing API; older dashboard versions that only send `days` are unaffected.
