<script lang="ts">
  import { onMount } from 'svelte';
  import Card from './Card.svelte';
  import FormField from './FormField.svelte';
  import GutscheinListe from './GutscheinListe.svelte';
  import {
    normalizeGutschein,
    suggestNextGutscheinnummer,
    type Gutschein,
    type GutscheinRaw,
  } from '../utils/gutschein';

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

  const naechsteNummerVorschlagen = (liste: Gutschein[]) => {
    if (!gutscheinnummer) {
      gutscheinnummer = suggestNextGutscheinnummer(liste);
    }
  };

  const listeRendern = (liste: Gutschein[]) => {
    gutscheine = liste;
    naechsteNummerVorschlagen(liste);
  };

  async function gutscheineLaden() {
    gutscheine = null;
    listFehler = '';
    try {
      const antwort = await fetch('/api/gutscheine');
      if (!antwort.ok) throw new Error('Fehler beim Laden der Gutscheine');
      const daten: GutscheinRaw[] = await antwort.json();
      const liste = Array.isArray(daten) ? daten.map(normalizeGutschein) : [];
      listeRendern(liste);
    } catch (fehler) {
      console.error(fehler);
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

    try {
      const antwort = await fetch('/api/gutscheine', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          gutscheinnummer: gutscheinnummer?.trim() || undefined,
          kaufdatum,
          betrag: Number(betrag),
          eingeloestAm: eingeloestAm || null,
          verkauftAn: verkauftAn?.trim() || undefined,
        }),
      });

      if (!antwort.ok) {
        const fehler = await antwort.json().catch(() => null);
        throw new Error(fehler?.detail || 'Gutschein konnte nicht gespeichert werden.');
      }

      const ergebnis = await antwort.json();
      gutscheinnummer = '';
      kaufdatum = '';
      betrag = '';
      eingeloestAm = '';
      verkauftAn = '';
      naechsteNummerVorschlagen([]);
      statusAnzeigen(`Gutschein ${ergebnis.gutscheinnummer ?? ''} wurde gespeichert.`);
      await gutscheineLaden();
    } catch (fehler) {
      console.error(fehler);
      statusAnzeigen(fehler instanceof Error ? fehler.message : 'Gutschein konnte nicht gespeichert werden.', true);
    } finally {
      sendenDisabled = false;
      sendenText = 'Gutschein speichern';
    }
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

    try {
      const antwort = await fetch(
        `/api/gutscheine/${encodeURIComponent(aktuellerEinloeseGutschein.gutscheinnummer)}/einloesen`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ eingeloestAm: einloesenDatum }),
        },
      );

      if (!antwort.ok) {
        const fehler = await antwort.json().catch(() => null);
        throw new Error(fehler?.detail || 'Gutschein konnte nicht eingelöst werden.');
      }

      statusAnzeigen(`Gutschein ${aktuellerEinloeseGutschein.gutscheinnummer} wurde eingelöst.`);
      closeRedeem();
      await gutscheineLaden();
    } catch (fehler) {
      console.error(fehler);
      einloesenStatus = fehler instanceof Error ? fehler.message : 'Gutschein konnte nicht eingelöst werden.';
    } finally {
      einloesend = false;
    }
  }

  onMount(gutscheineLaden);
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
            label="Gutscheinnummer"
            id="gutscheinnummer"
            hint="Standardmäßig wird die nächste Nummer vorgeschlagen. Bei Bedarf überschreiben."
          >
            <input id="gutscheinnummer" name="gutscheinnummer" type="text" inputmode="numeric" placeholder="z. B. 202501" bind:value={gutscheinnummer} />
          </FormField>
          <FormField label="Kaufdatum" id="kaufdatum" required>
            <input id="kaufdatum" name="kaufdatum" type="date" required bind:value={kaufdatum} />
          </FormField>
          <FormField label="Betrag (EUR)" id="betrag" required>
            <input id="betrag" name="betrag" type="number" step="0.01" min="0.01" required bind:value={betrag} />
          </FormField>
          <FormField label="Verkauft an (optional)" id="verkauftAn" hint="Optional: Name oder Kontakt des Käufers.">
            <input id="verkauftAn" name="verkauftAn" type="text" inputmode="text" bind:value={verkauftAn} />
          </FormField>
          <FormField
            label="Eingelöst am (optional)"
            id="eingeloestAm"
            hint="Nur ausfüllen, wenn der Gutschein bereits eingelöst wurde."
          >
            <input id="eingeloestAm" name="eingeloestAm" type="date" bind:value={eingeloestAm} />
          </FormField>
          <button id="senden-button" type="submit" class="primary-button" disabled={sendenDisabled}>{sendenText}</button>
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