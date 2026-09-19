<script lang="ts">
  import { onMount } from 'svelte';
  import type { Component } from 'svelte';
  import {
    BarChart3,
    CalendarDays,
    Clock,
    Inbox,
    PawPrint,
    Sparkles,
    Ticket,
  } from '@lucide/svelte';
  import { normalizeEvents, type EventListItem } from '../../utils/events';
  import { normalizeGutscheine, type Gutschein, type GutscheinRaw } from '../../utils/gutschein';
  import { formatCurrency, formatDate, toNumber } from '../../utils/formatters';

  type MessageStats = {
    Total: number;
    Spam: number;
    Legit: number;
    OldCount: number;
    Series: { Period: string; Spam: number; Legit: number }[];
  };

  type Tile = {
    icon: Component;
    href: string;
    title: string;
    value?: string | null;
    detail?: string | null;
  };

  let alpakaCount = $state<number | null>(null);
  let events = $state<EventListItem[] | null>(null);
  let gutscheine = $state<Gutschein[] | null>(null);
  let stats = $state<MessageStats | null>(null);

  const latestEvent = $derived.by(() => {
    if (!events || events.length === 0) return null;
    return [...events].sort(
      (a, b) => new Date(b.eventDate).valueOf() - new Date(a.eventDate).valueOf(),
    )[0];
  });
  const summeOffen = $derived(
    gutscheine === null
      ? null
      : gutscheine.reduce((summe, g) => (!g.eingeloestAm ? summe + toNumber(g.betrag) : summe), 0),
  );
  const spamPct = $derived(
    stats && stats.Total > 0 ? Math.round((stats.Spam / stats.Total) * 100) : null,
  );

  const hofTiles = $derived<Tile[]>([
    {
      icon: PawPrint,
      href: '/alpakas',
      title: 'Alpakas',
      value: alpakaCount === null ? null : String(alpakaCount),
      detail: alpakaCount === null ? null : 'Stammdaten und Ereignisse',
    },
    {
      icon: CalendarDays,
      href: '/alpakas',
      title: 'Ereignisse',
      value: events === null ? null : String(events.length),
      detail:
        events === null
          ? null
          : latestEvent
            ? `Letztes: ${latestEvent.eventType || 'Ereignis'} · ${formatDate(latestEvent.eventDate)}`
            : 'Noch keine Ereignisse.',
    },
    {
      icon: Ticket,
      href: '/gutscheine',
      title: 'Gutscheine',
      value: summeOffen === null ? null : formatCurrency(summeOffen),
      detail: summeOffen === null ? null : 'Noch nicht eingelöst',
    },
    {
      icon: Sparkles,
      href: '/assistent',
      title: 'Assistent',
      value: undefined,
      detail: 'Fragen zu Alpakas, Ereignissen und Gutscheinen im Chat stellen.',
    },
  ]);

  const websiteTiles = $derived<Tile[]>([
    {
      icon: Inbox,
      href: '/website/messages',
      title: 'Nachrichten',
      value: stats === null ? null : String(stats.Total),
      detail:
        stats === null
          ? null
          : stats.Total === 0
            ? 'Keine Nachrichten in 30 Tagen'
            : `${spamPct} % Spam`,
    },
    {
      icon: BarChart3,
      href: '/website/pageviews',
      title: 'Statistik',
      value: undefined,
      detail: 'Seitenaufrufe und Besucherquellen der Website.',
    },
    {
      icon: Clock,
      href: '/website/sitzungen',
      title: 'Sitzungen',
      value: undefined,
      detail: 'Sitzungsverläufe der Website-Besucher.',
    },
  ]);

  const clusters = $derived([
    { label: 'Hof', tiles: hofTiles },
    { label: 'Website', tiles: websiteTiles },
  ]);

  async function fetchJson<T>(url: string): Promise<T | null> {
    try {
      const response = await fetch(url);
      if (!response.ok) return null;
      return (await response.json()) as T;
    } catch (error) {
      console.error(error);
      return null;
    }
  }

  async function load() {
    const [alpakasResult, eventsResult, gutscheineResult, statsResult] = await Promise.allSettled([
      fetchJson<unknown[]>('/api/alpakas'),
      fetchJson<unknown[]>('/api/events'),
      fetchJson<unknown[]>('/api/gutscheine'),
      fetchJson<MessageStats>('/api/messages/stats?days=30'),
    ]);

    alpakaCount =
      alpakasResult.status === 'fulfilled' && Array.isArray(alpakasResult.value)
        ? alpakasResult.value.length
        : null;
    events =
      eventsResult.status === 'fulfilled' && eventsResult.value !== null
        ? normalizeEvents(eventsResult.value)
        : null;
    gutscheine =
      gutscheineResult.status === 'fulfilled' && gutscheineResult.value !== null
        ? normalizeGutscheine(gutscheineResult.value as GutscheinRaw[])
        : null;
    stats = statsResult.status === 'fulfilled' ? statsResult.value : null;
  }

  onMount(load);
</script>

<section class="overview section">
  <div class="container">
    {#each clusters as cluster (cluster.label)}
      <div class="cluster">
        <h2 class="cluster-label">{cluster.label}</h2>
        <div class="tile-grid">
          {#each cluster.tiles as tile (tile.title)}
            {@const Icon = tile.icon}
            <a class="tile" href={tile.href}>
              <span class="tile-icon" aria-hidden="true">
                <Icon />
              </span>
              <span class="tile-body">
                <span class="tile-title">{tile.title}</span>
                {#if tile.value !== undefined}
                  <span class="tile-value">{tile.value ?? '–'}</span>
                {/if}
                {#if tile.detail}
                  <span class="tile-detail">{tile.detail}</span>
                {/if}
              </span>
            </a>
          {/each}
        </div>
      </div>
    {/each}
  </div>
</section>

<style>
  .overview {
    background-color: var(--schurwolle);
    color: var(--taubenblau);
  }

  .cluster {
    margin-bottom: 2rem;
  }

  .cluster-label {
    margin: 0 0 0.75rem;
    font-size: 0.9rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--weidegruen);
    text-align: left;
  }

  .tile-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 1rem;
  }

  .tile {
    display: flex;
    align-items: flex-start;
    gap: 0.85rem;
    background: #fff;
    border-radius: 0.75rem;
    padding: 1.25rem;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.06);
    border: 1px solid rgba(0, 32, 73, 0.1);
    text-decoration: none;
    color: var(--taubenblau);
    transition: transform 0.15s ease;
  }

  .tile:hover,
  .tile:focus-visible {
    transform: translateY(-2px);
  }

  .tile:focus-visible {
    outline: 2px solid var(--weidegruen);
    outline-offset: 2px;
  }

  .tile-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 999px;
    background-color: var(--himmelblau);
    color: var(--schurwolle);
  }

  .tile-icon :global(svg) {
    width: 1.25rem;
    height: 1.25rem;
  }

  .tile-body {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    min-width: 0;
  }

  .tile-title {
    font-weight: 600;
    line-height: 1.3;
  }

  .tile-value {
    font-size: 1.5rem;
    font-weight: 700;
    line-height: 1.2;
  }

  .tile-detail {
    font-size: 0.85rem;
    color: rgba(0, 32, 73, 0.7);
    line-height: 1.4;
  }
</style>
