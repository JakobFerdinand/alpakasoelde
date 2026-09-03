<script lang="ts">
  import { ChevronDown, RotateCcw, Send, Sparkles } from '@lucide/svelte';

  type ToolTrace = {
    tool: string;
    arguments: string;
  };

  type ToolTraceRaw = {
    tool?: string;
    Tool?: string;
    arguments?: string;
    Arguments?: string;
  };

  type AskResultRaw = {
    reply?: string;
    Reply?: string;
    session?: unknown;
    Session?: unknown;
    tools?: ToolTraceRaw[];
    Tools?: ToolTraceRaw[];
  };

  type ProblemDetails = {
    title?: string;
    status?: number;
    detail?: string;
    Detail?: string;
  };

  type ChatMessage = {
    id: number;
    role: 'user' | 'assistant';
    text: string;
    tools: ToolTrace[];
  };

  const starterPrompts = [
    'Wie viele Besucher hatten wir letzte Woche?',
    'Welche Seite lief im letzten Monat am besten?',
    'Wie viele Gutscheine sind noch offen?',
    'Wann war das letzte Scheren?',
  ];

  let messages = $state<ChatMessage[]>([]);
  let session = $state<unknown>(null);
  let frage = $state('');
  let laedt = $state(false);
  let fehler = $state('');
  let naechsteId = 0;
  let eingabe = $state<HTMLTextAreaElement | null>(null);

  const kannSenden = $derived(!laedt && frage.trim().length > 0);
  const istLeer = $derived(messages.length === 0 && !laedt && !fehler);

  /**
   * Normalizes a tool trace, handling both camelCase and PascalCase field names.
   */
  const normalizeToolTrace = (eintrag: ToolTraceRaw | null | undefined): ToolTrace => ({
    tool: eintrag?.tool ?? eintrag?.Tool ?? '',
    arguments: eintrag?.arguments ?? eintrag?.Arguments ?? '',
  });

  const normalizeToolTraces = (eintraege: ToolTraceRaw[] | null | undefined): ToolTrace[] =>
    Array.isArray(eintraege) ? eintraege.map(normalizeToolTrace) : [];

  /** Puts the unanswered question back into the textarea and drops its pending bubble. */
  function zuruecksetzenNachFehler(gestellteFrage: string, meldung: string) {
    const letzte = messages[messages.length - 1];
    if (letzte?.role === 'user' && letzte.text === gestellteFrage) {
      messages = messages.slice(0, -1);
    }
    frage = gestellteFrage;
    fehler = meldung;
  }

  function neuesGespraech() {
    messages = [];
    session = null;
    fehler = '';
    frage = '';
    eingabe?.focus();
  }

  async function frageSenden(text: string) {
    const bereinigt = text.trim();
    if (!bereinigt || laedt) return;

    fehler = '';
    frage = '';
    messages = [...messages, { id: naechsteId++, role: 'user', text: bereinigt, tools: [] }];
    laedt = true;

    try {
      const antwort = await fetch('/api/assistant', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question: bereinigt, session }),
      });

      if (!antwort.ok) {
        const problem: ProblemDetails | null = await antwort.json().catch(() => null);
        zuruecksetzenNachFehler(
          bereinigt,
          problem?.detail || problem?.Detail || 'Die Frage konnte nicht beantwortet werden.',
        );
        return;
      }

      const daten: AskResultRaw = await antwort.json();
      const antwortText = daten.reply ?? daten.Reply ?? '';
      session = daten.session ?? daten.Session ?? null;
      messages = [
        ...messages,
        {
          id: naechsteId++,
          role: 'assistant',
          text: antwortText || 'Darauf habe ich keine Antwort gefunden.',
          tools: normalizeToolTraces(daten.tools ?? daten.Tools),
        },
      ];
    } catch (e) {
      console.error(e);
      zuruecksetzenNachFehler(
        bereinigt,
        'Der Assistent ist gerade nicht erreichbar. Bitte später noch einmal versuchen.',
      );
    } finally {
      laedt = false;
    }
  }

  function onSubmit(event: SubmitEvent) {
    event.preventDefault();
    frageSenden(frage);
  }

  function onKeydown(event: KeyboardEvent) {
    if (event.key !== 'Enter' || event.shiftKey) return;
    event.preventDefault();
    frageSenden(frage);
  }
</script>

<section class="assistent section">
  <div class="container">
    <header class="kopf">
      <div>
        <p class="card-eyebrow">Assistent</p>
        <h2 class="card-title">Fragen an die Daten</h2>
        <p class="card-subtitle">
          Fragen zu Besuchern, Nachrichten, Gutscheinen, Alpakas und Ereignissen — in ganzen Sätzen.
        </p>
      </div>
      {#if messages.length > 0}
        <button type="button" class="ghost-button neu" onclick={neuesGespraech} disabled={laedt}>
          <RotateCcw aria-hidden="true" />
          Neues Gespräch
        </button>
      {/if}
    </header>

    <div class="verlauf" aria-live="polite" aria-busy={laedt}>
      {#if istLeer}
        <div class="leerzustand">
          <Sparkles aria-hidden="true" />
          <p class="leer-titel">Womit kann ich helfen?</p>
          <p class="leer-text">Zum Beispiel:</p>
          <ul class="vorschlaege">
            {#each starterPrompts as vorschlag}
              <li>
                <button type="button" class="vorschlag" onclick={() => frageSenden(vorschlag)}>
                  {vorschlag}
                </button>
              </li>
            {/each}
          </ul>
        </div>
      {:else}
        <ol class="blasen">
          {#each messages as nachricht (nachricht.id)}
            <li class="blase" class:user={nachricht.role === 'user'}>
              <div class="blasen-text">{nachricht.text}</div>
              {#if nachricht.role === 'assistant' && nachricht.tools.length > 0}
                <details class="daten">
                  <summary>
                    <ChevronDown aria-hidden="true" />
                    <span>Verwendete Daten ({nachricht.tools.length})</span>
                  </summary>
                  <ul class="daten-liste">
                    {#each nachricht.tools as spur, index (index)}
                      <li>
                        <code class="werkzeug">{spur.tool || 'unbekannt'}</code>
                        {#if spur.arguments}
                          <code class="argumente">{spur.arguments}</code>
                        {/if}
                      </li>
                    {/each}
                  </ul>
                </details>
              {/if}
            </li>
          {/each}
        </ol>
      {/if}

      {#if laedt}
        <p class="denkt loading-text">
          <span class="loading">Der Assistent denkt nach…</span>
        </p>
        <p class="denkt-hinweis">Das kann bis zu 40 Sekunden dauern.</p>
      {/if}

      {#if fehler}
        <p class="error" role="alert">{fehler}</p>
      {/if}
    </div>

    <form class="eingabe" onsubmit={onSubmit}>
      <label class="sr-only" for="assistent-frage">Frage an den Assistenten</label>
      <textarea
        id="assistent-frage"
        bind:this={eingabe}
        bind:value={frage}
        onkeydown={onKeydown}
        rows="2"
        disabled={laedt}
        placeholder="Frage stellen — Enter sendet, Umschalt+Enter macht einen Zeilenumbruch."
      ></textarea>
      <button type="submit" class="primary-button senden" disabled={!kannSenden}>
        <Send aria-hidden="true" />
        <span>Senden</span>
      </button>
    </form>
  </div>
</section>

<style>
  .assistent .container {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .kopf {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 1rem;
  }

  .kopf .card-subtitle {
    margin-bottom: 0;
  }

  .neu {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    flex-shrink: 0;
  }

  .neu :global(svg) {
    width: 1rem;
    height: 1rem;
  }

  .verlauf {
    border: 1px solid rgba(0, 32, 73, 0.08);
    border-radius: 0.75rem;
    background-color: var(--weiss);
    padding: 1rem;
    min-height: 320px;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .leerzustand {
    margin: auto 0;
    text-align: center;
    color: var(--taubenblau);
  }

  .leerzustand :global(svg) {
    width: 2rem;
    height: 2rem;
    color: var(--bluetenhonig);
  }

  .leer-titel {
    margin: 0.5rem 0 0;
    font-size: 1.15rem;
    font-weight: 600;
  }

  .leer-text {
    margin: 0.25rem 0 1rem;
    color: rgba(0, 32, 73, 0.6);
  }

  .vorschlaege {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 0.5rem;
  }

  .vorschlag {
    background-color: var(--schurwolle);
    border: 1px solid rgba(0, 32, 73, 0.15);
    border-radius: 999px;
    padding: 0.5rem 0.9rem;
    color: var(--taubenblau);
    font-family: inherit;
    font-size: 0.95rem;
    cursor: pointer;
  }

  .vorschlag:hover,
  .vorschlag:focus-visible {
    background-color: var(--auwasser);
    outline: none;
  }

  .blasen {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .blase {
    max-width: min(46rem, 90%);
    align-self: flex-start;
    background-color: var(--schurwolle);
    border: 1px solid rgba(0, 32, 73, 0.08);
    border-radius: 0.75rem;
    padding: 0.75rem 0.9rem;
  }

  .blase.user {
    align-self: flex-end;
    background-color: var(--taubenblau);
    border-color: var(--taubenblau);
    color: var(--schurwolle);
  }

  .blasen-text {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    line-height: 1.5;
  }

  .daten {
    margin-top: 0.6rem;
    border-top: 1px solid rgba(0, 32, 73, 0.1);
    padding-top: 0.5rem;
  }

  .daten summary {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    cursor: pointer;
    font-size: 0.9rem;
    font-weight: 600;
    color: var(--weidegruen);
    list-style: none;
  }

  .daten summary::-webkit-details-marker {
    display: none;
  }

  .daten summary :global(svg) {
    width: 1rem;
    height: 1rem;
    transition: transform 0.15s ease;
  }

  .daten[open] summary :global(svg) {
    transform: rotate(180deg);
  }

  .daten-liste {
    list-style: none;
    margin: 0.5rem 0 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  .daten-liste li {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 0.4rem;
    font-size: 0.85rem;
  }

  .werkzeug {
    font-weight: 700;
    color: var(--taubenblau);
  }

  .argumente {
    color: rgba(0, 32, 73, 0.7);
    overflow-wrap: anywhere;
  }

  .denkt {
    margin: 0;
    color: var(--weidegruen);
  }

  .denkt-hinweis {
    margin: 0;
    font-size: 0.9rem;
    color: rgba(0, 32, 73, 0.6);
  }

  .eingabe {
    display: flex;
    align-items: flex-end;
    gap: 0.75rem;
  }

  .eingabe textarea {
    flex: 1;
    padding: 0.75rem 0.85rem;
    border: 1px solid rgba(0, 32, 73, 0.15);
    border-radius: 0.6rem;
    font-family: inherit;
    font-size: 1rem;
    background-color: var(--weiss);
    color: var(--taubenblau);
    resize: vertical;
  }

  .eingabe textarea:focus {
    outline: 2px solid var(--taubenblau);
    outline-offset: 1px;
  }

  .eingabe textarea:disabled {
    opacity: 0.7;
  }

  .senden {
    display: inline-flex;
    align-items: center;
    gap: 0.45rem;
  }

  .senden :global(svg) {
    width: 1.1rem;
    height: 1.1rem;
  }

  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }

  @media (max-width: 720px) {
    .kopf {
      flex-direction: column;
      align-items: flex-start;
    }

    .eingabe {
      flex-direction: column;
      align-items: stretch;
    }

    .senden {
      justify-content: center;
    }

    .blase {
      max-width: 100%;
    }
  }
</style>
