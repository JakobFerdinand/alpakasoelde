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
      verkauftAn: null,
    };
  }

  return {
    gutscheinnummer: gutschein.gutscheinnummer ?? gutschein.Gutscheinnummer ?? '',
    kaufdatum: gutschein.kaufdatum ?? gutschein.Kaufdatum ?? '',
    betrag: gutschein.betrag ?? gutschein.Betrag ?? null,
    eingeloestAm: gutschein.eingeloestAm ?? gutschein.EingeloestAm ?? null,
    verkauftAn: gutschein.verkauftAn ?? gutschein.VerkauftAn ?? null,
  };
};

/**
 * Normalizes an array of gutschein objects.
 */
export const normalizeGutscheine = (gutscheine: GutscheinRaw[] | null | undefined): Gutschein[] => {
  if (!Array.isArray(gutscheine)) return [];
  return gutscheine.map(normalizeGutschein);
};
