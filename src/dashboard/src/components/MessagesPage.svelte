<script lang="ts">
  import { onMount } from 'svelte';
  import { ArrowDown, ArrowUp, ArrowUpDown, MailX, ShieldAlert, Trash2 } from '@lucide/svelte';
  import MessageCell from './MessageCell.svelte';
  import { formatTimestamp } from '../utils/formatters';

  type Message = {
    Id: string;
    Name: string;
    Email: string;
    Phone: string;
    Message: string;
    Timestamp: string;
    IsSpam: boolean;
  };

  type FilterKey = 'all' | 'legit' | 'spam';
  type SortKey = 'name' | 'message' | 'email' | 'phone' | 'timestamp';
  type SortDir = 'asc' | 'desc';

  const columnCount = 7;
  const collator = new Intl.Collator('de', { sensitivity: 'base', numeric: true });
  const oldMessageAgeMs = 6 * 30 * 24 * 60 * 60 * 1000;

  let messages = $state<Message[]>([]);
  let loading = $state(true);
  let error = $state('');
  let filter = $state<FilterKey>('all');
  let sortKey = $state<SortKey>('timestamp');
  let sortDir = $state<SortDir>('desc');

  const isOldMessage = (message: Message): boolean =>
    new Date(message.Timestamp).getTime() < Date.now() - oldMessageAgeMs;

  function matchesFilter(message: Message): boolean {
    if (filter === 'spam') return message.IsSpam;
    if (filter === 'legit') return !message.IsSpam;
    return true;
  }

  function compareMessages(a: Message, b: Message): number {
    let result: number;
    switch (sortKey) {
      case 'name':
        result = collator.compare(a.Name, b.Name);
        break;
      case 'message':
        result = collator.compare(a.Message, b.Message);
        break;
      case 'email':
        result = collator.compare(a.Email, b.Email);
        break;
      case 'phone':
        result = collator.compare(a.Phone, b.Phone);
        break;
      case 'timestamp':
        result = new Date(a.Timestamp).getTime() - new Date(b.Timestamp).getTime();
        break;
    }
    return sortDir === 'asc' ? result : -result;
  }

  const visible = $derived(messages.filter(matchesFilter).sort(compareMessages));
  const spamCount = $derived(messages.filter((m) => m.IsSpam).length);
  const summary = $derived(
    messages.length === 0
      ? ''
      : spamCount > 0
        ? `${messages.length} Nachrichten · ${spamCount} Spam`
        : `${messages.length} Nachrichten · kein Spam`,
  );
  const emptyText = $derived(
    filter === 'spam'
      ? 'Keine Spam-Nachrichten vorhanden.'
      : filter === 'legit'
        ? 'Keine gesendeten Nachrichten vorhanden.'
        : 'Keine Nachrichten gefunden.',
  );

  function toggleSort(key: SortKey) {
    if (key === sortKey) {
      sortDir = sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      sortKey = key;
      sortDir = key === 'timestamp' ? 'desc' : 'asc';
    }
  }

  function sortDirFor(key: SortKey): 'ascending' | 'descending' | undefined {
    if (key !== sortKey) return undefined;
    return sortDir === 'asc' ? 'ascending' : 'descending';
  }

  async function deleteMessage(id: string) {
    const confirmed = window.confirm('Möchten Sie diese Nachricht wirklich löschen?');
    if (!confirmed) return;

    try {
      const res = await fetch(`/api/messages/${id}`, { method: 'DELETE' });
      if (!res.ok) {
        console.error('Failed to delete message', id, res.status);
        return;
      }
      messages = messages.filter((m) => m.Id !== id);
    } catch (e) {
      console.error(e);
    }
  }

  async function loadMessages() {
    loading = true;
    error = '';
    try {
      const res = await fetch('/api/messages');
      if (!res.ok) throw new Error(`Failed to load messages (${res.status})`);
      messages = await res.json();
    } catch (e) {
      console.error(e);
      error = 'Nachrichten konnten nicht geladen werden.';
    } finally {
      loading = false;
    }
  }

  onMount(loadMessages);
</script>

<section class="dashboard-page section">
  <div class="container">
    <h2>Eingegangene Nachrichten</h2>
    <div class="toolbar">
      <div class="filter-group" role="group" aria-label="Nachrichten filtern">
        {#each (['all', 'legit', 'spam'] as FilterKey[]) as key (key)}
          <button
            type="button"
            class="filter-button"
            class:is-active={filter === key}
            aria-pressed={filter === key}
            onclick={() => (filter = key)}
          >
            {key === 'all' ? 'Alle' : key === 'legit' ? 'Gesendet' : 'Spam'}
          </button>
        {/each}
      </div>
      <p id="message-summary" class="message-summary" aria-live="polite">{summary}</p>
    </div>
    <div class="table-wrapper">
      <table class="message-table data-table">
        <thead>
          <tr>
            <th scope="col" class="marker-header" aria-label="Markierungen"></th>
            <th scope="col" aria-sort={sortDirFor('name')}>
              <button type="button" class="sort-button" onclick={() => toggleSort('name')}>
                Name
                <span class="sort-icon" aria-hidden="true">
                  {#if sortKey === 'name'}
                    {#if sortDir === 'asc'}
                      <ArrowUp class="sort-icon-svg" />
                    {:else}
                      <ArrowDown class="sort-icon-svg" />
                    {/if}
                  {:else}
                    <ArrowUpDown class="sort-icon-svg" />
                  {/if}
                </span>
              </button>
            </th>
            <th scope="col" aria-sort={sortDirFor('message')}>
              <button type="button" class="sort-button" onclick={() => toggleSort('message')}>
                Nachricht
                <span class="sort-icon" aria-hidden="true">
                  {#if sortKey === 'message'}
                    {#if sortDir === 'asc'}
                      <ArrowUp class="sort-icon-svg" />
                    {:else}
                      <ArrowDown class="sort-icon-svg" />
                    {/if}
                  {:else}
                    <ArrowUpDown class="sort-icon-svg" />
                  {/if}
                </span>
              </button>
            </th>
            <th scope="col" aria-sort={sortDirFor('email')}>
              <button type="button" class="sort-button" onclick={() => toggleSort('email')}>
                Email
                <span class="sort-icon" aria-hidden="true">
                  {#if sortKey === 'email'}
                    {#if sortDir === 'asc'}
                      <ArrowUp class="sort-icon-svg" />
                    {:else}
                      <ArrowDown class="sort-icon-svg" />
                    {/if}
                  {:else}
                    <ArrowUpDown class="sort-icon-svg" />
                  {/if}
                </span>
              </button>
            </th>
            <th scope="col" aria-sort={sortDirFor('phone')}>
              <button type="button" class="sort-button" onclick={() => toggleSort('phone')}>
                Telefon
                <span class="sort-icon" aria-hidden="true">
                  {#if sortKey === 'phone'}
                    {#if sortDir === 'asc'}
                      <ArrowUp class="sort-icon-svg" />
                    {:else}
                      <ArrowDown class="sort-icon-svg" />
                    {/if}
                  {:else}
                    <ArrowUpDown class="sort-icon-svg" />
                  {/if}
                </span>
              </button>
            </th>
            <th scope="col" aria-sort={sortDirFor('timestamp')}>
              <button type="button" class="sort-button" onclick={() => toggleSort('timestamp')}>
                Zeitpunkt
                <span class="sort-icon" aria-hidden="true">
                  {#if sortKey === 'timestamp'}
                    {#if sortDir === 'asc'}
                      <ArrowUp class="sort-icon-svg" />
                    {:else}
                      <ArrowDown class="sort-icon-svg" />
                    {/if}
                  {:else}
                    <ArrowUpDown class="sort-icon-svg" />
                  {/if}
                </span>
              </button>
            </th>
            <th scope="col" class="actions-header">Aktionen</th>
          </tr>
        </thead>
        <tbody id="message-table-body">
          {#if loading}
            <tr>
              <td colspan={columnCount}>Lade Nachrichten...</td>
            </tr>
          {:else if error}
            <tr>
              <td colspan={columnCount} class="error">{error}</td>
            </tr>
          {:else if visible.length === 0}
            <tr>
              <td colspan={columnCount}>{emptyText}</td>
            </tr>
          {:else}
            {#each visible as message (message.Id)}
              <tr class:is-spam={message.IsSpam} class:is-old={isOldMessage(message)}>
                <td class="marker-cell">
                  {#if message.IsSpam}
                    <span class="marker marker-spam" role="img" title="Als Spam eingestuft" aria-label="Als Spam eingestuft">
                      <ShieldAlert class="marker-svg" aria-hidden="true" />
                    </span>
                  {/if}
                  {#if isOldMessage(message)}
                    <span class="marker marker-old" role="img" title="Älter als 6 Monate" aria-label="Älter als 6 Monate">
                      <MailX class="marker-svg" aria-hidden="true" />
                    </span>
                  {/if}
                </td>
                <td>{message.Name}</td>
                <td class="message-cell">
                  <MessageCell message={message.Message} />
                </td>
                <td>{message.Email}</td>
                <td>{message.Phone || '–'}</td>
                <td class="nowrap">{formatTimestamp(message.Timestamp)}</td>
                <td class="action-cell">
                  <button type="button" class="icon-button" aria-label="Nachricht löschen" onclick={() => deleteMessage(message.Id)}>
                    <Trash2 class="delete-icon" aria-hidden="true" />
                  </button>
                </td>
              </tr>
            {/each}
          {/if}
        </tbody>
      </table>
    </div>
  </div>
</section>

<style>
  .dashboard-page {
    background-color: var(--auwasser);
    color: var(--taubenblau);
  }

  .dashboard-page .container {
    max-width: none;
  }

  .message-table {
    width: 100%;
  }

  .message-table th {
    position: sticky;
    top: 0;
    z-index: 1;
  }

  .message-table tbody tr.is-old td {
    background-color: rgba(176, 0, 32, 0.1);
  }

  .message-table tbody tr.is-spam td {
    background-color: rgba(215, 133, 75, 0.2);
  }

  .marker-header,
  .marker-cell {
    width: 2.25rem;
    vertical-align: middle;
    padding-left: 0.75rem;
    padding-right: 0.25rem;
  }

  .marker {
    display: inline-flex;
    align-items: center;
    justify-content: center;
  }

  .marker + .marker {
    margin-left: 0.35rem;
  }

  .marker-svg {
    width: 1.1rem;
    height: 1.1rem;
    stroke: currentColor;
    fill: none;
  }

  .marker-spam {
    color: var(--backstein);
  }

  .marker-old {
    color: #b00020;
  }

  .message-cell {
    white-space: pre-wrap;
    word-break: break-word;
    max-width: 22rem;
  }

  .nowrap {
    white-space: nowrap;
  }

  .actions-header,
  .action-cell {
    text-align: center;
    width: 5rem;
  }

  .sort-button {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    padding: 0;
    border: none;
    background: none;
    font-family: inherit;
    font-size: inherit;
    font-weight: inherit;
    color: inherit;
    cursor: pointer;
  }

  .sort-button:hover {
    text-decoration: underline;
  }

  .sort-button:focus-visible {
    outline: 2px solid var(--schurwolle);
    outline-offset: 2px;
  }

  .sort-icon {
    display: inline-flex;
    align-items: center;
    opacity: 0.55;
  }

  th[aria-sort='ascending'] .sort-icon,
  th[aria-sort='descending'] .sort-icon {
    opacity: 1;
  }

  .sort-icon-svg {
    width: 1rem;
    height: 1rem;
    stroke: currentColor;
    fill: none;
  }

  .toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    margin-bottom: 0.5rem;
  }

  .filter-group {
    display: inline-flex;
    border-radius: 0.5rem;
    overflow: hidden;
    border: 1px solid rgba(0, 32, 73, 0.15);
  }

  .filter-button {
    border: none;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    padding: 0.5rem 1rem;
    font-family: inherit;
    font-size: 0.9rem;
    font-weight: 600;
    cursor: pointer;
  }

  .filter-button + .filter-button {
    border-left: 1px solid rgba(0, 32, 73, 0.15);
  }

  .filter-button:hover {
    background-color: var(--himmelblau);
  }

  .filter-button.is-active {
    background-color: var(--weidegruen);
    color: var(--schurwolle);
  }

  .filter-button:focus-visible {
    outline: 2px solid var(--taubenblau);
    outline-offset: -2px;
  }

  .message-summary {
    margin: 0;
    font-weight: 600;
  }

  @media (max-width: 600px) {
    .message-table th,
    .message-table td {
      font-size: 0.875rem;
    }

    .message-cell {
      max-width: 16rem;
    }
  }
</style>