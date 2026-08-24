<script lang="ts">
  import {
    AreaChart,
    Axis,
    Bars,
    Chart,
    Highlight,
    Layer,
    LineChart,
    Tooltip,
    groupStackData,
    type ChartState,
  } from 'layerchart';
  import { scaleBand } from 'd3-scale';
  import { sum } from 'd3-array';

  type SeriesRow = { Period: string; Group: string; Count: number };
  type StackItem = { Group: string; value: number };
  type ChartType = 'bars-stacked' | 'bars-grouped' | 'line' | 'area';

  interface Props {
    rows: SeriesRow[];
    chartType: ChartType;
    formatTooltipLabel: (period: string) => string;
    formatAxisLabel: (period: string) => string;
    context?: ChartState<any, any, any> | undefined;
    ontransform?: (details: { scale: number; translate: { x: number; y: number } }) => void;
  }

  const chartPalette = [
    'var(--weidegruen)',
    'var(--backstein)',
    'var(--himmelblau)',
    'var(--taubenblau)',
    '#b3822a',
    '#5f6b8a',
    '#8a6a9a',
  ];
  const maxZoomScale = 56;

  let {
    rows,
    chartType,
    formatTooltipLabel,
    formatAxisLabel,
    context = $bindable(),
    ontransform,
  }: Props = $props();

  const colorKeys = $derived(Array.from(new Set(rows.map((row) => row.Group))));
  const keyColors = $derived(colorKeys.map((_, index) => chartPalette[index % chartPalette.length]));
  const groupedSeries = $derived(colorKeys.map((key, index) => ({ key, color: keyColors[index] })));

  const stackedData = $derived.by(() =>
    rows.length
      ? groupStackData(
          rows.map((row) => ({ Period: row.Period, Group: row.Group, value: row.Count })),
          { xKey: 'Period', stackBy: 'Group' },
        )
      : [],
  );

  const groupedData = $derived.by(() => {
    if (!rows.length) return [];
    const periods = Array.from(new Set(rows.map((row) => row.Period)));
    return periods.map((period) => {
      const rowOut: Record<string, string | number> = { Period: period };
      for (const key of colorKeys) {
        rowOut[key] = rows.find((r) => r.Period === period && r.Group === key)?.Count ?? 0;
      }
      return rowOut;
    });
  });

  function handleTransform(details: { scale: number; translate: { x: number; y: number } }) {
    ontransform?.(details);
  }
</script>

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
        <Tooltip.Header>{formatTooltipLabel(data.Period)}</Tooltip.Header>
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
      bind:context={context}
      brush={{ axis: 'x', minExtent: { x: 1 } }}
      transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale', scaleExtent: [1, maxZoomScale], scrollActivationKey: 'control' }}
      onTransform={handleTransform}
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
            <Tooltip.Header>{formatTooltipLabel(data.Period)}</Tooltip.Header>
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
      bind:context={context}
      brush={{ axis: 'x', minExtent: { x: 1 } }}
      transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale', scaleExtent: [1, maxZoomScale], scrollActivationKey: 'control' }}
      onTransform={handleTransform}
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
            <Tooltip.Header>{formatTooltipLabel(data.Period)}</Tooltip.Header>
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
      bind:context={context}
      brush={{ axis: 'x', minExtent: { x: 1 } }}
      transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale', scaleExtent: [1, maxZoomScale], scrollActivationKey: 'control' }}
      onTransform={handleTransform}
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
      bind:context={context}
      brush={{ axis: 'x', minExtent: { x: 1 } }}
      transform={{ mode: 'domain', axis: 'x', scrollMode: 'scale', scaleExtent: [1, maxZoomScale], scrollActivationKey: 'control' }}
      onTransform={handleTransform}
      tooltip={explorerTooltip}
      props={{
        area: { line: { strokeWidth: 2 } },
        xAxis: { format: formatAxisLabel },
      }}
    />
  {/if}
</div>

<style>
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
</style>
