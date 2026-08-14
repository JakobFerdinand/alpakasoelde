<script lang="ts">
  import Card from './Card.svelte';
  import { formatCurrency, formatDate, toNumber } from '../utils/formatters';
  import type { Gutschein } from '../utils/gutschein';

  let {
    gutscheine,
    fehler = '',
    onRedeem,
    onRefresh,
  }: {
    gutscheine: Gutschein[] | null;
    fehler?: string;
    onRedeem?: (gutschein: Gutschein) => void;
    onRefresh?: () => void;
  } = $props();

  const items = $derived(gutscheine ?? []);
  const wirdGeladen = $derived(gutscheine === null);
  const summeVerkauft = $derived(items.reduce((summe, g) => summe + toNumber(g.betrag), 0));
  const summeOffen = $derived(
    items.reduce((summe, g) => (!g.eingeloestAm ? summe + toNumber(g.betrag) : summe), 0),
  );
</script>

<Card class="gutschein-liste-card">
  <div class="listen-kopf">
    <div>
      <p class="card-eyebrow">Übersicht</p>
      <h2 class="card-title">Verkaufte Gutscheine</h2>
      <p class="card-subtitle">Alle Gutscheine mit Kauf- und Einlösedaten.</p>
    </div>
    <button type="button" class="ghost-button" onclick={onRefresh}>Aktualisieren</button>
  </div>
  <div class="gutschein-liste" class:ladezustand={wirdGeladen}>
    {#if wirdGeladen}
      <p class="loading-text">Lade Gutscheine...</p>
    {:else if fehler}
      <p class="error">{fehler}</p>
    {:else if items.length === 0}
      <p class="empty">Noch keine Gutscheine erfasst.</p>
    {:else}
      <table class="gutschein-tabelle" aria-label="Gutscheinliste">
        <thead>
          <tr class="gutschein-kopf">
            <th scope="col">Gutscheinnummer</th>
            <th scope="col">Kaufdatum</th>
            <th scope="col">Betrag</th>
            <th scope="col">Verkauft an</th>
            <th scope="col">Eingelöst am</th>
          </tr>
        </thead>
        <tbody>
          {#each items as gutschein}
            <tr class="gutschein-zeile">
              <td>{gutschein.gutscheinnummer || '—'}</td>
              <td>{formatDate(gutschein.kaufdatum)}</td>
              <td>{formatCurrency(gutschein.betrag)}</td>
              <td>{gutschein.verkauftAn || '—'}</td>
              <td>
                {#if gutschein.eingeloestAm}
                  {formatDate(gutschein.eingeloestAm)}
                {:else}
                  <button type="button" class="link-button" onclick={() => onRedeem?.(gutschein)}>
                    Einlösen
                  </button>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
        <tfoot>
          <tr class="gutschein-summe">
            <td colspan="3" aria-hidden="true"></td>
            <td class="gutschein-summe-zelle">
              <span class="gutschein-summe-betrag">{formatCurrency(summeVerkauft)}</span>
            </td>
            <td class="gutschein-summe-zelle">
              <span class="gutschein-summe-betrag">{formatCurrency(summeOffen)}</span>
            </td>
          </tr>
        </tfoot>
      </table>
    {/if}
  </div>
</Card>

<style>
  .gutschein-liste-card {
    min-width: 0;
  }

  .listen-kopf {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 1rem;
    margin-bottom: 0.5rem;
  }

  .gutschein-liste {
    border: 1px solid rgba(0, 32, 73, 0.08);
    border-radius: 0.75rem;
    padding: 1rem;
    min-height: 120px;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
    width: 100%;
  }

  .gutschein-liste.ladezustand {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .gutschein-tabelle {
    width: 100%;
    border-collapse: collapse;
    min-width: 640px;
  }

  .gutschein-tabelle th,
  .gutschein-tabelle td {
    text-align: left;
    padding: 0.6rem 0.4rem;
  }

  .gutschein-kopf th {
    font-weight: 700;
    color: var(--taubenblau);
    border-bottom: 2px solid rgba(0, 32, 73, 0.15);
  }

  .gutschein-zeile td {
    border-bottom: 1px solid rgba(0, 32, 73, 0.08);
  }

  .gutschein-zeile:last-child td {
    border-bottom: none;
  }

  .gutschein-summe td {
    border-top: 2px solid rgba(0, 32, 73, 0.15);
    padding-top: 0.85rem;
  }

  .gutschein-summe-zelle {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 0.1rem;
    font-weight: 700;
    color: var(--taubenblau);
  }

  .gutschein-summe-betrag {
    font-size: 1rem;
  }

  @media (max-width: 720px) {
    .listen-kopf {
      flex-direction: column;
      align-items: flex-start;
    }

    .gutschein-tabelle th,
    .gutschein-tabelle td {
      padding: 0.5rem;
      font-size: 0.95rem;
    }
  }
</style>