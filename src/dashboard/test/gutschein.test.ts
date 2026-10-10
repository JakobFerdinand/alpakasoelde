import { describe, expect, test } from 'vitest';
import { normalizeGutscheine, type GutscheinRaw } from '../src/utils/gutschein';

describe('normalizeGutscheine', () => {
  test('accepts both API casings and fills the gaps with empty values', () => {
    // The API answers in camelCase on some routes and PascalCase on others.
    const raw: GutscheinRaw[] = [
      { gutscheinnummer: '202401', kaufdatum: '2024-03-01', betrag: 50, verkauftAn: 'Anna' },
      {
        Gutscheinnummer: '202402',
        Kaufdatum: '2024-04-01',
        Betrag: '75',
        EingeloestAm: '2024-05-01',
      },
      {},
    ];

    expect(normalizeGutscheine(raw)).toEqual([
      {
        gutscheinnummer: '202401',
        kaufdatum: '2024-03-01',
        betrag: 50,
        eingeloestAm: null,
        verkauftAn: 'Anna',
      },
      {
        gutscheinnummer: '202402',
        kaufdatum: '2024-04-01',
        betrag: '75',
        eingeloestAm: '2024-05-01',
        verkauftAn: null,
      },
      {
        gutscheinnummer: '',
        kaufdatum: '',
        betrag: null,
        eingeloestAm: null,
        verkauftAn: null,
      },
    ]);
  });

  test('returns an empty list when the fetch yielded no array', () => {
    expect(normalizeGutscheine(undefined)).toEqual([]);
    expect(normalizeGutscheine(null)).toEqual([]);
  });
});
