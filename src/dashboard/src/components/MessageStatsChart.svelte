<script lang="ts">
  import { onMount } from 'svelte';
  import { Axis, Bars, Chart, Highlight, Layer, Tooltip, groupStackData } from 'layerchart';
  import { scaleBand } from 'd3-scale';
  import { sum } from 'd3-array';
  import { Inbox, MailCheck, ShieldAlert } from '@lucide/svelte';

  type PeriodBucket = { Period: string; Spam: number; Legit: number };
  type StatsResult = {
    Total: number;
    Spam: number;
    Legit: number;
    OldCount: number;
    Series: PeriodBucket[];
  };
  type KindRow = { Period: string; kind: 'legit' | 'spam'; value: number };

  const periods = [
    { label: '4 Wochen', days: 28 },
    { label: '3 Monate', days: 90 },
    { label: '6 Monate', days: 180 },
  ];
  const colorKeys = ['legit', 'spam'];
  const keyColors = ['var(--weidegruen)', 'var(--backstein)'];

  let days = $state(28);
  let loading = $state(true);
  let error = $state('');
  let stats = $state<StatsResult | null>(null);

  const chartData = $derived.by(() =>
    stats ? groupStackData(toLongRows(stats.Series), { xKey: 'Period', stackBy: 'kind' }) : [],
  );

  const total = $derived(stats?.Total ?? 0);
  const spam = $derived(stats?.Spam ?? 0);
  const legit = $derived(stats?.Legit ?? 0);
  const spamPct = $derived(total > 0 ? Math.round((spam / total) * 100) : null);
  const legitPct = $derived(total > 0 ? Math.round((legit / total) * 100) : null);
  const hasData = $derived(Boolean(stats) && stats!.Series.length > 0);

  function toLongRows(series: PeriodBucket[]): KindRow[] {
    return series.flatMap((bucket) => [
      { Period: bucket.Period, kind: 'legit' as const, value: bucket.Legit },
      { Period: bucket.Period, kind: 'spam' as const, value: bucket.Spam },
    ]);
  }

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
      const res = await fetch(`/api/messages/stats?days=${days}`);
      if (!res.ok) throw new Error(`Failed to load stats (${res.status})`);
      stats = await res.json();
    } catch (e) {
      console.error(e);
      error = 'Nachrichtenstatistik konnte nicht geladen werden.';
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    load();
  });
</script>

<section class="dashboard-messages section">
  <div class="container">
    <div class="message-stats-card card">
      <div class="stats-header">
        <h2>Nachrichten</h2>
        <a class="all-link" href="/messages">Alle anzeigen</a>
      </div>

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
        <a class="kpi-tile" href="/messages">
          <Inbox class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(total)}</span>
          <span class="kpi-label">Gesamt</span>
        </a>
        <a class="kpi-tile kpi-tile-spam" href="/messages">
          <ShieldAlert class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(spam)}</span>
          {#if spamPct !== null}
            <span class="kpi-percent">{spamPct} %</span>
          {/if}
          <span class="kpi-label">Spam</span>
        </a>
        <a class="kpi-tile kpi-tile-legit" href="/messages">
          <MailCheck class="kpi-icon" aria-hidden="true" />
          <span class="kpi-value">{formatCount(legit)}</span>
          {#if legitPct !== null}
            <span class="kpi-percent">{legitPct} %</span>
          {/if}
          <span class="kpi-label">Legit</span>
        </a>
      </div>

      {#if stats && stats.OldCount > 0}
        <a class="old-chip" href="/messages">
          {formatCount(stats.OldCount)} Nachricht{stats.OldCount === 1 ? '' : 'en'} älter als 6 Monate
        </a>
      {/if}

      {#if hasData}
        <div class="chart-legend" aria-hidden="true">
          <span class="legend-item"><span class="legend-swatch legend-swatch-legit"></span>Legit</span>
          <span class="legend-item"><span class="legend-swatch legend-swatch-spam"></span>Spam</span>
        </div>
      {/if}

      {#if loading}
        <div class="chart-loading">
          <p class="loading-text">Lade Daten...</p>
        </div>
      {:else if !hasData}
        <p class="chart-message">Keine Nachrichten im Zeitraum.</p>
      {:else}
        <div class="chart-wrap">
          <Chart
            data={chartData}
            x="Period"
            xScale={scaleBand().paddingInner(0.4).paddingOuter(0.2)}
            y="values"
            yNice
            c="kind"
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
                        label={item.kind}
                        value={item.value}
                        color={context.cScale?.(item.kind)}
                        format="integer"
                        valueAlign="right"
                      />
                    {/each}
                    <Tooltip.Separator />
                    <Tooltip.Item
                      label="Gesamt"
                      value={sum([...data.data], (d: KindRow) => d.value)}
                      format="integer"
                      valueAlign="right"
                    />
                  </Tooltip.List>
                {/snippet}
              </Tooltip.Root>
            {/snippet}
          </Chart>
        </div>
      {/if}

      <h3 class="sr-only">Nachrichteneingänge nach Woche</h3>
      <table class="sr-only-table">
        <thead>
          <tr>
            <th scope="col">Woche</th>
            <th scope="col">Legit</th>
            <th scope="col">Spam</th>
          </tr>
        </thead>
        <tbody>
          {#each stats?.Series ?? [] as bucket}
            <tr>
              <th scope="row">{formatWeekLabel(bucket.Period)}</th>
              <td>{formatCount(bucket.Legit)}</td>
              <td>{formatCount(bucket.Spam)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </div>
</section>

<style>
  .dashboard-messages {
    background-color: var(--auwasser);
    color: var(--taubenblau);
  }

  .message-stats-card {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
  }

  .stats-header {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    gap: 1rem;
  }

  .stats-header h2 {
    margin: 0;
    font-size: 1.875rem;
  }

  .all-link,
  .old-chip {
    color: var(--taubenblau);
  }

  .all-link:hover,
  .old-chip:hover {
    text-decoration: none;
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
    text-decoration: none;
    border: 1px solid rgba(0, 32, 73, 0.1);
    transition: transform 0.15s ease;
  }

  .kpi-tile:hover,
  .kpi-tile:focus-visible {
    transform: translateY(-2px);
  }

  .kpi-tile-spam {
    border-color: var(--backstein);
  }

  .kpi-tile-legit {
    border-color: var(--weidegruen);
  }

  :global(.kpi-icon) {
    width: 1.25rem;
    height: 1.25rem;
  }

  .kpi-value {
    font-size: 1.5rem;
    font-weight: 700;
  }

  .kpi-percent {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--taubenblau);
  }

  .kpi-tile-spam .kpi-percent {
    color: var(--backstein);
  }

  .kpi-tile-legit .kpi-percent {
    color: var(--weidegruen);
  }

  .kpi-label {
    font-weight: 600;
  }

  .old-chip {
    display: inline-flex;
    align-items: center;
    font-weight: 600;
    background-color: rgba(176, 0, 32, 0.1);
    border: 1px solid rgba(176, 0, 32, 0.35);
    border-radius: 0.5rem;
    padding: 0.5rem 0.9rem;
    text-decoration: none;
    width: fit-content;
  }

  .chart-legend {
    display: flex;
    gap: 1rem;
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

  .legend-swatch-legit {
    background-color: var(--weidegruen);
  }

  .legend-swatch-spam {
    background-color: var(--backstein);
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

    .stats-header {
      flex-direction: column;
      align-items: flex-start;
    }
  }
</style>
