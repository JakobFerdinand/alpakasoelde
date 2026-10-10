import { describe, expect, test } from 'vitest';
import { normalizePageViewStats } from '../src/utils/pageviews';

describe('normalizePageViewStats', () => {
  test('reads the PascalCase answer of /api/pageviews/stats', () => {
    const stats = normalizePageViewStats({
      Total: 400,
      UniquePaths: 9,
      TopPaths: [
        { Path: '/', Count: 120 },
        { Path: '/gutscheine', Count: 80 },
      ],
      Devices: [{ Category: 'desktop', Count: 300 }],
      Origins: [{ Domain: 'google.com', Count: 150 }],
      Series: [{ Period: '2026-09-27T00:00:00Z', Group: '', Count: 12 }],
      Sessions: 60,
      Visitors: 55,
      Navigations: [{ Type: 'reload', Count: 8 }],
      AudienceSeries: [{ Period: '2026-09-27T00:00:00Z', Visitors: 20, Sessions: 22 }],
      Granularity: 'week',
      GroupBy: 'path',
    });

    expect(stats).toEqual({
      Total: 400,
      UniquePaths: 9,
      TopPaths: [
        { Path: '/', Count: 120 },
        { Path: '/gutscheine', Count: 80 },
      ],
      Devices: [{ Category: 'desktop', Count: 300 }],
      Origins: [{ Domain: 'google.com', Count: 150 }],
      Series: [{ Period: '2026-09-27T00:00:00Z', Group: '', Count: 12 }],
      Sessions: 60,
      Visitors: 55,
      Navigations: [{ Type: 'reload', Count: 8 }],
      AudienceSeries: [{ Period: '2026-09-27T00:00:00Z', Visitors: 20, Sessions: 22 }],
      Granularity: 'week',
      GroupBy: 'path',
    });
  });

  test('also reads camelCase payloads and defaults the rest', () => {
    const stats = normalizePageViewStats({ total: 7, series: [{ period: 'x', count: 2 }] });

    expect(stats).toEqual({
      Total: 7,
      UniquePaths: 0,
      TopPaths: [],
      Devices: [],
      Origins: [],
      Series: [{ Period: 'x', Group: null, Count: 2 }],
      Sessions: 0,
      Visitors: 0,
      Navigations: [],
      AudienceSeries: [],
      Granularity: 'day',
      GroupBy: 'total',
    });
  });

  test('keeps a null Group instead of inventing a string', () => {
    const stats = normalizePageViewStats({ series: [{ period: 'x', group: null, count: 1 }] });
    expect(stats.Series[0]?.Group).toBeNull();
  });
});
