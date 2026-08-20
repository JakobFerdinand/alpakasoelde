<script lang="ts">
  import { onMount } from 'svelte';
  import { AreaChart, Axis, Bars, Chart, Highlight, Layer, LineChart, Tooltip, groupStackData, type ChartState } from 'layerchart';
  import { scaleBand } from 'd3-scale';
  import { sum } from 'd3-array';
  import { BarChart3, Eye, Files } from '@lucide/svelte';

  type PathCount = { Path: string; Count: number };
  type DeviceCount = { Category: string; Count: number };
  type OriginCount = { Domain: string; Count: number };
  type Bucket = { Period: string; Group: string | null; Count: number };
  type Granularity = 'week' | 'day' | 'hour';
  type GroupBy = 'total' | 'path' | 'device' | 'origin';
  type ChartType = 'bars-stacked' | 'bars-grouped' | 'line' | 'area';
  type StatsResult = {
    Total: number;
    UniquePaths: number;
    TopPaths: PathCount[];
    Devices: DeviceCount[];
    Origins: OriginCount[];
    Series: Bucket[];
    Granularity: Granularity;
    GroupBy: GroupBy;
  };
  type StackItem = { Group: string; value: number };

  const periods = [
    { label: '4 Wochen', days: 28 },
    { label: '3 Monate', days: 90 },
    { label: '6 Monate', days: 180 },
  ];
  const granularities: { label: string; value: Granularity }[] = [
    { label: 'Stunde', value: 'hour' },
    { label: 'Tag', value: 'day' },
    { label: 'Woche', value: 'week' },
  ];
  const groupings: { label: string; value: GroupBy }[] = [
    { label: 'Gesamt', value: 'total' },
    { label: 'Seite', value: 'path' },
    { label: 'Gerätekategorie', value: 'device' },
    { label: 'Herkunftsdomain', value: 'origin' },
  ];
  const chartTypes: { label: string; value: ChartType }[] = [
    { label: 'Säulen gestapelt', value: 'bars-stacked' },
    { label: 'Säulen gruppiert', value: 'bars-grouped' },
    { label: 'Linien', value: 'line' },
    { label: 'Fläche', value: 'area' },
  ];
  const chartPalette = [
    'var(--weidegruen)',
    'var(--backstein)',
    'var(--himmelblau)',
    'var(--taubenblau)',
    '#b3822a',
    '#5f6b8a',
    '#8a6a9a',
  ];

  let activeController: AbortController | null = null;

  let days = $state(28);
  let granularity = $state<Granularity>('week');
  let groupBy = $state<GroupBy>('path');
  let chartType = $state<ChartType>('bars-stacked');
  let loading = $state(true);
  let error = $state('');
  let stats = $state<StatsResult | null>(null);

  const hourDisabled = $derived(days > 28);

  const seriesRows = $derived(
    (stats?.Series ?? []).map((row) => ({
      Period: row.Period,
      Group: row.Group ?? 'Gesamt',
      Count: row.Count,
    })),
  );
  const colorKeys = $derived(Array.from(new Set(seriesRows.map((row) => row.Group))));
  const keyColors = $derived(colorKeys.map((_, index) => chartPalette[index % chartPalette.length]));

  const stackedData = $derived.by(() =>
    seriesRows.length
      ? groupStackData(
          seriesRows.map((row) => ({ Period: row.Period, Group: row.Group, value: row.Count })),
          { xKey: 'Period', stackBy: 'Group' },
        )
      : [],
  );

  const groupedData = $derived.by(() => {
    if (!seriesRows.length) return [];
    const periods = Array.from(new Set(seriesRows.map((row) => row.Period)));
    return periods.map((period) => {
      const row: Record<string, string | number> = { Period: period };
      for (const key of colorKeys) {
        row[key] = seriesRows.find((r) => r.Period === period && r.Group === key)?.Count ?? 0;
      }
      return row;
    });
  });

  const groupedSeries = $derived(colorKeys.map((key, index) => ({ key, color: keyColors[index] })));

  const total = $derived(stats?.Total ?? 0);
  const topPath = $derived(stats?.TopPaths[0] ?? null);
  const uniquePages = $derived(stats?.UniquePaths ?? 0);
  const hasData = $derived(Boolean(stats) && stats!.Total > 0);
  const devices = $derived(stats?.Devices ?? []);
  const deviceTotal = $derived(devices.reduce((acc, device) => acc + device.Count, 0));
  const granularityLabel = $derived(
    granularity === 'hour' ? 'Stunde' : granularity === 'day' ? 'Tag' : 'Woche',
  );
  const groupLabel = $derived(
    groupBy === 'total'
      ? 'Gesamt'
      : groupBy === 'device'
        ? 'Gerätekategorie'
        : groupBy === 'origin'
          ? 'Herkunftsdomain'
          : 'Seite',
  );

  function formatCount(value: number): string {
    return new Intl.NumberFormat('de-AT').format(value);
  }

  function formatPercent(value: number): string {
    return new Intl.NumberFormat('de-AT', { style: 'percent', maximumFractionDigits: 1 }).format(value);
  }

  function formatPeriodLabel(period: string): string {
    const [datePart, timePart] = period.split('T');
    const [year, month, day] = datePart.split('-');
    if (timePart) return `${day}.${month}. ${timePart}`;
    if (granularity === 'week') return `Woche ab ${day}.${month}.${year}`;
    return `${day}.${month}.${year}`;
  }

  function formatAxisLabel(period: string): string {
    const [datePart, timePart] = period.split('T');
    const [year, month, day] = datePart.split('-');
    if (timePart) return `${day}.${month}. ${timePart}`;
    return `${day}.${month}.${year}`;
  }

  function setPeriod(daysValue: number) {
    days = daysValue;
    if (granularity === 'hour' && daysValue > 28) {
      granularity = 'day';
    }
    load();
  }

  async function load() {
    activeController?.abort();
    const controller = new AbortController();
    activeController = controller;
    loading = true;
    error = '';
    try {
      const res = await fetch(
        `/api/pageviews/stats?days=${days}&granularity=${granularity}&groupBy=${groupBy}`,
        { signal: controller.signal },
      );
      if (!res.ok) throw new Error(`Failed to load stats (${res.status})`);
      if (controller.signal.aborted) return;
      stats = await res.json();
    } catch (e) {
      if (controller.signal.aborted) return;
      console.error(e);
      error = 'Statistik konnte nicht geladen werden.';
    } finally {
      if (!controller.signal.aborted) loading = false;
    }
  }

  onMount(() => {
    load();
    return () => activeController?.abort();
  });
</script>

<section class="dashboard-pageviews section">
  <div class="container">
    <div class="pageview-stats-card card">
      <h2 class="stats-title">Seitenaufrufe</h2>

      <div class="chart-toolbar">
        <div class="period-toggle" role="group" aria-label="Zeitgranularität wählen">
          {#each granularities as option}
            <button
              type="button"
              class="period-button"
              class:is-active={granularity === option.value}
              class:is-disabled={option.value === 'hour' && hourDisabled}
              aria-pressed={granularity === option.value}
              disabled={option.value === 'hour' && hourDisabled}
              title={option.value === 'hour' && hourDisabled ? 'Stundengenauigkeit nur für 4 Wochen verfügbar' : undefined}
              onclick={() => {
                granularity = option.value;
                load();
              }}
            >
              {option.label}
            </button>
          {/each}
        </div>

        <div class="period-toggle" role="group" aria-label="Gruppierung wählen">
          {#each groupings as option}
            <button
              type="button"
              class="period-button"
              class:is-active={groupBy === option.value}
              aria-pressed={groupBy === option.value}
              onclick={() => {
                groupBy = option.value;
                load();
              }}
            >
              {option.label}
            </button>
          {/each}
        </div>

        <div class="period-toggle" role="group" aria-label="Diagrammtyp wählen">
          {#each chartTypes as option}
            <button
              type="button"
              class="period-button"
              class:is-active={chartType === option.value}
              aria-pressed={chartType === option.value}
              onclick={() => {
                chartType = option.value;
              }}
            >
              {option.label}
            </button>
          {/each}
        </div>

        <div class="period-toggle" role="group" aria-label="Zeitraum wählen">
          {#each periods as period}
            <button
              type="button"
              class="period-button"
              class:is-active={days === period.days}
              aria-pressed={days === period.days}
              onclick={() => setPeriod(period.days)}
            >
              {period.label}
            </button>
          {/each}
        </div>
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
          {#snippet explorerTooltip({ context }: { context: ChartState<any> })}
            {@const visibleSeries = context.tooltip.series.filter((s) => s.visible)}
            <Tooltip.Root {context}>
              {#snippet children({ data })}
                <Tooltip.Header>{formatPeriodLabel(data.Period)}</Tooltip.Header>
                <Tooltip.List>
                  {#each visibleSeries as s (s.key)}
                    <Tooltip.Item
                      label={s.label}
                      value={s.value}
                      color={s.color}
                      format="integer"
                      valueAlign="right"
                    />
                  {/each}
                  <Tooltip.Separator />
                  <Tooltip.Item
                    label="Gesamt"
                    value={sum(visibleSeries, (s) => s.value ?? 0)}
                    format="integer"
                    valueAlign="right"
                  />
                </Tooltip.List>
              {/snippet}
            </Tooltip.Root>
          {/snippet}

          {#if chartType === 'bars-stacked'}
            <Chart
              data={stackedData}
              x="Period"
              xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
              y="values"
              yNice
              c="Group"
              cDomain={colorKeys}
              cRange={keyColors}
              padding={{ left: 32, bottom: 20, top: 8 }}
              tooltipContext={{ mode: 'band' }}
              height={300}
            >
              {#snippet children({ context })}
                <Layer>
                  <Axis placement="left" grid rule />
                  <Axis placement="bottom" rule format={formatAxisLabel} />
                  <Bars strokeWidth={1} />
                  <Highlight area />
                </Layer>

                <Tooltip.Root>
                  {#snippet children({ data })}
                    <Tooltip.Header>{formatPeriodLabel(data.Period)}</Tooltip.Header>
                    <Tooltip.List>
                      {#each data.data as item}
                        <Tooltip.Item
                          label={item.Group}
                          value={item.value}
                          color={context.cScale?.(item.Group)}
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
          {:else if chartType === 'bars-grouped'}
            <Chart
              data={groupedData}
              x="Period"
              xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
              y={colorKeys}
              yNice
              series={groupedSeries}
              seriesLayout="group"
              padding={{ left: 32, bottom: 20, top: 8 }}
              tooltipContext={{ mode: 'band' }}
              height={300}
            >
              {#snippet children()}
                <Layer>
                  <Axis placement="left" grid rule />
                  <Axis placement="bottom" rule format={formatAxisLabel} />
                  {#each groupedSeries as series}
                    <Bars seriesKey={series.key} x1={(d: Record<string, string | number>) => series.key} strokeWidth={1} />
                  {/each}
                  <Highlight area />
                </Layer>

                <Tooltip.Root>
                  {#snippet children({ data })}
                    <Tooltip.Header>{formatPeriodLabel(data.Period)}</Tooltip.Header>
                    <Tooltip.List>
                      {#each groupedSeries as series, index}
                        <Tooltip.Item
                          label={series.key}
                          value={data[series.key] ?? 0}
                          color={keyColors[index]}
                          format="integer"
                          valueAlign="right"
                        />
                      {/each}
                      <Tooltip.Separator />
                      <Tooltip.Item
                        label="Gesamt"
                        value={sum(colorKeys, (key) => data[key] ?? 0)}
                        format="integer"
                        valueAlign="right"
                      />
                    </Tooltip.List>
                  {/snippet}
                </Tooltip.Root>
              {/snippet}
            </Chart>
          {:else if chartType === 'line'}
            <LineChart
              data={groupedData}
              x="Period"
              xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
              series={groupedSeries}
              padding={{ left: 32, bottom: 20, top: 36 }}
              height={300}
              legend={{ placement: 'top' }}
              tooltip={explorerTooltip}
              props={{
                spline: { strokeWidth: 2 },
                xAxis: { format: formatAxisLabel },
              }}
            />
          {:else}
            <AreaChart
              data={groupedData}
              x="Period"
              y={colorKeys}
              xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
              series={groupedSeries}
              padding={{ left: 32, bottom: 20, top: 36 }}
              height={300}
              legend={{ placement: 'top' }}
              tooltip={explorerTooltip}
              props={{
                area: { line: { strokeWidth: 2 } },
                xAxis: { format: formatAxisLabel },
              }}
            />
          {/if}
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

        {#if devices.length > 0}
          <div class="device-block">
            <h3 class="device-title">Gerätekategorien</h3>
            {#each devices as device}
              {@const share = deviceTotal > 0 ? device.Count / deviceTotal : 0}
              <div class="device-row">
                <span class="device-label">{device.Category}</span>
                <div class="device-bar-wrap" aria-hidden="true">
                  <div class="device-bar" style="width: {share * 100}%"></div>
                </div>
                <span class="device-value">{formatCount(device.Count)}</span>
                <span class="device-percent">{formatPercent(share)}</span>
              </div>
            {/each}
          </div>
        {/if}

        <div class="table-wrap">
          <table class="pageview-table">
            <thead>
              <tr>
                <th scope="col">Herkunftsdomain</th>
                <th scope="col" class="table-count">Aufrufe</th>
              </tr>
            </thead>
            <tbody>
              {#each stats?.Origins ?? [] as origin}
                <tr>
                  <th scope="row">{origin.Domain}</th>
                  <td class="table-count">{formatCount(origin.Count)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>

        <h3 class="sr-only">Seitenaufrufe nach {granularityLabel} und {groupLabel}</h3>
        <table class="sr-only-table">
          <thead>
            <tr>
              <th scope="col">{granularityLabel}</th>
              <th scope="col">{groupLabel}</th>
              <th scope="col">Aufrufe</th>
            </tr>
          </thead>
          <tbody>
            {#each stats?.Series ?? [] as row}
              <tr>
                <th scope="row">{formatPeriodLabel(row.Period)}</th>
                <td>{row.Group ?? 'Gesamt'}</td>
                <td>{formatCount(row.Count)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      {/if}
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

  .chart-toolbar {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
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

  .period-button.is-disabled,
  .period-button:disabled {
    opacity: 0.45;
    cursor: not-allowed;
  }

  .period-button:disabled:hover {
    background-color: var(--schurwolle);
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

  .device-block {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding: 1rem;
    border: 1px solid rgba(0, 32, 73, 0.1);
    border-radius: 0.5rem;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
  }

  .device-title {
    margin: 0;
    font-size: 1.1rem;
  }

  .device-row {
    display: grid;
    grid-template-columns: 6.5rem 1fr auto auto;
    align-items: center;
    gap: 0.75rem;
  }

  .device-label {
    font-weight: 600;
    color: var(--taubenblau);
  }

  .device-bar-wrap {
    height: 0.75rem;
    border-radius: 0.375rem;
    background-color: rgba(0, 32, 73, 0.1);
    overflow: hidden;
  }

  .device-bar {
    height: 100%;
    border-radius: 0.375rem;
    background-color: var(--weidegruen);
  }

  .device-value {
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    text-align: right;
  }

  .device-percent {
    font-size: 0.85rem;
    font-variant-numeric: tabular-nums;
    text-align: right;
    min-width: 3.5rem;
    color: var(--taubenblau);
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
    left: -9999px;
    top: auto;
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