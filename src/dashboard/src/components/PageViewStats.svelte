<script lang="ts">
  import { onMount } from 'svelte';
  import type { ChartState } from 'layerchart';
  import { BarChart3, Eye, Files, Repeat, Users, ZoomOut } from '@lucide/svelte';
  import PageViewSeriesChart from './PageViewSeriesChart.svelte';

  type PathCount = { Path: string; Count: number };
  type DeviceCount = { Category: string; Count: number };
  type OriginCount = { Domain: string; Count: number };
  type Bucket = { Period: string; Group: string | null; Count: number };
  type AudienceBucket = { Period: string; Visitors: number; Sessions: number };
  type NavigationCount = { Type: string; Count: number };
  type Granularity = 'week' | 'day' | 'hour';
  type GroupBy = 'path' | 'device' | 'origin';
  type ChartType = 'bars-stacked' | 'bars-grouped' | 'line' | 'area';
  type TransformDetails = { scale: number; translate: { x: number; y: number } };
  type StatsResult = {
    Total: number;
    UniquePaths: number;
    TopPaths: PathCount[];
    Devices: DeviceCount[];
    Origins: OriginCount[];
    Series: Bucket[];
    Sessions: number;
    Visitors: number;
    Navigations: NavigationCount[];
    AudienceSeries: AudienceBucket[];
    Granularity: Granularity;
    GroupBy: GroupBy | 'total';
  };

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
  const chartTypes: { label: string; value: ChartType }[] = [
    { label: 'Säulen gestapelt', value: 'bars-stacked' },
    { label: 'Säulen gruppiert', value: 'bars-grouped' },
    { label: 'Linien', value: 'line' },
    { label: 'Fläche', value: 'area' },
  ];

  let activeController: AbortController | null = null;

  let days = $state(28);
  let granularity = $state<Granularity>('week');
  let chartType = $state<ChartType>('bars-stacked');
  let loading = $state(true);
  let error = $state('');
  let pathStats = $state<StatsResult | null>(null);
  let deviceStats = $state<StatsResult | null>(null);
  let originStats = $state<StatsResult | null>(null);
  let pathCtx = $state<ChartState<any, any, any> | undefined>();
  let deviceCtx = $state<ChartState<any, any, any> | undefined>();
  let originCtx = $state<ChartState<any, any, any> | undefined>();
  let audienceCtx = $state<ChartState<any, any, any> | undefined>();
  let zoomed = $state(false);

  const hourDisabled = $derived(days > 28);

  const total = $derived(pathStats?.Total ?? 0);
  const sessions = $derived(pathStats?.Sessions ?? 0);
  const visitors = $derived(pathStats?.Visitors ?? 0);
  const markedShare = $derived.by(() => {
    const marked = (pathStats?.Navigations ?? [])
      .filter((entry) => entry.Type === 'reload' || entry.Type === 'back_forward')
      .reduce((acc, entry) => acc + entry.Count, 0);
    return total > 0 ? marked / total : 0;
  });
  const topPath = $derived(pathStats?.TopPaths[0] ?? null);
  const uniquePages = $derived(pathStats?.UniquePaths ?? 0);
  const hasData = $derived(Boolean(pathStats) && pathStats!.Total > 0);
  const devices = $derived(deviceStats?.Devices ?? []);
  const deviceTotal = $derived(devices.reduce((acc, device) => acc + device.Count, 0));
  const granularityLabel = $derived(
    granularity === 'hour' ? 'Stunde' : granularity === 'day' ? 'Tag' : 'Woche',
  );

  function toRows(stats: StatsResult | null) {
    return (stats?.Series ?? []).map((row) => ({
      Period: row.Period,
      Group: row.Group ?? 'Gesamt',
      Count: row.Count,
    }));
  }
  const pathRows = $derived(toRows(pathStats));
  const deviceRows = $derived(toRows(deviceStats));
  const originRows = $derived(toRows(originStats));
  const audienceRows = $derived(
    (pathStats?.AudienceSeries ?? []).flatMap((entry) => [
      { Period: entry.Period, Group: 'Besucher', Count: entry.Visitors },
      { Period: entry.Period, Group: 'Sitzungen', Count: entry.Sessions },
    ]),
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

  async function fetchStats(groupBy: GroupBy, signal: AbortSignal): Promise<StatsResult> {
    const res = await fetch(
      `/api/pageviews/stats?days=${days}&granularity=${granularity}&groupBy=${groupBy}`,
      { signal },
    );
    if (!res.ok) throw new Error(`Failed to load stats (${res.status})`);
    return res.json();
  }

  function transformApplied(ctx: ChartState<any, any, any>, details: TransformDetails): boolean {
    return (
      ctx.transform.scale === details.scale &&
      ctx.transform.translate.x === details.translate.x &&
      ctx.transform.translate.y === details.translate.y
    );
  }

  function handleTransform(source: ChartState<any, any, any> | undefined, details: TransformDetails) {
    zoomed = details.scale > 1 || details.translate.x !== 0;
    if (!source) return;
    for (const ctx of [pathCtx, deviceCtx, originCtx, audienceCtx]) {
      if (!ctx || ctx === source || transformApplied(ctx, details)) continue;
      ctx.transform.setScale(details.scale, { instant: true });
      ctx.transform.setTranslate(details.translate, { instant: true });
    }
  }

  function resetZoom() {
    zoomed = false;
    for (const ctx of [pathCtx, deviceCtx, originCtx, audienceCtx]) {
      if (!ctx || transformApplied(ctx, { scale: 1, translate: { x: 0, y: 0 } })) continue;
      ctx.transform.reset();
    }
  }

  async function load() {
    activeController?.abort();
    zoomed = false;
    const controller = new AbortController();
    activeController = controller;
    loading = true;
    error = '';
    try {
      const [path, device, origin] = await Promise.all([
        fetchStats('path', controller.signal),
        fetchStats('device', controller.signal),
        fetchStats('origin', controller.signal),
      ]);
      if (controller.signal.aborted) return;
      pathStats = path;
      deviceStats = device;
      originStats = origin;
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

        <div class="period-toggle" role="group" aria-label="Diagrammtyp wählen">
          {#each chartTypes as option}
            <button
              type="button"
              class="period-button"
              class:is-active={chartType === option.value}
              aria-pressed={chartType === option.value}
              onclick={() => {
                chartType = option.value;
                zoomed = false;
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

        {#if zoomed}
          <button
            type="button"
            class="period-button zoom-reset"
            title="Zoom zurücksetzen"
            onclick={resetZoom}
          >
            <ZoomOut class="zoom-reset-icon" aria-hidden="true" />
            Zoom zurücksetzen
          </button>
        {/if}
      </div>

      {#if error}
        <p class="error" role="alert">{error}</p>
      {/if}

      <div class="kpi-row">
        <div class="kpi-tile">
          <BarChart3 class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(total)}</span>
          <span class="kpi-percent">{formatPercent(markedShare)} neu geladen/zurück</span>
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
        <div class="kpi-tile">
          <Users class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(sessions)}</span>
          <span class="kpi-label">Sitzungen</span>
        </div>
        <div class="kpi-tile">
          <Repeat class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(visitors)}</span>
          <span class="kpi-label">Besucher</span>
        </div>
      </div>

      {#if loading}
        <div class="chart-loading">
          <p class="loading-text">Lade Daten...</p>
        </div>
      {:else if !hasData}
        <p class="chart-message">Keine Seitenaufrufe im Zeitraum.</p>
      {:else}
        {#if pathRows.length}
          <section class="chart-section" aria-labelledby="path-chart-title">
            <h3 id="path-chart-title" class="section-title">Seitenaufrufe nach {granularityLabel} und Seite</h3>
            <PageViewSeriesChart
              rows={pathRows}
              {chartType}
              formatTooltipLabel={formatPeriodLabel}
              formatAxisLabel={formatAxisLabel}
              bind:context={pathCtx}
              ontransform={(details) => handleTransform(pathCtx, details)}
            />
            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">{granularityLabel}</th>
                  <th scope="col">Seite</th>
                  <th scope="col">Aufrufe</th>
                </tr>
              </thead>
              <tbody>
                {#each pathRows as row}
                  <tr>
                    <th scope="row">{formatPeriodLabel(row.Period)}</th>
                    <td>{row.Group}</td>
                    <td>{formatCount(row.Count)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
        {/if}

        {#if audienceRows.length}
          <section class="chart-section" aria-labelledby="audience-chart-title">
            <h3 id="audience-chart-title" class="section-title">Besucher und Sitzungen nach {granularityLabel}</h3>
            <PageViewSeriesChart
              rows={audienceRows}
              {chartType}
              formatTooltipLabel={formatPeriodLabel}
              formatAxisLabel={formatAxisLabel}
              bind:context={audienceCtx}
              ontransform={(details) => handleTransform(audienceCtx, details)}
            />
            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">{granularityLabel}</th>
                  <th scope="col">Metrik</th>
                  <th scope="col">Anzahl</th>
                </tr>
              </thead>
              <tbody>
                {#each audienceRows as row}
                  <tr>
                    <th scope="row">{formatPeriodLabel(row.Period)}</th>
                    <td>{row.Group}</td>
                    <td>{formatCount(row.Count)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
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
              {#each pathStats?.TopPaths ?? [] as path}
                <tr>
                  <th scope="row">{path.Path}</th>
                  <td class="table-count">{formatCount(path.Count)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>

        {#if deviceRows.length}
          <section class="chart-section" aria-labelledby="device-chart-title">
            <h3 id="device-chart-title" class="section-title">Gerätekategorien nach {granularityLabel}</h3>
            <PageViewSeriesChart
              rows={deviceRows}
              {chartType}
              formatTooltipLabel={formatPeriodLabel}
              formatAxisLabel={formatAxisLabel}
              bind:context={deviceCtx}
              ontransform={(details) => handleTransform(deviceCtx, details)}
            />
            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">{granularityLabel}</th>
                  <th scope="col">Gerätekategorie</th>
                  <th scope="col">Aufrufe</th>
                </tr>
              </thead>
              <tbody>
                {#each deviceRows as row}
                  <tr>
                    <th scope="row">{formatPeriodLabel(row.Period)}</th>
                    <td>{row.Group}</td>
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

        {#if originRows.length}
          <section class="chart-section" aria-labelledby="origin-chart-title">
            <h3 id="origin-chart-title" class="section-title">Herkunftsdomains nach {granularityLabel}</h3>
            <PageViewSeriesChart
              rows={originRows}
              {chartType}
              formatTooltipLabel={formatPeriodLabel}
              formatAxisLabel={formatAxisLabel}
              bind:context={originCtx}
              ontransform={(details) => handleTransform(originCtx, details)}
            />
            <table class="sr-only-table">
              <thead>
                <tr>
                  <th scope="col">{granularityLabel}</th>
                  <th scope="col">Herkunftsdomain</th>
                  <th scope="col">Aufrufe</th>
                </tr>
              </thead>
              <tbody>
                {#each originRows as row}
                  <tr>
                    <th scope="row">{formatPeriodLabel(row.Period)}</th>
                    <td>{row.Group}</td>
                    <td>{formatCount(row.Count)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
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
              {#each originStats?.Origins ?? [] as origin}
                <tr>
                  <th scope="row">{origin.Domain}</th>
                  <td class="table-count">{formatCount(origin.Count)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
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

  .zoom-reset {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    margin-left: auto;
  }

  :global(.zoom-reset-icon) {
    width: 1rem;
    height: 1rem;
  }

  .kpi-row {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
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

  .chart-section {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .section-title {
    margin: 0;
    font-size: 1.1rem;
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