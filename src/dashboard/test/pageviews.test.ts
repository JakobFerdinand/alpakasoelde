import { describe, expect, test } from 'vitest';
import {
  formatAxisLabel,
  formatPeriodLabel,
  granularityForDays,
  normalizePageViewStats,
  normalizeStatsRange,
  normalizeStatsWindow,
  statsQueryParams,
  toAudienceRows,
  toSeriesRows,
} from '../src/utils/pageviews';

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

describe('normalizeStatsWindow', () => {
  test('the window resolves days and engages a fully valid custom range', () => {
    expect(normalizeStatsWindow({ days: 7, from: null, to: null })).toEqual({
      days: 7,
      from: null,
      to: null,
    });
    expect(normalizeStatsWindow({ days: 7, from: '2026-08-01', to: '2026-08-20' })).toEqual({
      days: 20,
      from: '2026-08-01',
      to: '2026-08-20',
    });
  });

  test('hour availability is judged against the window, not the granularity-capped days', () => {
    expect(normalizeStatsWindow({ days: 90, from: null, to: null }).days).toBe(90);
    expect(normalizeStatsWindow({ days: 7, from: '2026-08-01', to: '2026-09-30' }).days).toBe(61);
  });
});

describe('normalizeStatsRange', () => {
  test('keeps positive days and passes a valid granularity through', () => {
    expect(normalizeStatsRange({ days: 7, granularity: 'day', from: null, to: null })).toEqual({
      days: 7,
      from: null,
      to: null,
      granularity: 'day',
    });
  });

  test('falls back to the default window when days are missing or invalid', () => {
    expect(normalizeStatsRange({ days: 0, granularity: 'day', from: null, to: null })).toEqual({
      days: 28,
      from: null,
      to: null,
      granularity: 'day',
    });
    expect(normalizeStatsRange({ days: -5, granularity: 'day', from: null, to: null })).toEqual({
      days: 28,
      from: null,
      to: null,
      granularity: 'day',
    });
    expect(
      normalizeStatsRange({ days: Number.NaN, granularity: 'day', from: null, to: null }),
    ).toEqual({
      days: 28,
      from: null,
      to: null,
      granularity: 'day',
    });
  });

  test('caps the requested lookback at 180 days like the server', () => {
    expect(normalizeStatsRange({ days: 400, granularity: 'week', from: null, to: null }).days).toBe(
      180,
    );
  });

  test('caps hour granularity at 28 days like the server', () => {
    expect(normalizeStatsRange({ days: 90, granularity: 'hour', from: null, to: null })).toEqual({
      days: 28,
      from: null,
      to: null,
      granularity: 'hour',
    });
    expect(normalizeStatsRange({ days: 7, granularity: 'hour', from: null, to: null }).days).toBe(
      7,
    );
  });

  test('falls back to week when the granularity is unknown', () => {
    expect(
      normalizeStatsRange({ days: 7, granularity: 'quartal', from: null, to: null }).granularity,
    ).toBe('week');
  });

  test('a valid custom range overrides days and is echoed for the query', () => {
    expect(
      normalizeStatsRange({ days: 7, granularity: 'day', from: '2026-08-01', to: '2026-08-20' }),
    ).toEqual({ days: 20, from: '2026-08-01', to: '2026-08-20', granularity: 'day' });
  });

  test('a custom range beyond 180 days is capped', () => {
    expect(
      normalizeStatsRange({ days: 7, granularity: 'day', from: '2025-01-01', to: '2026-06-06' })
        .days,
    ).toBe(180);
  });

  test('an invalid or inverted custom range falls back to the days window', () => {
    expect(
      normalizeStatsRange({ days: 5, granularity: 'day', from: '2026-08-20', to: '2026-08-01' }),
    ).toEqual({ days: 5, from: null, to: null, granularity: 'day' });
    expect(
      normalizeStatsRange({ days: 5, granularity: 'day', from: null, to: '2026-08-01' }),
    ).toEqual({
      days: 5,
      from: null,
      to: null,
      granularity: 'day',
    });
    expect(
      normalizeStatsRange({ days: 5, granularity: 'day', from: 'gestern', to: '2026-08-01' }),
    ).toEqual({ days: 5, from: null, to: null, granularity: 'day' });
  });

  test('hour granularity keeps the custom range but caps the informational days', () => {
    expect(
      normalizeStatsRange({ days: 7, granularity: 'hour', from: '2026-08-01', to: '2026-09-30' }),
    ).toEqual({ days: 28, from: '2026-08-01', to: '2026-09-30', granularity: 'hour' });
  });
});

describe('granularityForDays', () => {
  test('demotes hour to day beyond the 28-day limit', () => {
    expect(granularityForDays('hour', 90)).toBe('day');
    expect(granularityForDays('hour', 28)).toBe('hour');
    expect(granularityForDays('day', 90)).toBe('day');
    expect(granularityForDays('week', 180)).toBe('week');
  });
});

describe('statsQueryParams', () => {
  test('a days-based range sends days plus granularity and groupBy', () => {
    const params = statsQueryParams({ days: 7, from: null, to: null, granularity: 'day' }, 'path');
    expect(params.get('days')).toBe('7');
    expect(params.get('granularity')).toBe('day');
    expect(params.get('groupBy')).toBe('path');
    expect(params.has('from')).toBe(false);
    expect(params.has('to')).toBe(false);
  });

  test('a custom range sends from and to instead of days', () => {
    const params = statsQueryParams(
      { days: 20, from: '2026-08-01', to: '2026-08-20', granularity: 'week' },
      'device',
    );
    expect(params.get('from')).toBe('2026-08-01');
    expect(params.get('to')).toBe('2026-08-20');
    expect(params.get('granularity')).toBe('week');
    expect(params.get('groupBy')).toBe('device');
    expect(params.has('days')).toBe(false);
  });
});

describe('toSeriesRows / toAudienceRows', () => {
  test('flattens series into chart rows with a Gesamt group fallback', () => {
    const stats = normalizePageViewStats({
      series: [
        { period: '2026-09-28', group: '/', count: 3 },
        { period: '2026-09-28', group: null, count: 9 },
      ],
    });

    expect(toSeriesRows(stats)).toEqual([
      { Period: '2026-09-28', Group: '/', Count: 3 },
      { Period: '2026-09-28', Group: 'Gesamt', Count: 9 },
    ]);
    expect(toSeriesRows(null)).toEqual([]);
  });

  test('splits the audience series into Besucher and Sitzungen rows', () => {
    const stats = normalizePageViewStats({
      audienceSeries: [{ period: '2026-09-28', visitors: 5, sessions: 7 }],
    });

    expect(toAudienceRows(stats)).toEqual([
      { Period: '2026-09-28', Group: 'Besucher', Count: 5 },
      { Period: '2026-09-28', Group: 'Sitzungen', Count: 7 },
    ]);
    expect(toAudienceRows(null)).toEqual([]);
  });
});

describe('formatPeriodLabel / formatAxisLabel', () => {
  test('labels week periods with their Monday', () => {
    expect(formatPeriodLabel('2026-09-28', 'week')).toBe('Woche ab 28.09.2026');
  });

  test('labels day periods as plain dates', () => {
    expect(formatPeriodLabel('2026-09-28', 'day')).toBe('28.09.2026');
  });

  test('labels hour periods with date and time', () => {
    expect(formatPeriodLabel('2026-09-28T14:00', 'hour')).toBe('28.09. 14:00');
  });

  test('axis labels drop the week prefix', () => {
    expect(formatAxisLabel('2026-09-28')).toBe('28.09.2026');
    expect(formatAxisLabel('2026-09-28T14:00')).toBe('28.09. 14:00');
  });
});
