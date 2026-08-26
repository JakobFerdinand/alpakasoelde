<script lang="ts">
  import { onMount } from 'svelte';
  import { ArrowLeft, TimerOff, X } from '@lucide/svelte';
  import { formatDuration, formatTimestamp } from '../utils/formatters';

  type SessionSummary = {
    SessionId: string;
    VisitorId: string | null;
    StartedAt: string;
    LastSeenAt: string;
    PageViews: number;
    DurationSeconds: number;
    EntryPath: string;
    ExitPath: string;
    EntryReferrerHost: string | null;
    DeviceCategory: string;
  };

  type SessionEvent = {
    TimestampUtc: string;
    Path: string;
    ReferrerHost: string | null;
    NavigationType: string | null;
    DeviceCategory: string;
    DwellSeconds: number | null;
  };

  type SessionListResult = {
    WindowDays: number;
    Sessions: SessionSummary[];
    Truncated: boolean;
    UngroupedPageViews: number;
  };

  type SessionDetailResult = {
    Summary: SessionSummary;
    Events: SessionEvent[];
  };

  type Period = '7' | '28' | '90' | '180';
  type MinPages = 1 | 2;

  type TraceRow =
    | { Kind: 'event'; Event: SessionEvent; Index: number; OffsetPct: number; WidthPct: number; IsLast: boolean }
    | { Kind: 'gap'; Label: string };

  const periods: { label: string; value: Period }[] = [
    { label: '7 Tage', value: '7' },
    { label: '28 Tage', value: '28' },
    { label: '90 Tage', value: '90' },
    { label: '180 Tage', value: '180' },
  ];

  const bounceFilters: { label: string; value: MinPages }[] = [
    { label: 'Alle', value: 1 },
    { label: 'mind. 2 Seiten', value: 2 },
  ];

  const columnCount = 7;
  const gapThresholdMs = 30 * 60 * 1000;

  let activeController: AbortController | null = null;
  let detailController: AbortController | null = null;

  let selectedSessionId = $state<string | null>(new URLSearchParams(window.location.search).get('id'));
  let sessions = $state<SessionListResult | null>(null);
  let detail = $state<SessionDetailResult | null>(null);
  let period = $state<Period>('28');
  let minPages = $state<MinPages>(1);
  let visitorFilter = $state<string | null>(null);
  let loading = $state(true);
  let loadingDetail = $state(false);
  let error = $state('');
  let errorDetail = $state('');

  const sessionList = $derived(sessions?.Sessions ?? []);
  const sessionCount = $derived(sessionList.length);
  const avgPages = $derived(
    sessionCount === 0 ? 0 : sessionList.reduce((acc, s) => acc + s.PageViews, 0) / sessionCount,
  );
  const avgDuration = $derived(
    sessionCount === 0 ? 0 : sessionList.reduce((acc, s) => acc + s.DurationSeconds, 0) / sessionCount,
  );
  const bounceShare = $derived(
    sessionCount === 0 ? 0 : sessionList.filter((s) => s.PageViews === 1).length / sessionCount,
  );
  const resultSummary = $derived(sessionCount === 0 ? '' : `${formatNumber(sessionCount)} Sitzungen`);

  const traceEvents = $derived(detail?.Events ?? []);
  const traceRows = $derived.by(() => {
    const rows: TraceRow[] = [];
    if (traceEvents.length === 0) return rows;
    const startMs = Date.parse(traceEvents[0].TimestampUtc);
    const endMs = Date.parse(traceEvents[traceEvents.length - 1].TimestampUtc);
    const spanMs = Math.max(endMs - startMs, 0);
    for (let i = 0; i < traceEvents.length; i++) {
      const current = traceEvents[i];
      if (i > 0) {
        const gapMs = Date.parse(current.TimestampUtc) - Date.parse(traceEvents[i - 1].TimestampUtc);
        if (gapMs > gapThresholdMs) {
          rows.push({ Kind: 'gap', Label: formatDuration(gapMs / 1000) });
        }
      }
      const isLast = i === traceEvents.length - 1;
      const offsetPct = spanMs > 0 ? ((Date.parse(current.TimestampUtc) - startMs) / spanMs) * 100 : 0;
      const widthPct =
        !isLast && current.DwellSeconds !== null && spanMs > 0
          ? Math.max(((current.DwellSeconds as number) / (spanMs / 1000)) * 100, 2)
          : 0;
      rows.push({ Kind: 'event', Event: current, Index: i, OffsetPct: offsetPct, WidthPct: widthPct, IsLast: isLast });
    }
    return rows;
  });

  function formatNumber(value: number): string {
    return new Intl.NumberFormat('de-AT').format(value);
  }

  function formatDecimal(value: number): string {
    return new Intl.NumberFormat('de-AT', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);
  }

  function formatPercent(value: number): string {
    return new Intl.NumberFormat('de-AT', { style: 'percent', maximumFractionDigits: 1 }).format(value);
  }

  function shortId(value: string | null): string {
    return value ? value.slice(0, 8) : '—';
  }

  function referrerOrDirect(referrerHost: string | null): string {
    return referrerHost || 'direkt';
  }

  function navigationText(type: string | null): string {
    if (type === 'navigate') return 'Aufruf';
    if (type === 'reload') return 'Neu geladen';
    if (type === 'back_forward') return 'Vor/Zurück';
    return type ?? 'Ohne Angabe';
  }

  function barClass(type: string | null): string {
    switch (type) {
      case 'navigate':
        return 'bar-navigate';
      case 'reload':
        return 'bar-reload';
      case 'back_forward':
        return 'bar-back-forward';
      default:
        return 'bar-unknown';
    }
  }

  async function loadSessions() {
    activeController?.abort();
    const controller = new AbortController();
    activeController = controller;
    loading = true;
    error = '';
    try {
      const params = new URLSearchParams({ days: period, minPages: String(minPages) });
      if (visitorFilter) {
        params.set('visitor', visitorFilter);
      }
      const res = await fetch(`/api/pageviews/sessions?${params}`, { signal: controller.signal });
      if (!res.ok) throw new Error(`Failed to load sessions (${res.status})`);
      sessions = await res.json();
    } catch (e) {
      if (controller.signal.aborted) return;
      console.error(e);
      error = 'Sitzungen konnten nicht geladen werden.';
    } finally {
      if (!controller.signal.aborted) loading = false;
    }
  }

  async function loadDetail(id: string) {
    detailController?.abort();
    const controller = new AbortController();
    detailController = controller;
    loadingDetail = true;
    errorDetail = '';
    detail = null;
    try {
      const res = await fetch(`/api/pageviews/sessions/${encodeURIComponent(id)}`, { signal: controller.signal });
      if (res.status === 404) {
        errorDetail = 'Sitzung nicht gefunden.';
        return;
      }
      if (!res.ok) throw new Error(`Failed to load session (${res.status})`);
      detail = await res.json();
    } catch (e) {
      if (controller.signal.aborted) return;
      console.error(e);
      errorDetail = 'Sitzung konnte nicht geladen werden.';
    } finally {
      if (!controller.signal.aborted) loadingDetail = false;
    }
  }

  async function openSession(id: string) {
    selectedSessionId = id;
    history.replaceState(null, '', `/sitzungen?id=${encodeURIComponent(id)}`);
    await loadDetail(id);
  }

  function backToList() {
    selectedSessionId = null;
    detail = null;
    errorDetail = '';
    history.replaceState(null, '', '/sitzungen');
  }

  function filterByVisitor(visitorId: string) {
    visitorFilter = visitorId;
    backToList();
    loadSessions();
  }

  function clearVisitorFilter() {
    visitorFilter = null;
    loadSessions();
  }

  onMount(() => {
    loadSessions();
    if (selectedSessionId) {
      loadDetail(selectedSessionId);
    }
    return () => {
      activeController?.abort();
      detailController?.abort();
    };
  });
</script>

<section class="dashboard-sessions section">
  <div class="container">
    <h2>Sitzungen</h2>

    {#if selectedSessionId}
      <button type="button" class="back-link back-button" onclick={backToList}>
        <ArrowLeft class="back-icon" aria-hidden="true" />
        Zurück zur Liste
      </button>
      {#if errorDetail}
        <p class="error" role="alert">{errorDetail}</p>
      {:else if loadingDetail || !detail}
        <div class="detail-loading">
          <p class="loading-text">Lade Sitzung...</p>
        </div>
      {:else if detail}
        {@const summary = detail.Summary}
        <div class="card session-header">
          <dl class="summary-chips">
            <div class="chip">
              <dt>Seiten</dt>
              <dd>{formatNumber(summary.PageViews)}</dd>
            </div>
            <div class="chip">
              <dt>Dauer</dt>
              <dd>{formatDuration(summary.DurationSeconds)}</dd>
            </div>
            <div class="chip">
              <dt>Einstieg</dt>
              <dd>{summary.EntryPath}</dd>
            </div>
            <div class="chip">
              <dt>Ausstieg</dt>
              <dd>{summary.ExitPath}</dd>
            </div>
            <div class="chip">
              <dt>Referrer</dt>
              <dd>{referrerOrDirect(summary.EntryReferrerHost)}</dd>
            </div>
            <div class="chip">
              <dt>Gerät</dt>
              <dd>{summary.DeviceCategory}</dd>
            </div>
            <div class="chip">
              <dt>Besucher</dt>
              <dd title={summary.VisitorId ?? undefined}>{shortId(summary.VisitorId)}</dd>
            </div>
          </dl>
          {#if summary.VisitorId}
            {@const visitorId = summary.VisitorId}
            <button type="button" class="ghost-button visitor-button" onclick={() => filterByVisitor(visitorId)}>
              Sitzungen dieses Besuchers
            </button>
          {/if}
        </div>

        <section class="waterfall-section" aria-labelledby="waterfall-title">
          <h3 id="waterfall-title">Zeitlicher Verlauf</h3>
          <ul class="trace-legend" aria-hidden="true">
            <li><span class="legend-swatch swatch-navigate"></span>Aufruf</li>
            <li><span class="legend-swatch swatch-reload"></span>Neu geladen</li>
            <li><span class="legend-swatch swatch-back-forward"></span>Vor/Zurück</li>
            <li><span class="legend-swatch swatch-unknown"></span>Ohne Angabe</li>
          </ul>
          <div class="trace-chart">
            {#each traceRows as row}
              {#if row.Kind === 'gap'}
                <div class="trace-gap" role="separator">
                  <TimerOff class="gap-icon" aria-hidden="true" />
                  <span>Lücke: {row.Label}</span>
                </div>
              {:else}
                <div
                  class="trace-row"
                  aria-label={`Schritt ${row.Index + 1}: ${row.Event.Path} (${navigationText(row.Event.NavigationType)})`}
                >
                  <div class="trace-gutter">
                    <span class="trace-index">{row.Index + 1}</span>
                    <span class="trace-time">{formatTimestamp(row.Event.TimestampUtc)}</span>
                  </div>
                  <div class="trace-track">
                    {#if row.IsLast}
                      <span class="trace-bar-marker {barClass(row.Event.NavigationType)}" style={`left: ${row.OffsetPct}%`}></span>
                    {:else}
                      <span class="trace-bar {barClass(row.Event.NavigationType)}" style={`left: ${row.OffsetPct}%; width: ${row.WidthPct}%`}></span>
                    {/if}
                  </div>
                  <div class="trace-dwell">
                    {row.IsLast ? 'offen' : formatDuration(row.Event.DwellSeconds)}
                  </div>
                </div>
              {/if}
            {/each}
          </div>
        </section>

        <div class="table-wrapper">
          <table class="data-table steps-table">
            <thead>
              <tr>
                <th scope="col">#</th>
                <th scope="col">Zeitpunkt</th>
                <th scope="col">Pfad</th>
                <th scope="col">Typ</th>
                <th scope="col">Verweildauer</th>
                <th scope="col">Referrer</th>
                <th scope="col">Gerät</th>
              </tr>
            </thead>
            <tbody>
              {#each traceEvents as event, i (i)}
                <tr>
                  <td>{i + 1}</td>
                  <td class="nowrap">{formatTimestamp(event.TimestampUtc)}</td>
                  <td>{event.Path}</td>
                  <td>{event.NavigationType === null ? '–' : navigationText(event.NavigationType)}</td>
                  <td class="nowrap">{i === traceEvents.length - 1 ? 'offen' : formatDuration(event.DwellSeconds)}</td>
                  <td>{referrerOrDirect(event.ReferrerHost)}</td>
                  <td>{event.DeviceCategory}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
    {:else}
      <div class="toolbar">
        <div class="segmented" role="group" aria-label="Zeitraum wählen">
          {#each periods as option}
            <button
              type="button"
              class="segment-button"
              class:is-active={period === option.value}
              aria-pressed={period === option.value}
              onclick={() => {
                period = option.value;
                loadSessions();
              }}
            >
              {option.label}
            </button>
          {/each}
        </div>
        <div class="segmented" role="group" aria-label="Sitzungen nach Seitenzahl filtern">
          {#each bounceFilters as option}
            <button
              type="button"
              class="segment-button"
              class:is-active={minPages === option.value}
              aria-pressed={minPages === option.value}
              onclick={() => {
                minPages = option.value;
                loadSessions();
              }}
            >
              {option.label}
            </button>
          {/each}
        </div>
        <p class="result-summary" aria-live="polite">{resultSummary}</p>
      </div>

      {#if visitorFilter}
        <div class="active-filter">
          <span class="filter-chip">
            Besucher: {visitorFilter.slice(0, 8)}…
            <button type="button" class="chip-clear" aria-label="Besucherfilter entfernen" onclick={clearVisitorFilter}>
              <X class="chip-clear-icon" aria-hidden="true" />
            </button>
          </span>
        </div>
      {/if}

      <div class="kpi-strip">
        <div class="kpi-card">
          <span class="kpi-value">{formatNumber(sessionCount)}</span>
          {#if sessions?.Truncated}
            <span class="kpi-hint">weitere vorhanden</span>
          {/if}
          <span class="kpi-label">Anzahl Sitzungen</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-value">{formatDecimal(avgPages)}</span>
          <span class="kpi-label">Ø Seiten/Sitzung</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-value">{formatDuration(avgDuration)}</span>
          <span class="kpi-label">Ø Dauer</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-value">{formatPercent(bounceShare)}</span>
          <span class="kpi-label">Bounce-Anteil</span>
        </div>
      </div>

      <div class="table-wrapper">
        <table class="data-table session-table">
          <thead>
            <tr>
              <th scope="col">Zuletzt aktiv</th>
              <th scope="col">Dauer</th>
              <th scope="col">Seiten</th>
              <th scope="col">Ablauf</th>
              <th scope="col">Referrer</th>
              <th scope="col">Gerät</th>
              <th scope="col">Besucher</th>
            </tr>
          </thead>
          <tbody>
            {#if loading}
              <tr>
                <td colspan={columnCount}>Lade Sitzungen...</td>
              </tr>
            {:else if error}
              <tr>
                <td colspan={columnCount} class="error"><span role="alert">{error}</span></td>
              </tr>
            {:else if sessionCount === 0}
              <tr>
                <td colspan={columnCount}>Keine Sitzungen im gewählten Zeitraum.</td>
              </tr>
            {:else}
              {#each sessionList as session (session.SessionId)}
                <tr
                  class="session-row"
                  tabindex="0"
                  role="link"
                  aria-label={`Sitzung vom ${formatTimestamp(session.StartedAt)} anzeigen`}
                  onclick={() => openSession(session.SessionId)}
                  onkeydown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault();
                      openSession(session.SessionId);
                    }
                  }}
                >
                  <td class="nowrap">{formatTimestamp(session.LastSeenAt)}</td>
                  <td class="nowrap">{formatDuration(session.DurationSeconds)}</td>
                  <td>{session.PageViews}</td>
                  <td class="nowrap">{session.EntryPath} → {session.ExitPath}</td>
                  <td>{referrerOrDirect(session.EntryReferrerHost)}</td>
                  <td>{session.DeviceCategory}</td>
                  <td class="nowrap" title={session.VisitorId ?? undefined}>{shortId(session.VisitorId)}</td>
                </tr>
              {/each}
            {/if}
          </tbody>
        </table>
      </div>

      {#if !loading && !error && sessions && sessions.UngroupedPageViews > 0}
        <p class="ungrouped-note">
          {formatNumber(sessions.UngroupedPageViews)} Seitenaufrufe ohne Sitzungs-ID nicht berücksichtigt.
        </p>
      {/if}
    {/if}
  </div>
</section>

<style>
  .dashboard-sessions {
    background-color: var(--auwasser);
    color: var(--taubenblau);
  }

  .toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    margin-bottom: 0.75rem;
  }

  .segmented {
    display: inline-flex;
    border-radius: 0.5rem;
    overflow: hidden;
    border: 1px solid rgba(0, 32, 73, 0.15);
    align-self: flex-start;
  }

  .segment-button {
    border: none;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    padding: 0.5rem 1rem;
    font-family: inherit;
    font-size: 0.9rem;
    font-weight: 600;
    cursor: pointer;
  }

  .segment-button + .segment-button {
    border-left: 1px solid rgba(0, 32, 73, 0.15);
  }

  .segment-button:hover {
    background-color: var(--himmelblau);
  }

  .segment-button.is-active {
    background-color: var(--weidegruen);
    color: var(--schurwolle);
  }

  .segment-button:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: -2px;
  }

  .result-summary {
    margin: 0;
    font-weight: 600;
  }

  .active-filter {
    display: flex;
    margin-bottom: 0.75rem;
  }

  .filter-chip {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    padding: 0.3rem 0.6rem;
    border-radius: 999px;
    background-color: var(--weidegruen);
    color: var(--schurwolle);
    font-size: 0.85rem;
    font-weight: 600;
  }

  .chip-clear {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border: none;
    background: none;
    color: inherit;
    cursor: pointer;
    padding: 0.1rem;
    border-radius: 50%;
  }

  .chip-clear:hover,
  .chip-clear:focus-visible {
    background-color: rgba(251, 247, 237, 0.25);
  }

  .chip-clear:focus-visible {
    outline: 2px solid var(--schurwolle);
    outline-offset: 1px;
  }

  :global(.chip-clear-icon) {
    width: 0.9rem;
    height: 0.9rem;
  }

  .kpi-strip {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    gap: 0.75rem;
    margin-bottom: 0.5rem;
  }

  .kpi-card {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.15rem;
    padding: 0.85rem 1rem;
    border-radius: 0.5rem;
    background-color: rgba(255, 255, 255, 0.55);
    color: var(--taubenblau);
    border: 1px solid rgba(0, 32, 73, 0.12);
  }

  .kpi-value {
    font-size: 1.35rem;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
  }

  .kpi-hint {
    font-size: 0.78rem;
    font-weight: 600;
    color: var(--backstein);
  }

  .kpi-label {
    font-size: 0.85rem;
    font-weight: 600;
  }

  .session-row {
    cursor: pointer;
  }

  .session-row:focus-visible {
    outline: 2px solid var(--weidegruen);
    outline-offset: -2px;
  }

  .ungrouped-note {
    margin: 0.25rem 0 0;
    font-size: 0.85rem;
    color: rgba(0, 32, 73, 0.65);
  }

  .nowrap {
    white-space: nowrap;
  }

  .detail-loading {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 3rem 0;
  }

  .back-button {
    border: none;
    background: none;
    padding: 0;
    font-family: inherit;
    font-size: 1rem;
    cursor: pointer;
  }

  .back-button:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: 2px;
  }

  :global(.back-icon) {
    width: 1.1rem;
    height: 1.1rem;
  }

  .session-header {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    margin-bottom: 1.25rem;
  }

  .summary-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin: 0;
  }

  .chip {
    display: inline-flex;
    flex-direction: column;
    gap: 0.1rem;
    padding: 0.45rem 0.7rem;
    border-radius: 0.5rem;
    background-color: rgba(141, 165, 211, 0.18);
    border: 1px solid rgba(0, 32, 73, 0.1);
    min-width: 4.5rem;
  }

  .chip dt {
    font-size: 0.72rem;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
    color: rgba(0, 32, 73, 0.65);
  }

  .chip dd {
    margin: 0;
    font-weight: 700;
    overflow-wrap: anywhere;
  }

  .visitor-button {
    align-self: flex-start;
  }

  .waterfall-section {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    padding: 1rem;
    border: 1px solid rgba(0, 32, 73, 0.12);
    border-radius: 0.5rem;
    background-color: rgba(255, 255, 255, 0.55);
    margin-bottom: 1rem;
  }

  .waterfall-section h3 {
    margin: 0;
    font-size: 1.1rem;
  }

  .trace-legend {
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    list-style: none;
    margin: 0;
    padding: 0;
    font-size: 0.85rem;
    font-weight: 600;
  }

  .trace-legend li {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
  }

  .legend-swatch {
    display: inline-block;
    width: 1rem;
    height: 0.5rem;
    border-radius: 999px;
  }

  .swatch-navigate,
  .bar-navigate {
    background-color: var(--himmelblau);
  }

  .swatch-reload,
  .bar-reload {
    background-color: var(--bluetenhonig);
  }

  .swatch-back-forward,
  .bar-back-forward {
    background-color: var(--backstein);
  }

  .swatch-unknown,
  .bar-unknown {
    background-color: #9aa5b1;
  }

  .trace-chart {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }

  .trace-row {
    display: grid;
    grid-template-columns: 13rem 1fr 5.5rem;
    align-items: center;
    gap: 0.75rem;
  }

  .trace-gutter {
    display: flex;
    align-items: baseline;
    gap: 0.5rem;
    font-size: 0.8rem;
    min-width: 0;
  }

  .trace-index {
    font-weight: 700;
    font-variant-numeric: tabular-nums;
  }

  .trace-time {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .trace-track {
    position: relative;
    height: 1.5rem;
    border-radius: 0.375rem;
    background-color: rgba(0, 32, 73, 0.06);
    overflow: hidden;
  }

  .trace-bar {
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    height: 0.7rem;
    border-radius: 999px;
  }

  .trace-bar-marker {
    position: absolute;
    top: 50%;
    transform: translateY(-50%);
    width: 0.4rem;
    height: 1rem;
    border-radius: 0.2rem;
  }

  .trace-dwell {
    text-align: right;
    font-size: 0.85rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  }

  .trace-gap {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    margin-left: calc(13rem + 0.75rem);
    padding-top: 0.3rem;
    border-top: 1px dashed rgba(0, 32, 73, 0.4);
    color: rgba(0, 32, 73, 0.65);
    font-size: 0.8rem;
    font-style: italic;
  }

  :global(.gap-icon) {
    width: 0.95rem;
    height: 0.95rem;
    flex-shrink: 0;
  }

  @media (max-width: 768px) {
    .waterfall-section {
      display: none;
    }

    .kpi-strip {
      grid-template-columns: repeat(2, 1fr);
    }
  }
</style>
