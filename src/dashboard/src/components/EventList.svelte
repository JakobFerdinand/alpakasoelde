<script lang="ts">
  import { onMount } from 'svelte';
  import { CircleEllipsis, Scissors, Stethoscope, Syringe, Worm } from '@lucide/svelte';
  import { formatCurrency, formatDate } from '../utils/formatters';
  import { EVENT_TYPE_ICON_KEYS, normalizeEvents, type EventListItem } from './event-list';

  let {
    id,
    fetchUrl,
    emptyText = 'Keine Ereignisse vorhanden.',
    loadingText = 'Lade Ereignisse...',
    showAlpakaNames = true,
    events = null,
  }: {
    id: string;
    fetchUrl?: string;
    emptyText?: string;
    loadingText?: string;
    showAlpakaNames?: boolean;
    events?: EventListItem[] | null;
  } = $props();

  let fetched = $state<EventListItem[]>([]);
  let loading = $state(true);
  let error = $state('');

  const items = $derived(fetchUrl ? fetched : normalizeEvents(events));
  const isLoading = $derived(fetchUrl ? loading : events === null);
  const hasError = $derived(fetchUrl ? error !== '' : false);

  const hasCosts = $derived(items.some((event) => !Number.isNaN(Number(event.cost))));
  const totalCost = $derived(
    items.reduce((sum, event) => {
      const value = Number(event.cost);
      if (Number.isNaN(value)) return sum;
      return sum + value;
    }, 0),
  );

  function hasCost(cost: number | string | null | undefined): boolean {
    return !Number.isNaN(Number(cost));
  }

  function eventIcon(eventType: string | undefined): typeof CircleEllipsis {
    const normalizedType = (eventType ?? '').trim().toLowerCase();
    switch (EVENT_TYPE_ICON_KEYS[normalizedType]) {
      case 'worm':
        return Worm;
      case 'scissors':
        return Scissors;
      case 'syringe':
        return Syringe;
      case 'stethoscope':
        return Stethoscope;
      default:
        return CircleEllipsis;
    }
  }

  async function load() {
    loading = true;
    error = '';
    try {
      const response = await fetch(fetchUrl ?? '/api/events');
      if (!response.ok) {
        throw new Error('Ereignisse konnten nicht geladen werden.');
      }
      fetched = normalizeEvents(await response.json());
    } catch (e) {
      console.error(e);
      error = 'Ereignisse konnten nicht geladen werden.';
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    if (fetchUrl) {
      load();
    }
  });
</script>

<div id={id} class={`events-content${isLoading ? ' loading' : ''}`}>
  {#if isLoading}
    <p class="loading-text">{loadingText}</p>
  {:else if hasError}
    <p class="error">{error}</p>
  {:else if items.length === 0}
    <p class="empty">{emptyText}</p>
  {:else}
    <div class="event-table-wrapper">
      <table class="event-table">
        <thead>
          <tr>
            <th scope="col" class="event-date-header" aria-label="Datum"></th>
            <th scope="col" class="event-type-header" aria-label="Ereignistyp"></th>
            <th scope="col" aria-label="Details"></th>
            <th scope="col" class="event-cost-header">Kosten</th>
          </tr>
        </thead>
        <tbody>
          {#each items as item}
            {@const Icon = eventIcon(item.eventType)}
            <tr>
              <td class="event-date">{formatDate(item.eventDate)}</td>
              <td class="event-type">
                <span class="event-icon-wrapper" title={item.eventType || 'Ereignis'} aria-label={item.eventType || 'Ereignis'}>
                  <Icon class="event-icon" aria-hidden="true" />
                </span>
              </td>
              <td class="event-details">
                {#if showAlpakaNames && Array.isArray(item.alpakaNames) && item.alpakaNames.length > 0}
                  <ul class="alpaka-list">
                    {#each item.alpakaNames as name}
                      <li class="alpaka-name">{name}</li>
                    {/each}
                  </ul>
                {/if}
                {#if item.comment}
                  <p class="event-comment">{item.comment}</p>
                {/if}
              </td>
              <td class="event-cost">{formatCurrency(item.cost)}</td>
            </tr>
          {/each}
        </tbody>
        {#if hasCosts}
          <tfoot>
            <tr>
              <td class="event-total-label" colspan="3">Summe</td>
              <td class="event-cost event-total">{formatCurrency(totalCost)}</td>
            </tr>
          </tfoot>
        {/if}
      </table>
    </div>

    <div class="event-cards">
      {#each items as item}
        {@const Icon = eventIcon(item.eventType)}
        <article class="event-card">
          <div class="event-card-head">
            <span class="event-icon-wrapper" title={item.eventType || 'Ereignis'} aria-label={item.eventType || 'Ereignis'}>
              <Icon class="event-icon" aria-hidden="true" />
            </span>
            <div class="event-card-meta">
              <p class="event-card-type">{item.eventType || 'Ereignis'}</p>
              <p class="event-card-date">{formatDate(item.eventDate)}</p>
            </div>
            {#if hasCost(item.cost)}
              <span class="event-cost event-card-cost">{formatCurrency(item.cost)}</span>
            {/if}
          </div>
          <div class="event-card-body">
            {#if showAlpakaNames && Array.isArray(item.alpakaNames) && item.alpakaNames.length > 0}
              <ul class="alpaka-list">
                {#each item.alpakaNames as name}
                  <li class="alpaka-name">{name}</li>
                {/each}
              </ul>
            {/if}
            {#if item.comment}
              <p class="event-comment">{item.comment}</p>
            {/if}
          </div>
        </article>
      {/each}
      {#if hasCosts}
        <div class="event-cards-total">
          <span class="event-card-type">Summe</span>
          <span class="event-cost">{formatCurrency(totalCost)}</span>
        </div>
      {/if}
    </div>
  {/if}
</div>

<style>
  .events-content {
    background-color: #fff;
    border-radius: 0.75rem;
    padding: 1.25rem;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.06);
    min-height: 120px;
  }

  .event-table-wrapper {
    width: 100%;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .event-table {
    width: 100%;
    border-collapse: collapse;
    min-width: 520px;
  }

  .event-table th,
  .event-table td {
    padding: 0.75rem 0.5rem;
    vertical-align: top;
  }

  .event-table thead th {
    font-size: 0.85rem;
    color: var(--sahlchen);
    font-weight: 600;
  }

  .event-date-header,
  .event-type-header {
    width: 3rem;
  }

  .event-cost-header,
  .event-cost {
    text-align: right;
    white-space: nowrap;
  }

  .event-table tbody td {
    border-top: 1px solid rgba(0, 32, 73, 0.08);
  }

  .event-table tbody tr:first-child td {
    border-top: none;
  }

  .event-date {
    color: var(--taubenblau);
    font-weight: 700;
    white-space: nowrap;
  }

  .event-type {
    text-align: center;
  }

  .event-icon-wrapper {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 999px;
    background-color: var(--himmelblau);
    color: var(--schurwolle);
  }

  :global(.event-icon) {
    width: 1.25rem;
    height: 1.25rem;
  }

  .event-details {
    color: var(--taubenblau);
  }

  .alpaka-list {
    margin: 0 0 0.35rem;
    padding-left: 1.25rem;
    color: var(--taubenblau);
  }

  .alpaka-name {
    margin-bottom: 0.25rem;
    line-height: 1.4;
  }

  .event-comment {
    margin: 0;
    color: var(--taubenblau);
    line-height: 1.5;
  }

  .event-cost {
    font-weight: 700;
    color: var(--weidegruen);
  }

  .event-total-label {
    text-align: right;
    font-weight: 700;
    color: var(--taubenblau);
    padding-right: 0.5rem;
  }

  .event-total {
    border-top: 2px solid rgba(0, 32, 73, 0.12);
    padding-top: 0.75rem;
  }

  .event-cards {
    display: none;
  }

  .event-card {
    border: 1px solid rgba(0, 32, 73, 0.08);
    border-radius: 0.75rem;
    padding: 0.85rem 1rem;
  }

  .event-card-head {
    display: flex;
    align-items: flex-start;
    gap: 0.75rem;
  }

  .event-card-meta {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    min-width: 0;
  }

  .event-card-type {
    margin: 0;
    color: var(--taubenblau);
    font-weight: 700;
    line-height: 1.3;
  }

  .event-card-date {
    margin: 0;
    color: var(--taubenblau);
    font-size: 0.85rem;
    opacity: 0.7;
  }

  .event-card-cost {
    margin-left: auto;
    white-space: nowrap;
  }

  .event-card-body {
    margin-top: 0.65rem;
    padding-top: 0.65rem;
    border-top: 1px solid rgba(0, 32, 73, 0.08);
  }

  .event-card-body .alpaka-list {
    margin-bottom: 0;
  }

  .event-card-body .alpaka-list + .event-comment {
    margin-top: 0.35rem;
  }

  .event-cards-total {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-top: 2px solid rgba(0, 32, 73, 0.12);
    padding: 0.75rem 1rem 0;
    margin-top: 0.35rem;
  }

  .loading-text,
  .error,
  .empty {
    margin: 0;
  }

  .error {
    color: #b00020;
  }

  @media (max-width: 768px) {
    .event-table-wrapper {
      display: none;
    }

    .event-cards {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
  }
</style>
