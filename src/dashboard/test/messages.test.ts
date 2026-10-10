import { describe, expect, test } from 'vitest';
import { normalizeMessage, normalizeMessageStats, normalizeMessages } from '../src/utils/messages';

describe('normalizeMessage', () => {
  test('reads the PascalCase answer of /api/messages', () => {
    const message = normalizeMessage({
      Id: '10',
      Name: 'Anna Muster',
      Email: 'anna@example.at',
      Phone: '+43 664 0000000',
      Message: 'Guten Tag',
      Timestamp: '2026-10-01T10:00:00Z',
      IsSpam: true,
    });

    expect(message).toEqual({
      Id: '10',
      Name: 'Anna Muster',
      Email: 'anna@example.at',
      Phone: '+43 664 0000000',
      Message: 'Guten Tag',
      Timestamp: '2026-10-01T10:00:00Z',
      IsSpam: true,
    });
  });

  test('also reads camelCase payloads and defaults the rest', () => {
    const message = normalizeMessage({ id: '11', name: 'Berta', isSpam: false });

    expect(message).toEqual({
      Id: '11',
      Name: 'Berta',
      Email: '',
      Phone: '',
      Message: '',
      Timestamp: '',
      IsSpam: false,
    });
  });

  test('normalizeMessages skips anything that is not an array', () => {
    expect(normalizeMessages(null)).toEqual([]);
    expect(normalizeMessages([])).toEqual([]);
    expect(normalizeMessages({ Id: '1' })).toEqual([]);
  });
});

describe('normalizeMessageStats', () => {
  test('reads the PascalCase answer incl. the series buckets', () => {
    const stats = normalizeMessageStats({
      Total: 12,
      Spam: 2,
      Legit: 10,
      OldCount: 1,
      Series: [
        { Period: '2026-W40', Spam: 1, Legit: 4 },
        { Period: '2026-W41', Spam: 1, Legit: 6 },
      ],
    });

    expect(stats).toEqual({
      Total: 12,
      Spam: 2,
      Legit: 10,
      OldCount: 1,
      Series: [
        { Period: '2026-W40', Spam: 1, Legit: 4 },
        { Period: '2026-W41', Spam: 1, Legit: 6 },
      ],
    });
  });

  test('defaults a truncated or wrongly cased payload', () => {
    expect(normalizeMessageStats(null)).toEqual({
      Total: 0,
      Spam: 0,
      Legit: 0,
      OldCount: 0,
      Series: [],
    });
    expect(normalizeMessageStats({ total: 5, series: 'keine liste' })).toEqual({
      Total: 5,
      Spam: 0,
      Legit: 0,
      OldCount: 0,
      Series: [],
    });
  });
});
