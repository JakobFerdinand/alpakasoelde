<script lang="ts">
  import { onMount } from 'svelte';
  import EventList from './EventList.svelte';
  import { normalizeEvents, type EventListItem } from './event-list';
  import { calculateAge, formatDateForInput, formatDateLong } from '../utils/formatters';

  type Alpaka = {
    Id: string;
    Name: string;
    Geburtsdatum: string;
    ImageUrl: string | null;
    Events?: unknown;
  };

  const MAX_FILE_SIZE = 15 * 1024 * 1024;

  let currentAlpaka = $state<Alpaka | null>(null);
  let error = $state('');
  let editMode = $state(false);
  let saving = $state(false);
  let status = $state('');
  let name = $state('');
  let geburtsdatum = $state('');
  let photoFile = $state<File | null>(null);

  const loading = $derived(currentAlpaka === null && error === '');
  const alpakaEvents = $derived<EventListItem[] | null>(currentAlpaka ? normalizeEvents(currentAlpaka.Events ?? []) : null);

  const searchParams = new URLSearchParams(window.location.search);
  const alpakaId = searchParams.get('id');

  function enterEditMode() {
    if (!currentAlpaka) return;
    editMode = true;
    status = '';
    name = currentAlpaka.Name ?? '';
    geburtsdatum = formatDateForInput(currentAlpaka.Geburtsdatum);
    photoFile = null;
  }

  function exitEditMode() {
    if (!currentAlpaka) return;
    editMode = false;
    status = '';
    name = currentAlpaka.Name ?? '';
    geburtsdatum = formatDateForInput(currentAlpaka.Geburtsdatum);
    photoFile = null;
  }

  async function submitEdit(event: SubmitEvent) {
    if (!alpakaId || !currentAlpaka) return;
    event.preventDefault();

    const file = photoFile;
    if (file && file.size > MAX_FILE_SIZE) {
      alert(`Bild ist zu groß. Maximal ${MAX_FILE_SIZE / (1024 * 1024)}MB erlaubt.`);
      return;
    }

    saving = true;
    status = 'Speichere Änderungen...';

    try {
      const formData = new FormData();
      formData.append('name', name);
      formData.append('geburtsdatum', geburtsdatum);
      if (file) {
        formData.append('photo', file);
      }

      const response = await fetch(`/api/alpakas/${encodeURIComponent(alpakaId)}`, {
        method: 'PUT',
        body: formData,
      });

      if (!response.ok) {
        throw new Error('Update failed');
      }

      currentAlpaka = await response.json();
      editMode = false;
      status = 'Änderungen gespeichert!';
    } catch (loadError) {
      console.error(loadError);
      status = 'Speichern fehlgeschlagen. Bitte versuche es erneut.';
    } finally {
      saving = false;
      photoFile = null;
    }
  }

  async function loadAlpaka(id: string) {
    try {
      const response = await fetch(`/api/alpakas/${encodeURIComponent(id)}`);
      if (response.status === 404) {
        error = 'Alpaka nicht gefunden.';
        return;
      }
      if (!response.ok) {
        throw new Error(`Failed to load alpaka ${id}`);
      }
      const updated = (await response.json()) as Alpaka;
      currentAlpaka = updated;
      name = updated.Name ?? '';
      geburtsdatum = formatDateForInput(updated.Geburtsdatum);
    } catch (loadError) {
      console.error(loadError);
      error = 'Alpaka konnte nicht geladen werden.';
    }
  }

  onMount(() => {
    if (!alpakaId) {
      error = 'Es wurde kein Alpaka angegeben.';
    } else {
      loadAlpaka(alpakaId);
    }
  });
</script>

<section class="alpaka-detail section">
  <div class="container">
    <a class="back-link" href="/">&larr; Zurück zur Übersicht</a>
    <div class="alpaka-detail-card" class:loading={loading}>
      <div class="alpaka-detail-header">
        <p class="alpaka-detail-title">Alpaka Details</p>
        {#if currentAlpaka}
          <button
            class="edit-btn"
            type="button"
            aria-label="Alpaka bearbeiten"
            title="Alpaka bearbeiten"
            hidden={editMode}
            disabled={editMode}
            onclick={enterEditMode}
          >
            <svg
              aria-hidden="true"
              focusable="false"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5Z" />
            </svg>
          </button>
        {/if}
      </div>

      {#if error}
        <p class="error">{error}</p>
      {:else if currentAlpaka}
        <div class="alpaka-detail-content">
          {#if currentAlpaka.ImageUrl}
            <img class="alpaka-photo" src={currentAlpaka.ImageUrl} alt={currentAlpaka.Name} />
          {:else}
            <div class="alpaka-photo placeholder">{currentAlpaka.Name.charAt(0).toUpperCase()}</div>
          {/if}

          <h2>{currentAlpaka.Name}</h2>

          {#if !editMode}
            <p class="alpaka-meta">Alter: {calculateAge(currentAlpaka.Geburtsdatum)}</p>
            <p class="alpaka-meta">Geburtsdatum: {formatDateLong(currentAlpaka.Geburtsdatum)}</p>
            {#if status}
              <p class="edit-status" role="status">{status}</p>
            {/if}
          {:else}
            <form id="alpaka-edit-form" class="alpaka-edit-form" onsubmit={submitEdit}>
              <div class="form-field">
                <label for="alpaka-name">Name*</label>
                <input id="alpaka-name" name="name" type="text" maxlength="100" required bind:value={name} />
              </div>
              <div class="form-field">
                <label for="alpaka-geburtsdatum">Geburtsdatum*</label>
                <input id="alpaka-geburtsdatum" name="geburtsdatum" type="date" required bind:value={geburtsdatum} />
              </div>
              <div class="form-field">
                <label for="alpaka-photo">Neues Foto (optional)</label>
                <input
                  id="alpaka-photo"
                  name="photo"
                  type="file"
                  accept=".png,.jpg,.jpeg"
                  onchange={(event) => (photoFile = (event.currentTarget as HTMLInputElement).files?.[0] ?? null)}
                />
                <p class="form-hint">Maximal 15 MB, .png, .jpg oder .jpeg</p>
              </div>
              <div class="form-actions">
                <button type="button" class="ghost-button" onclick={exitEditMode}>Abbrechen</button>
                <button type="submit" class="primary-button" disabled={saving}>Änderungen speichern</button>
              </div>
              {#if status}
                <p class="edit-status" role="status">{status}</p>
              {/if}
            </form>
          {/if}
        </div>
      {:else}
        <p class="loading-text">Lade Alpaka...</p>
      {/if}
    </div>

    {#if currentAlpaka}
      <div class="alpaka-events">
        <h3>Ereignisse</h3>
        <EventList
          id="alpaka-events-content"
          loadingText="Lade Ereignisse..."
          emptyText="Keine Ereignisse vorhanden."
          showAlpakaNames={false}
          events={alpakaEvents}
        />
      </div>
    {/if}
  </div>
</section>

<style>
  .alpaka-detail {
    background-color: var(--himmelblau);
    color: var(--schurwolle);
  }

  .alpaka-detail-card {
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    border-radius: 0.75rem;
    padding: 2rem;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.15);
    width: 100%;
  }

  .alpaka-detail-card.loading {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 12rem;
  }

  .alpaka-photo {
    display: block;
    width: min(100%, 18rem);
    aspect-ratio: 3 / 4;
    border-radius: 0.5rem;
    object-fit: cover;
    margin: 0 auto 1.5rem;
  }

  .alpaka-photo.placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: var(--weidegruen);
    color: var(--schurwolle);
    font-size: 2.5rem;
    font-weight: 700;
  }

  .alpaka-detail-card h2 {
    margin-top: 0;
    margin-bottom: 1rem;
    font-size: 2rem;
    text-align: center;
  }

  .alpaka-meta {
    margin: 0.25rem 0;
    font-weight: 500;
    text-align: center;
  }

  .alpaka-detail-content {
    text-align: center;
  }

  .alpaka-events {
    margin-top: 2rem;
    background-color: var(--schurwolle);
    color: var(--taubenblau);
    border-radius: 0.75rem;
    padding: 1.5rem;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
  }

  .alpaka-events h3 {
    margin-top: 0;
    margin-bottom: 1rem;
  }

  .alpaka-detail-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    margin-bottom: 1rem;
  }

  .alpaka-detail-title {
    margin: 0;
    font-weight: 700;
  }

  .edit-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: var(--weidegruen);
    color: var(--schurwolle);
    border: none;
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 0.5rem;
    cursor: pointer;
    font-weight: 600;
    padding: 0.5rem;
  }

  .edit-btn svg {
    width: 1.1rem;
    height: 1.1rem;
  }

  .edit-btn:disabled {
    opacity: 0.7;
    cursor: not-allowed;
  }

  .alpaka-edit-form {
    margin-top: 1.5rem;
    padding-top: 1.5rem;
    border-top: 1px solid var(--taubenblau);
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .alpaka-edit-form .form-field {
    text-align: left;
  }

  .alpaka-edit-form .form-actions {
    justify-content: flex-start;
  }

  .edit-status {
    margin: 0;
    font-weight: 600;
    color: var(--taubenblau);
  }
</style>