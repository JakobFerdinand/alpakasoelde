/**
 * Gutschein type definitions and normalization utilities.
 */

export interface Gutschein {
  gutscheinnummer: string;
  kaufdatum: string;
  betrag: number | string | null;
  eingeloestAm: string | null;
  verkauftAn: string | null;
}

export interface GutscheinRaw {
  gutscheinnummer?: string;
  kaufdatum?: string;
  betrag?: number | string | null;
  eingeloestAm?: string | null;
  verkauftAn?: string | null;
  Gutscheinnummer?: string;
  Kaufdatum?: string;
  Betrag?: number | string | null;
  EingeloestAm?: string | null;
  VerkauftAn?: string | null;
}

/**
 * Normalizes a gutschein object to handle both camelCase and PascalCase field names.
 */
export const normalizeGutschein = (gutschein: GutscheinRaw | null | undefined): Gutschein => {
  if (!gutschein) {
    return {
      gutscheinnummer: '',
      kaufdatum: '',
      betrag: null,
      eingeloestAm: null,
      verkauftAn: null
    };
  }

  return {
    gutscheinnummer: gutschein.gutscheinnummer ?? gutschein.Gutscheinnummer ?? '',
    kaufdatum: gutschein.kaufdatum ?? gutschein.Kaufdatum ?? '',
    betrag: gutschein.betrag ?? gutschein.Betrag ?? null,
    eingeloestAm: gutschein.eingeloestAm ?? gutschein.EingeloestAm ?? null,
    verkauftAn: gutschein.verkauftAn ?? gutschein.VerkauftAn ?? null
  };
};

/**
 * Normalizes an array of gutschein objects.
 */
export const normalizeGutscheine = (gutscheine: GutscheinRaw[] | null | undefined): Gutschein[] => {
  if (!Array.isArray(gutscheine)) return [];
  return gutscheine.map(normalizeGutschein);
};

/**
 * Suggests the next gutschein number based on existing gutscheine.
 */
export const suggestNextGutscheinnummer = (gutscheine: Gutschein[]): string => {
  const jahresPraefix = new Date().getFullYear().toString();
  const hoechsteEndung = gutscheine
    .map((g) => g.gutscheinnummer)
    .filter((nummer) => nummer?.startsWith(jahresPraefix))
    .map((nummer) => nummer?.slice(jahresPraefix.length) ?? '0')
    .map((endung) => (Number.isNaN(Number(endung)) ? 0 : Number(endung)))
    .reduce((max, aktuell) => Math.max(max, aktuell), 0);

  return `${jahresPraefix}${String(hoechsteEndung + 1).padStart(2, '0')}`;
};
