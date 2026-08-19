<script lang="ts">
  import { onMount } from 'svelte';
  import { Area, Axis, Bars, Chart, Highlight, Layer, Tooltip, groupStackData } from 'layerchart';
  import { scaleBand, scalePoint } from 'd3-scale';
  import { sum } from 'd3-array';
  import { BarChart3, Eye, Files } from '@lucide/svelte';

  type PeriodBucket = { Period: string; Count: number };
  type PathCount = { Path: string; Count: number };
  type PathBucket = { Period: string; Path: string; Count: number };
  type DeviceCount = { Category: string; Count: number };
  type DeviceBucket = { Period: string; Category: string; Count: number };
  type OriginCount = { Domain: string; Count: number };
  type OriginBucket = { Period: string; Domain: string; Count: number };
  type StatsResult = {
    Total: number;
    UniquePaths: number;
    TopPaths: PathCount[];
    Series: PeriodBucket[];
    PathSeries: PathBucket[];
    Devices: DeviceCount[];
    DeviceSeries?: DeviceBucket[];
    Origins?: OriginCount[];
    OriginSeries?: OriginBucket[];
  };
  type StackItem = { kind: string; value: number };
  type WeekRow = { Period: string; Path: string; value: number };
  type WeekSeriesItem = { key: string; label: string; value: (row: WeekRow) => number; data: WeekRow[]; color: string };

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
    '#8a6a9a',
  ];
  const deviceCategories = ['Mobil', 'Tablet', 'Laptop', 'Breitbild'];
  const deviceColors = ['var(--weidegruen)', 'var(--backstein)', 'var(--himmelblau)', 'var(--taubenblau)'];

  let activeController: AbortController | null = null;

  let days = $state(28);
  let loading = $state(true);
  let error = $state('');
  let stats = $state<StatsResult | null>(null);

  let selectedWeek = $state<string | null>(null);
  let weekStats = $state<StatsResult | null>(null);
  let weekLoading = $state(false);
  let weekError = $state('');

  const chartData = $derived.by(() => {
    if (!stats?.PathSeries?.length) return [];
    return groupStackData(
      stats.PathSeries.map((row) => ({ Period: row.Period, Path: row.Path, value: row.Count })),
      { xKey: 'Period', stackBy: 'Path' },
    );
  });
  const colorKeys = $derived(Array.from(new Set((stats?.PathSeries ?? []).map((row) => row.Path))));
  const keyColors = $derived(colorKeys.map((_, index) => chartPalette[index % chartPalette.length]));
  const deviceChartData = $derived.by(() => {
    if (!stats?.DeviceSeries?.length) return [];
    return groupStackData(
      stats.DeviceSeries.map((row) => ({ Period: row.Period, Category: row.Category, value: row.Count })),
      { xKey: 'Period', stackBy: 'Category' },
    );
  });
  const hasDeviceSeries = $derived((stats?.DeviceSeries ?? []).some((row) => row.Count > 0));
  const originKeys = $derived(Array.from(new Set((stats?.OriginSeries ?? []).map((row) => row.Domain))));
  const originColors = $derived(originKeys.map((_, index) => chartPalette[index % chartPalette.length]));
  const originChartData = $derived.by(() => {
    if (!stats?.OriginSeries?.length) return [];
    return groupStackData(
      stats.OriginSeries.map((row) => ({ Period: row.Period, Domain: row.Domain, value: row.Count })),
      { xKey: 'Period', stackBy: 'Domain' },
    );
  });
  const hasOriginSeries = $derived((stats?.OriginSeries ?? []).some((row) => row.Count > 0));

  const weekRows = $derived<WeekRow[]>((weekStats?.PathSeries ?? []).map((row) => ({ Period: row.Period, Path: row.Path, value: row.Count })));
  const weekSeries = $derived<WeekSeriesItem[]>(
    colorKeys.map((key, index) => ({
      key,
      label: key,
      value: (row: WeekRow) => row.value,
      data: weekRows.filter((row) => row.Path === key),
      color: keyColors[index],
    })),
  );

  const total = $derived(stats?.Total ?? 0);
  const topPath = $derived(stats?.TopPaths[0] ?? null);
  const uniquePages = $derived(stats?.UniquePaths ?? 0);
  const hasData = $derived(Boolean(stats) && stats!.Total > 0);
  const devices = $derived(stats?.Devices ?? []);
  const deviceTotal = $derived(devices.reduce((sum, device) => sum + device.Count, 0));

  function formatCount(value: number): string {
    return new Intl.NumberFormat('de-AT').format(value);
  }

  function formatPercent(value: number): string {
    return new Intl.NumberFormat('de-AT', { style: 'percent', maximumFractionDigits: 1 }).format(value);
  }

  function formatWeekLabel(period: string): string {
    const [year, month, day] = period.split('-');
    return `Woche ab ${day}.${month}.${year}`;
  }

  function formatDayLabel(period: string): string {
    const [year, month, day] = period.split('-');
    return `${day}.${month}.${year}`;
  }

  async function loadWeek(period: string) {
    activeController?.abort();
    const controller = new AbortController();
    activeController = controller;
    weekLoading = true;
    weekError = '';
    try {
      const res = await fetch(`/api/pageviews/stats?days=${days}&week=${period}`, { signal: controller.signal });
      if (!res.ok) throw new Error(`Failed to load week stats (${res.status})`);
      if (controller.signal.aborted) return;
      weekStats = await res.json();
    } catch (e) {
      if (controller.signal.aborted) return;
      console.error(e);
      weekError = 'Wochenstatistik konnte nicht geladen werden.';
    } finally {
      if (!controller.signal.aborted) weekLoading = false;
    }
  }

  function openWeek(period: string) {
    if (selectedWeek === period) return;
    selectedWeek = period;
    loadWeek(period);
  }

  function closeWeek() {
    activeController?.abort();
    selectedWeek = null;
    weekStats = null;
    weekLoading = false;
    weekError = '';
  }

  async function load() {
    activeController?.abort();
    const controller = new AbortController();
    activeController = controller;
    loading = true;
    error = '';
    try {
      const res = await fetch(`/api/pageviews/stats?days=${days}`, { signal: controller.signal });
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

      <div class="period-toggle" role="group" aria-label="Zeitraum wählen">
        {#each periods as period}
          <button
            type="button"
            class="period-button"
            class:is-active={days === period.days}
            aria-pressed={days === period.days}
            onclick={() => {
              days = period.days;
              closeWeek();
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

        {#if !selectedWeek}
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
                  <Bars strokeWidth={1} onBarClick={(e, { data }) => data?.Period && openWeek(data.Period)} />
                  <Highlight
                    area
                    onAreaClick={(e, { data }) => data?.Period && openWeek(data.Period)}
                  />
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
        {:else}
          <div class="week-zoom-head">
            <button type="button" class="zoom-back" onclick={closeWeek}>Zurück zur Wochenübersicht</button>
            <h3 class="device-title">{formatWeekLabel(selectedWeek)} — Aufrufe pro Tag</h3>
          </div>

          {#if weekError}
            <p class="error" role="alert">{weekError}</p>
          {/if}

          {#if weekLoading}
            <div class="chart-loading">
              <p class="loading-text">Lade Daten...</p>
            </div>
          {:else if weekStats && weekStats.Total > 0}
            <div class="chart-wrap">
              <Chart
                data={weekRows}
                x="Period"
                xScale={scalePoint().padding(0.3)}
                y="value"
                yBaseline={0}
                yNice
                series={weekSeries}
                seriesLayout="stack"
                padding={{ left: 32, bottom: 20, top: 8 }}
                tooltipContext={{ mode: 'band' }}
                height={300}
              >
                {#snippet children({ context })}
                  <Layer>
                    <Axis placement="left" grid rule />
                    <Axis placement="bottom" rule />
                    {#each context.series.visibleSeries as s (s.key)}
                      <Area seriesKey={s.key} fillOpacity={0.35} line />
                    {/each}
                    <Highlight area />
                  </Layer>

                  <Tooltip.Root>
                    {#snippet children({ data })}
                      <Tooltip.Header>{formatDayLabel(data.Period)}</Tooltip.Header>
                      <Tooltip.List>
                        {#each data.series as item (item.key)}
                          <Tooltip.Item
                            label={item.label}
                            value={item.value}
                            color={item.color}
                            format="integer"
                            valueAlign="right"
                          />
                        {/each}
                        <Tooltip.Separator />
                        <Tooltip.Item
                          label="Gesamt"
                          value={sum([...data.series], (d: { value?: number }) => d.value ?? 0)}
                          format="integer"
                          valueAlign="right"
                        />
                      </Tooltip.List>
                    {/snippet}
                  </Tooltip.Root>
                {/snippet}
              </Chart>
            </div>
          {:else}
            <p class="chart-message">Keine Seitenaufrufe in dieser Woche.</p>
          {/if}
        {/if}

        <div class="table-wrap">
          <table class="pageview-table">
            <thead>
              <tr>
                <th scope="col">Seite</th>
                <th scope="col" class="table-count">Aufrufe</th>
              </tr>
            </thead>
            <tbody>
              {#each (selectedWeek ? weekStats?.TopPaths : stats?.TopPaths) ?? [] as path}
                <tr>
                  <th scope="row">{path.Path}</th>
                  <td class="table-count">{formatCount(path.Count)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>

        {#if hasDeviceSeries}
          <section class="device-chart" aria-labelledby="device-chart-title">
            <h3 id="device-chart-title" class="device-title">Gerätekategorien nach Woche</h3>
            <div class="chart-legend" aria-hidden="true">
              {#each deviceCategories as category, index}
                <span class="legend-item">
                  <span class="legend-swatch" style="background-color: {deviceColors[index]}"></span>
                  {category}
                </span>
              {/each}
            </div>

            <div class="chart-wrap">
              <Chart
                data={deviceChartData}
                x="Period"
                xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
                y="values"
                yNice
                c="Category"
                cDomain={deviceCategories}
                cRange={deviceColors}
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
                            label={item.Category}
                            value={item.value}
                            color={context.cScale?.(item.Category)}
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

            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">Woche</th>
                  <th scope="col">Gerätekategorie</th>
                  <th scope="col">Aufrufe</th>
                </tr>
              </thead>
              <tbody>
                {#each stats?.DeviceSeries ?? [] as row}
                  <tr>
                    <th scope="row">{formatWeekLabel(row.Period)}</th>
                    <td>{row.Category}</td>
                    <td>{formatCount(row.Count)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
        {/if}

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

        {#if hasOriginSeries}
          <section class="device-chart" aria-labelledby="origin-chart-title">
            <h3 id="origin-chart-title" class="device-title">Herkunftsdomains nach Woche</h3>
            <div class="chart-legend" aria-hidden="true">
              {#each originKeys as domain, index}
                <span class="legend-item">
                  <span class="legend-swatch" style="background-color: {originColors[index]}"></span>
                  {domain}
                </span>
              {/each}
            </div>

            <div class="chart-wrap">
              <Chart
                data={originChartData}
                x="Period"
                xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
                y="values"
                yNice
                c="Domain"
                cDomain={originKeys}
                cRange={originColors}
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
                            label={item.Domain}
                            value={item.value}
                            color={context.cScale?.(item.Domain)}
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

            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">Woche</th>
                  <th scope="col">Herkunftsdomain</th>
                  <th scope="col">Aufrufe</th>
                </tr>
              </thead>
              <tbody>
                {#each stats?.OriginSeries ?? [] as row}
                  <tr>
                    <th scope="row">{formatWeekLabel(row.Period)}</th>
                    <td>{row.Domain}</td>
                    <td>{formatCount(row.Count)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
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

  .device-chart {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
  }

  .device-title {
    margin: 0;
    font-size: 1.1rem;
  }

  .week-zoom-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    flex-wrap: wrap;
  }

  .zoom-back {
    border: 1px solid rgba(0, 32, 73, 0.15);
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    padding: 0.5rem 1rem;
    font-family: inherit;
    font-size: 0.9rem;
    font-weight: 600;
    border-radius: 0.5rem;
    cursor: pointer;
  }

  .zoom-back:hover {
    background-color: var(--himmelblau);
  }

  .zoom-back:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: -2px;
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
