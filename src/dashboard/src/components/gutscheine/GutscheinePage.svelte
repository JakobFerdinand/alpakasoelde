<script lang="ts">
  import { onMount } from 'svelte';
  import Card from '../ui/Card.svelte';
  import FormField from '../ui/FormField.svelte';
  import GutscheinListe from './GutscheinListe.svelte';
  import { apiRequest, RequestGate } from '../../utils/api';
  import {
    normalizeGutschein,
    normalizeGutscheine,
    type Gutschein,
    type GutscheinRaw,
  } from '../../utils/gutschein';

  const listGate = new RequestGate();

  let gutscheine = $state<Gutschein[] | null>(null);
  let listFehler = $state('');
  let status = $state('');
  let statusIsError = $state(false);
  let sendenDisabled = $state(false);
  let sendenText = $state('Gutschein speichern');

  let gutscheinnummer = $state('');
  let kaufdatum = $state('');
  let betrag = $state('');
  let verkauftAn = $state('');
  let eingeloestAm = $state('');

  let dialog = $state<HTMLDialogElement>();
  let dialogOpen = $state(false);
  let aktuellerEinloeseGutschein = $state<Gutschein | null>(null);
  let einloesenDatum = $state('');
  let einloesenStatus = $state('');
  let einloesend = $state(false);

  $effect(() => {
    if (!dialog) return;
    const isOpen = dialog.open;
    if (dialogOpen && !isOpen) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', 'true');
      }
    } else if (!dialogOpen && isOpen) {
      dialog.close();
    }
  });

  const statusAnzeigen = (nachricht: string, istFehler = false) => {
    status = nachricht;
    statusIsError = istFehler;
  };

  const statusZuruecksetzen = () => {
    status = '';
    statusIsError = false;
  };

  const listeRendern = (liste: Gutschein[]) => {
    gutscheine = liste;
  };

  async function gutscheineLaden() {
    const signal = listGate.start();
    gutscheine = null;
    listFehler = '';
    const outcome = await apiRequest<unknown>('/api/gutscheine', {
      signal,
      fallback: 'Gutscheine konnten nicht geladen werden.',
    });
    if (!listGate.isCurrent(signal)) return;
    if (outcome.ok) {
      listeRendern(normalizeGutscheine(outcome.data as GutscheinRaw[]));
    } else if (outcome.kind !== 'aborted') {
      console.error(outcome.message);
      listFehler = 'Gutscheine konnten nicht geladen werden.';
    }
  }

  async function onGutscheinSubmit(event: SubmitEvent) {
    event.preventDefault();
    statusZuruecksetzen();

    if (!kaufdatum || !betrag) {
      statusAnzeigen('Bitte Kaufdatum und Betrag ausfüllen.', true);
      return;
    }

    sendenDisabled = true;
    sendenText = 'Speichern...';

    const outcome = await apiRequest<unknown>('/api/gutscheine', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gutscheinnummer: gutscheinnummer?.trim() || undefined,
        kaufdatum,
        betrag: Number(betrag),
        eingeloestAm: eingeloestAm || null,
        verkauftAn: verkauftAn?.trim() || undefined,
      }),
      fallback: 'Gutschein konnte nicht gespeichert werden.',
    });

    if (!outcome.ok) {
      if (outcome.kind !== 'aborted') {
        console.error(outcome.message);
        statusAnzeigen(outcome.message, true);
      }
    } else {
      gutscheinnummer = '';
      kaufdatum = '';
      betrag = '';
      eingeloestAm = '';
      verkauftAn = '';
      statusAnzeigen(
        `Gutschein ${normalizeGutschein(outcome.data as GutscheinRaw).gutscheinnummer} wurde gespeichert.`,
      );
      await gutscheineLaden();
    }

    sendenDisabled = false;
    sendenText = 'Gutschein speichern';
  }

  function openRedeem(gutschein: Gutschein) {
    if (!gutschein?.gutscheinnummer) {
      statusAnzeigen('Die Gutscheinnummer fehlt.', true);
      return;
    }
    aktuellerEinloeseGutschein = gutschein;
    einloesenDatum = '';
    einloesenStatus = '';
    dialogOpen = true;
  }

  function closeRedeem() {
    dialogOpen = false;
  }

  function onDialogClose() {
    dialogOpen = false;
    aktuellerEinloeseGutschein = null;
    einloesenStatus = '';
  }

  async function onRedeemSubmit(event: SubmitEvent) {
    event.preventDefault();
    if (!aktuellerEinloeseGutschein) return;

    if (!einloesenDatum) {
      einloesenStatus = 'Bitte Einlösedatum angeben.';
      return;
    }

    einloesenStatus = '';
    einloesend = true;

    const outcome = await apiRequest<unknown>(
      `/api/gutscheine/${encodeURIComponent(aktuellerEinloeseGutschein.gutscheinnummer)}/einloesen`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ eingeloestAm: einloesenDatum }),
        fallback: 'Gutschein konnte nicht eingelöst werden.',
      },
    );

    if (!outcome.ok) {
      if (outcome.kind !== 'aborted') {
        console.error(outcome.message);
        einloesenStatus = outcome.message;
      }
    } else {
      statusAnzeigen(`Gutschein ${aktuellerEinloeseGutschein.gutscheinnummer} wurde eingelöst.`);
      closeRedeem();
      await gutscheineLaden();
    }

    einloesend = false;
  }

  onMount(() => {
    gutscheineLaden();
    return () => listGate.dispose();
  });
</script>

<section class="gutscheine section">
  <div class="container">
    <a class="back-link" href="/">&larr; Zurück zur Übersicht</a>
    <div class="gutscheine-grid">
      <Card
        eyebrow="Neu"
        title="Gutschein erfassen"
        subtitle="Verkäufe dokumentieren und optional das Einlösedatum festhalten."
      >
        <form id="gutschein-formular" class="gutschein-formular" onsubmit={onGutscheinSubmit}>
          <FormField
            label="Gutscheinnummer (optional)"
            id="gutscheinnummer"
            hint="Wird automatisch vergeben, wenn das Feld leer bleibt."
          >
            <input
              id="gutscheinnummer"
              name="gutscheinnummer"
              type="text"
              inputmode="numeric"
              placeholder="z. B. 202501"
              bind:value={gutscheinnummer}
            />
          </FormField>
          <FormField label="Kaufdatum" id="kaufdatum" required>
            <input id="kaufdatum" name="kaufdatum" type="date" required bind:value={kaufdatum} />
          </FormField>
          <FormField label="Betrag (EUR)" id="betrag" required>
            <input
              id="betrag"
              name="betrag"
              type="number"
              step="0.01"
              min="0.01"
              required
              bind:value={betrag}
            />
          </FormField>
          <FormField
            label="Verkauft an (optional)"
            id="verkauftAn"
            hint="Optional: Name oder Kontakt des Käufers."
          >
            <input
              id="verkauftAn"
              name="verkauftAn"
              type="text"
              inputmode="text"
              bind:value={verkauftAn}
            />
          </FormField>
          <FormField
            label="Eingelöst am (optional)"
            id="eingeloestAm"
            hint="Nur ausfüllen, wenn der Gutschein bereits eingelöst wurde."
          >
            <input id="eingeloestAm" name="eingeloestAm" type="date" bind:value={eingeloestAm} />
          </FormField>
          <button id="senden-button" type="submit" class="primary-button" disabled={sendenDisabled}
            >{sendenText}</button
          >
          {#if status}
            <p id="gutschein-status" class="status-message" class:error={statusIsError}>{status}</p>
          {/if}
        </form>
      </Card>

      <GutscheinListe
        {gutscheine}
        fehler={listFehler}
        onRedeem={openRedeem}
        onRefresh={gutscheineLaden}
      />
    </div>

    <dialog bind:this={dialog} class="modal" onclose={onDialogClose}>
      <form class="dialog-content" onsubmit={onRedeemSubmit}>
        <p class="card-eyebrow">Gutschein einlösen</p>
        <h3>Gutschein {aktuellerEinloeseGutschein?.gutscheinnummer ?? ''}</h3>
        <FormField label="Eingelöst am" id="gutschein-einloesen-datum" required>
          <input
            id="gutschein-einloesen-datum"
            name="eingeloestAm"
            type="date"
            required
            bind:value={einloesenDatum}
            min={aktuellerEinloeseGutschein?.kaufdatum || undefined}
          />
        </FormField>
        {#if einloesenStatus}
          <p class="status-message error">{einloesenStatus}</p>
        {/if}
        <div class="dialog-actions">
          <button type="button" class="ghost-button" onclick={closeRedeem}>Abbrechen</button>
          <button type="submit" class="primary-button" disabled={einloesend}>
            {einloesend ? 'Einlösen...' : 'Gutschein einlösen'}
          </button>
        </div>
      </form>
    </dialog>
  </div>
</section>

<style>
  .gutscheine {
    background-color: var(--schurwolle);
    color: var(--taubenblau);
  }

  .gutscheine-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 1.5rem;
    margin-top: 1rem;
  }

  .gutschein-formular {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }
</style>
