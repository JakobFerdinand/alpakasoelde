<script lang="ts">
  import { onMount } from 'svelte';
  import { Axis, Bars, Chart, Highlight, Layer, Tooltip, groupStackData } from 'layerchart';
  import { scaleBand } from 'd3-scale';
  import { sum } from 'd3-array';
  import { BarChart3, Eye, Files } from '@lucide/svelte';

  type PeriodBucket = { Period: string; Count: number };
  type PathCount = { Path: string; Count: number };
  type PathBucket = { Period: string; Path: string; Count: number };
  type StatsResult = {
    Total: number;
    UniquePaths: number;
    TopPaths: PathCount[];
    Series: PeriodBucket[];
    PathSeries: PathBucket[];
  };
  type StackItem = { kind: string; value: number };

  const periods = [
    { label: '4 Wochen', days: 28 },
    { label: '3 Monate', days: 90 },
    { label: '6 Monate', days: 180 },
  ];
  const chartPalette = [
    'var(--weidegruen)',
    'var(--backstein)',
    'var(--himmelblau)',
    'var(--taubenblau)',
    '#b3822a',
    '#5f6b8a',
  ];

  let days = $state(28);
  let loading = $state(true);
  let error = $state('');
  let stats = $state<StatsResult | null>(null);

  const chartData = $derived.by(() =>
    stats
      ? groupStackData(
          stats.PathSeries.map((row) => ({ Period: row.Period, Path: row.Path, value: row.Count })),
          { xKey: 'Period', stackBy: 'Path' },
        )
      : [],
  );
  const colorKeys = $derived(Array.from(new Set(stats?.PathSeries.map((row) => row.Path) ?? [])));
  const keyColors = $derived(colorKeys.map((_, index) => chartPalette[index % chartPalette.length]));

  const total = $derived(stats?.Total ?? 0);
  const topPath = $derived(stats?.TopPaths[0] ?? null);
  const uniquePages = $derived(stats?.UniquePaths ?? 0);
  const hasData = $derived(Boolean(stats) && stats!.Series.length > 0);

  function formatCount(value: number): string {
    return new Intl.NumberFormat('de-AT').format(value);
  }

  function formatWeekLabel(period: string): string {
    const [year, month, day] = period.split('-');
    return `Woche ab ${day}.${month}.${year}`;
  }

  async function load() {
    loading = true;
    error = '';
    try {
      const res = await fetch(`/api/pageviews/stats?days=${days}`);
      if (!res.ok) throw new Error(`Failed to load stats (${res.status})`);
      stats = await res.json();
    } catch (e) {
      console.error(e);
      error = 'Statistik konnte nicht geladen werden.';
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    load();
  });
</script>

<section class="dashboard-pageviews section">
  <div class="container">
    <div class="pageview-stats-card card">
      <h2 class="stats-title">Seitenaufrufe</h2>

      <div class="period-toggle" role="group" aria-label="Zeitraum wählen">
        {#each periods as period}
          <button
            type="button"
            class="period-button"
            class:is-active={days === period.days}
            aria-pressed={days === period.days}
            onclick={() => {
              days = period.days;
              load();
            }}
          >
            {period.label}
          </button>
        {/each}
      </div>

      {#if error}
        <p class="error" role="alert">{error}</p>
      {/if}

      <div class="kpi-row">
        <div class="kpi-tile">
          <BarChart3 class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(total)}</span>
          <span class="kpi-label">Gesamt</span>
        </div>
        <div class="kpi-tile kpi-tile-top">
          <Eye class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value kpi-value-top" title={topPath?.Path}>{topPath?.Path ?? '—'}</span>
          {#if topPath}
            <span class="kpi-percent">{formatCount(topPath.Count)} Aufrufe</span>
          {/if}
          <span class="kpi-label">Meistbesuchte Seite</span>
        </div>
        <div class="kpi-tile">
          <Files class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(uniquePages)}</span>
          <span class="kpi-label">Einzigartige Seiten</span>
        </div>
      </div>

      {#if loading}
        <div class="chart-loading">
          <p class="loading-text">Lade Daten...</p>
        </div>
      {:else if !hasData}
        <p class="chart-message">Keine Seitenaufrufe im Zeitraum.</p>
      {:else}
        <div class="chart-legend" aria-hidden="true">
          {#each colorKeys as key, index}
            <span class="legend-item">
              <span class="legend-swatch" style="background-color: {keyColors[index]}"></span>
              {key}
            </span>
          {/each}
        </div>

        <div class="chart-wrap">
          <Chart
            data={chartData}
            x="Period"
            xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
            y="values"
            yNice
            c="Path"
            cDomain={colorKeys}
            cRange={keyColors}
            padding={{ left: 32, bottom: 20, top: 8 }}
            tooltipContext={{ mode: 'band' }}
            height={300}
          >
            {#snippet children({ context })}
              <Layer>
                <Axis placement="left" grid rule />
                <Axis placement="bottom" rule />
                <Bars strokeWidth={1} />
                <Highlight area />
              </Layer>

              <Tooltip.Root>
                {#snippet children({ data })}
                  <Tooltip.Header>{formatWeekLabel(data.Period)}</Tooltip.Header>
                  <Tooltip.List>
                    {#each data.data as item}
                      <Tooltip.Item
                        label={item.Path}
                        value={item.value}
                        color={context.cScale?.(item.Path)}
                        format="integer"
                        valueAlign="right"
                      />
                    {/each}
                    <Tooltip.Separator />
                    <Tooltip.Item
                      label="Gesamt"
                      value={sum([...data.data], (d: StackItem) => d.value)}
                      format="integer"
                      valueAlign="right"
                    />
                  </Tooltip.List>
                {/snippet}
              </Tooltip.Root>
            {/snippet}
          </Chart>
        </div>

        <div class="table-wrap">
          <table class="pageview-table">
            <thead>
              <tr>
                <th scope="col">Seite</th>
                <th scope="col" class="table-count">Aufrufe</th>
              </tr>
            </thead>
            <tbody>
              {#each stats?.TopPaths ?? [] as path}
                <tr>
                  <th scope="row">{path.Path}</th>
                  <td class="table-count">{formatCount(path.Count)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}

      <h3 class="sr-only">Seitenaufrufe nach Woche und Seite</h3>
      <table class="sr-only-table">
        <thead>
          <tr>
            <th scope="col">Woche</th>
            <th scope="col">Seite</th>
            <th scope="col">Aufrufe</th>
          </tr>
        </thead>
        <tbody>
          {#each stats?.Series ?? [] as bucket}
            {@const bucketRows = (stats?.PathSeries ?? []).filter((row) => row.Period === bucket.Period && row.Count > 0)}
            {#each bucketRows as row}
              <tr>
                <th scope="row">{formatWeekLabel(bucket.Period)}</th>
                <td>{row.Path}</td>
                <td>{formatCount(row.Count)}</td>
              </tr>
            {/each}
          {/each}
        </tbody>
      </table>
    </div>
  </div>
</section>

<style>
  .dashboard-pageviews {
    background-color: var(--auwasser);
    color: var(--taubenblau);
  }

  .pageview-stats-card {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
  }

  .stats-title {
    margin: 0;
    font-size: 1.875rem;
  }

  .period-toggle {
    display: inline-flex;
    border-radius: 0.5rem;
    overflow: hidden;
    border: 1px solid rgba(0, 32, 73, 0.15);
    align-self: flex-start;
  }

  .period-button {
    border: none;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    padding: 0.5rem 1rem;
    font-family: inherit;
    font-size: 0.9rem;
    font-weight: 600;
    cursor: pointer;
  }

  .period-button + .period-button {
    border-left: 1px solid rgba(0, 32, 73, 0.15);
  }

  .period-button:hover {
    background-color: var(--himmelblau);
  }

  .period-button.is-active {
    background-color: var(--weidegruen);
    color: var(--schurwolle);
  }

  .period-button:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: -2px;
  }

  .kpi-row {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 1rem;
  }

  .kpi-tile {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
    padding: 1rem;
    border-radius: 0.5rem;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    border: 1px solid rgba(0, 32, 73, 0.1);
    transition: transform 0.15s ease;
  }

  .kpi-tile-top {
    border-color: var(--weidegruen);
  }

  :global(.kpi-icon) {
    width: 1.25rem;
    height: 1.25rem;
  }

  .kpi-value {
    font-size: 1.5rem;
    font-weight: 700;
    max-width: 100%;
  }

  .kpi-value-top {
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    font-size: 1.1rem;
    line-height: 1.3;
  }

  .kpi-percent {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--weidegruen);
  }

  .kpi-label {
    font-weight: 600;
  }

  .error {
    margin: 0;
    font-weight: 600;
    color: var(--backstein);
  }

  .chart-wrap {
    color: var(--taubenblau);
  }

  .chart-wrap :global(.lc-root-container) {
    --color-primary: var(--weidegruen);
    --color-surface-100: #ffffff;
    --color-surface-200: var(--schurwolle);
    --color-surface-300: rgba(0, 32, 73, 0.15);
    --color-surface-content: var(--taubenblau);
  }

  .chart-legend {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem 1.25rem;
    justify-content: flex-end;
    font-size: 0.85rem;
    font-weight: 600;
  }

  .legend-item {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
  }

  .legend-swatch {
    width: 0.75rem;
    height: 0.75rem;
    border-radius: 0.2rem;
  }

  .chart-loading {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 12rem;
  }

  .chart-message {
    margin: 0;
    font-weight: 600;
  }

  .table-wrap {
    border: 1px solid rgba(0, 32, 73, 0.1);
    border-radius: 0.5rem;
    overflow: hidden;
    background-color: var(--schurwolle);
  }

  .pageview-table {
    width: 100%;
    border-collapse: collapse;
  }

  .pageview-table th,
  .pageview-table td {
    padding: 0.65rem 1rem;
    text-align: left;
    border-bottom: 1px solid rgba(0, 32, 73, 0.1);
  }

  .pageview-table tbody tr:last-child th,
  .pageview-table tbody tr:last-child td {
    border-bottom: none;
  }

  .pageview-table thead th {
    background-color: var(--himmelblau);
    color: var(--taubenblau);
    font-size: 0.85rem;
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  .pageview-table tbody th {
    font-weight: 600;
    color: var(--taubenblau);
  }

  .table-count {
    text-align: right;
    font-variant-numeric: tabular-nums;
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }

  .sr-only-table {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }

  @media (max-width: 600px) {
    .kpi-row {
      grid-template-columns: 1fr;
    }
  }
</style>