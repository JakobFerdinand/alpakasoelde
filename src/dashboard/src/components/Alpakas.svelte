<script lang="ts">
  import { onMount } from 'svelte';
  import FormField from './FormField.svelte';
  import Modal from './Modal.svelte';
  import { calculateAge } from '../utils/formatters';

  type Alpaka = {
    Id: string;
    Geburtsdatum: string;
    ImageUrl: string | null;
    Name: string;
  };

  const MAX_FILE_SIZE = 15 * 1024 * 1024;

  let alpakaModalOpen = $state(false);
  let eventModalOpen = $state(false);
  let alpacas = $state<Alpaka[]>([]);
  let loading = $state(true);

  let alpakaName = $state('');
  let alpakaGeburtsdatum = $state('');
  let photoFile = $state<File | null>(null);

  let eventType = $state('');
  let selectedAlpakaIds = $state<string[]>([]);
  let eventDate = $state('');
  let eventCost = $state('');
  let eventComment = $state('');
  let eventSubmitting = $state(false);

  let eventTypeSelect: HTMLSelectElement | undefined = $state();
  let eventAlpakasSelect: HTMLSelectElement | undefined = $state();
  let eventDateInput: HTMLInputElement | undefined = $state();

  const sortedAlpacas = $derived([...alpacas].sort((a, b) => a.Name.localeCompare(b.Name)));

  function navigateToAlpaka(id: string) {
    window.location.href = `/alpakas?id=${encodeURIComponent(id)}`;
  }

  function ensureAlpakaSelection() {
    if (selectedAlpakaIds.length === 0 && alpacas.length > 0) {
      selectedAlpakaIds = [alpacas[0].Id];
    }
  }

  function openEventLightbox() {
    const today = new Date();
    const date = new Date(today.getTime() - today.getTimezoneOffset() * 60000);
    eventDate = date.toISOString().split('T')[0];
    ensureAlpakaSelection();
    eventModalOpen = true;
  }

  function onAddAlpakaSubmit(event: SubmitEvent) {
    if (photoFile && photoFile.size > MAX_FILE_SIZE) {
      event.preventDefault();
      alert(`File is too large. Maximum size is ${MAX_FILE_SIZE / (1024 * 1024)}MB.`);
    }
  }

  async function onAddEventSubmit(event: SubmitEvent) {
    event.preventDefault();
    if (!eventTypeSelect || !eventAlpakasSelect || !eventDateInput) return;

    if (!eventType) {
      eventTypeSelect.setCustomValidity('Bitte ein Ereignis auswählen.');
      eventTypeSelect.reportValidity();
      return;
    }
    eventTypeSelect.setCustomValidity('');

    if (selectedAlpakaIds.length === 0) {
      eventAlpakasSelect.setCustomValidity('Mindestens ein Alpaka auswählen.');
      eventAlpakasSelect.reportValidity();
      return;
    }
    eventAlpakasSelect.setCustomValidity('');

    if (!eventDate) {
      eventDateInput.setCustomValidity('Bitte ein Datum angeben.');
      eventDateInput.reportValidity();
      return;
    }
    eventDateInput.setCustomValidity('');

    const payload = {
      eventType,
      alpakaIds: selectedAlpakaIds,
      eventDate,
      cost: eventCost ? Number(eventCost) : null,
      comment: eventComment.trim() || null,
    };

    try {
      eventSubmitting = true;
      const response = await fetch('/api/events', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({}));
        throw new Error(errorBody?.detail || 'Das Ereignis konnte nicht gespeichert werden.');
      }

      eventType = '';
      selectedAlpakaIds = [];
      eventDate = '';
      eventCost = '';
      eventComment = '';
      eventModalOpen = false;
      alert('Ereignis erfolgreich gespeichert.');
    } catch (error) {
      console.error('Fehler beim Speichern des Ereignisses', error);
      alert(error instanceof Error ? error.message : 'Das Ereignis konnte nicht gespeichert werden.');
    } finally {
      eventSubmitting = false;
    }
  }

  async function loadAlpakas() {
    loading = true;
    try {
      const res = await fetch('/api/alpakas');
      if (!res.ok) return;
      alpacas = await res.json();
    } catch (error) {
      console.error('Failed to load alpacas:', error);
    } finally {
      loading = false;
    }
  }

  onMount(loadAlpakas);
</script>

<section class="dashboard-alpaka section">
  <div class="container">
    <div class="alpaka-header">
      <h2>Alpakas</h2>
      <div class="alpaka-actions">
        <button id="add-event-toggle" class="add-btn" aria-label="Neues Ereignis erfassen" title="Ereignis erfassen" onclick={openEventLightbox}>
          <span aria-hidden="true">&#128197;</span>
        </button>
        <button id="add-alpaka-toggle" class="add-btn" aria-label="Neues Alpaka hinzufügen" onclick={() => (alpakaModalOpen = true)}>+</button>
      </div>
    </div>
    <table id="alpaka-table" class="alpaka-table">
      <thead>
        <tr>
          <th></th>
          <th>Name</th>
          <th>Alter</th>
        </tr>
      </thead>
      <tbody id="alpaka-list">
        {#if loading}
          <tr>
            <td colspan="3" class="alpaka-loading loading">Lade Daten...</td>
          </tr>
        {:else}
          {#each alpacas as alpaka}
            <tr
              class="alpaka-item"
              tabindex="0"
              role="link"
              aria-label={`Details für ${alpaka.Name} anzeigen`}
              onclick={() => navigateToAlpaka(alpaka.Id)}
              onkeydown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  navigateToAlpaka(alpaka.Id);
                }
              }}
            >
              <td>
                {#if alpaka.ImageUrl}
                  <img class="alpaka-profile-photo" src={alpaka.ImageUrl} alt={alpaka.Name} />
                {:else}
                  <div class="alpaka-profile-placeholder">{alpaka.Name.charAt(0).toUpperCase()}</div>
                {/if}
              </td>
              <td class="alpaka-name">{alpaka.Name}</td>
              <td class="alpaka-age">{calculateAge(alpaka.Geburtsdatum)}</td>
            </tr>
          {/each}
        {/if}
      </tbody>
    </table>
  </div>

  <Modal bind:open={alpakaModalOpen} label="Neues Alpaka hinzufügen">
    <form id="add-alpaka-form" class="alpaka-form" method="post" action="/api/alpakas" enctype="multipart/form-data" onsubmit={onAddAlpakaSubmit}>
      <FormField label="Name" id="alpaka-name" required>
        <input id="alpaka-name" name="name" type="text" maxlength="100" required bind:value={alpakaName} />
      </FormField>
      <FormField label="Geburtsdatum" id="alpaka-geburtsdatum" required>
        <input id="alpaka-geburtsdatum" name="geburtsdatum" type="date" required bind:value={alpakaGeburtsdatum} />
      </FormField>
      <FormField label="Foto" id="alpaka-photo">
        <input
          id="alpaka-photo"
          name="photo"
          type="file"
          accept=".png,.jpg,.jpeg"
          onchange={(event) => (photoFile = (event.currentTarget as HTMLInputElement).files?.[0] ?? null)}
        />
      </FormField>
      <button type="submit" class="primary-button">Neues Alpaka anlegen</button>
    </form>
  </Modal>

  <Modal bind:open={eventModalOpen} label="Neues Ereignis erfassen">
    <form id="add-event-form" class="alpaka-form" novalidate onsubmit={onAddEventSubmit}>
      <FormField label="Ereignis" id="event-type" required>
        <select id="event-type" name="eventType" required bind:value={eventType} bind:this={eventTypeSelect}>
          <option value="">Bitte auswählen</option>
          <option value="Entwurmen">Entwurmen</option>
          <option value="Nägel schneiden">Nägel schneiden</option>
          <option value="Scheren">Scheren</option>
          <option value="Impfen">Impfen</option>
          <option value="Gesundheitscheck">Gesundheitscheck</option>
          <option value="Sonstiges">Sonstiges</option>
        </select>
      </FormField>
      <FormField label="Alpakas" id="event-alpakas" hint="Mit gedrückter STRG/Cmd Taste mehrere Alpakas auswählen." required>
        <select id="event-alpakas" name="alpakaIds" multiple size="5" required bind:value={selectedAlpakaIds} bind:this={eventAlpakasSelect}>
          {#each sortedAlpacas as alpaka}
            <option value={alpaka.Id}>{alpaka.Name}</option>
          {/each}
        </select>
      </FormField>
      <FormField label="Datum" id="event-date" required>
        <input id="event-date" name="eventDate" type="date" required bind:value={eventDate} bind:this={eventDateInput} />
      </FormField>
      <FormField label="Kosten (optional)" id="event-cost">
        <input id="event-cost" name="cost" type="number" min="0" step="0.01" inputmode="decimal" placeholder="0,00" bind:value={eventCost} />
      </FormField>
      <FormField label="Notiz" id="event-comment">
        <textarea id="event-comment" name="comment" rows="3" placeholder="Details zum Ereignis" bind:value={eventComment}></textarea>
      </FormField>
      <button type="submit" class="primary-button" disabled={eventSubmitting}>Ereignis speichern</button>
    </form>
  </Modal>
</section>

<style>
  .dashboard-alpaka {
    background-color: var(--himmelblau);
    color: var(--schurwolle);
  }

  .alpaka-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
  }

  .alpaka-table {
    width: 100%;
    border-collapse: collapse;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    border-radius: 0.5rem;
    overflow: hidden;
  }

  .alpaka-table th,
  .alpaka-table td {
    padding: 0.75rem 1rem;
    text-align: left;
    border-bottom: 1px solid var(--taubenblau);
  }

  .alpaka-table tr:last-child td {
    border-bottom: none;
  }

  .alpaka-name {
    font-weight: 600;
  }

  .alpaka-age {
    text-align: right;
  }

  .alpaka-loading {
    text-align: center;
  }

  .alpaka-actions {
    display: flex;
    gap: 0.5rem;
  }

  .alpaka-form {
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
  }

  .alpaka-item {
    transition: background-color 0.3s ease;
    cursor: pointer;
  }

  .alpaka-item:hover,
  .alpaka-item:focus-visible {
    background-color: var(--auwasser);
  }

  .alpaka-item:focus-visible {
    outline: 2px solid var(--weidegruen);
    outline-offset: -2px;
  }

  @media (max-width: 768px) {
    .alpaka-header {
      flex-direction: column;
      align-items: flex-start;
      gap: 1rem;
    }

    .alpaka-table {
      font-size: 0.9rem;
    }

    .alpaka-age {
      text-align: left;
    }
  }
</style>