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
  {/if}
</div>

<style>
  .events-content {
    background-color: #fff;
    border-radius: 0.75rem;
    padding: 1.25rem;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.06);
    min-height: 120px;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .event-table-wrapper {
    width: 100%;
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

  .event-icon {
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

  .loading-text,
  .error,
  .empty {
    margin: 0;
  }

  .error {
    color: #b00020;
  }

  @media (max-width: 768px) {
    .event-table th,
    .event-table td {
      padding: 0.65rem 0.25rem;
    }

    .event-table {
      min-width: 460px;
    }

    .event-date,
    .event-cost {
      white-space: normal;
    }
  }
</style>
