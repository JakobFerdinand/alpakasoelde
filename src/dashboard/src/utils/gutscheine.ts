import { formatCurrency, formatDate } from './format';

export type Gutschein = {
  gutscheinnummer?: string;
  kaufdatum?: string;
  betrag?: number | string | null;
  eingeloestAm?: string | null;
  Gutscheinnummer?: string;
  Kaufdatum?: string;
  Betrag?: number | string | null;
  EingeloestAm?: string | null;
};

export type NormalizedGutschein = {
  gutscheinnummer: string;
  kaufdatum: string;
  betrag: number | string | null;
  eingeloestAm: string | null;
};

export const normalizeGutschein = (gutschein: Gutschein): NormalizedGutschein => {
  const uppercase = gutschein ?? {};

  return {
    gutscheinnummer: gutschein?.gutscheinnummer ?? uppercase.Gutscheinnummer ?? '',
    kaufdatum: gutschein?.kaufdatum ?? uppercase.Kaufdatum ?? '',
    betrag: gutschein?.betrag ?? uppercase.Betrag ?? null,
    eingeloestAm: gutschein?.eingeloestAm ?? uppercase.EingeloestAm ?? null
  };
};

export const normalizeGutscheine = (gutscheine: Gutschein[] | null | undefined): NormalizedGutschein[] => {
  if (!Array.isArray(gutscheine)) return [];
  return gutscheine.map(normalizeGutschein);
};

export const normalizeAmount = (value?: number | string | null) => {
  const amount = Number(value);
  return Number.isFinite(amount) ? amount : 0;
};

export const calculateGutscheinSums = (gutscheine: NormalizedGutschein[]) => {
  return gutscheine.reduce(
    (sum, gutschein) => {
      const amount = normalizeAmount(gutschein.betrag);

      return {
        total: sum.total + amount,
        open: sum.open + (gutschein.eingeloestAm ? 0 : amount)
      };
    },
    { total: 0, open: 0 }
  );
};

export const renderGutscheinTableMarkup = (
  gutscheine: NormalizedGutschein[],
  options: { showTotals?: boolean } = {}
) => {
  const showTotals = options.showTotals ?? true;

  if (!gutscheine || gutscheine.length === 0) {
    return '<p class="leer">Noch keine Gutscheine erfasst.</p>';
  }

  const { total, open } = calculateGutscheinSums(gutscheine);

  const header = `
    <thead>
      <tr class="gutschein-kopf">
        <th scope="col">Gutscheinnummer</th>
        <th scope="col">Kaufdatum</th>
        <th scope="col">Betrag</th>
        <th scope="col">Eingelöst am</th>
      </tr>
    </thead>
  `;

  const rows = gutscheine
    .map((gutschein) => {
      const formattedDate = formatDate(gutschein.kaufdatum);
      const formattedAmount = formatCurrency(gutschein.betrag);
      const hasRedemptionDate = Boolean(gutschein.eingeloestAm);
      const redemptionCell = hasRedemptionDate
        ? formatDate(gutschein.eingeloestAm)
        : `<button type="button" class="link-button" data-gutscheinnummer="${gutschein.gutscheinnummer}">Einlösen</button>`;

      return `
        <tr class="gutschein-zeile">
          <td>${gutschein.gutscheinnummer || '—'}</td>
          <td>${formattedDate}</td>
          <td>${formattedAmount}</td>
          <td>${redemptionCell}</td>
        </tr>
      `;
    })
    .join('');

  const totals = !showTotals
    ? ''
    : `
      <tfoot>
        <tr class="gutschein-summe">
          <td colspan="2" aria-hidden="true"></td>
          <td class="gutschein-summe-zelle">
            <span class="gutschein-summe-label">Verkauft</span>
            <span class="gutschein-summe-betrag">${formatCurrency(total)}</span>
          </td>
          <td class="gutschein-summe-zelle">
            <span class="gutschein-summe-label">Offen</span>
            <span class="gutschein-summe-betrag">${formatCurrency(open)}</span>
          </td>
        </tr>
      </tfoot>
    `;

  return `
    <table class="gutschein-tabelle" aria-label="Gutscheinliste">
      ${header}
      <tbody>
        ${rows}
      </tbody>
      ${totals}
    </table>
  `;
};

export const renderGutscheinListe = (
  container: HTMLElement | null,
  gutscheine: Gutschein[] | NormalizedGutschein[],
  options: { onRedeemClick?: (gutschein: NormalizedGutschein) => void; showTotals?: boolean } = {}
) => {
  if (!container) return;

  const { showTotals = true } = options;
  const normalisierteGutscheine = normalizeGutscheine(gutscheine as Gutschein[]);
  container.classList.remove('ladezustand');
  container.innerHTML = renderGutscheinTableMarkup(normalisierteGutscheine, { showTotals });

  if (!options.onRedeemClick) return;

  container.querySelectorAll('[data-gutscheinnummer]').forEach((button) => {
    button.addEventListener('click', () => {
      const gutschein = normalisierteGutscheine.find(
        (eintrag) => eintrag.gutscheinnummer === (button as HTMLButtonElement).dataset.gutscheinnummer
      );

      if (gutschein) {
        options.onRedeemClick?.(gutschein);
      }
    });
  });
};
