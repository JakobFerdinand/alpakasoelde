/**
 * Casing-tolerant normalizers for the pageviews API.
 * The API answers PascalCase; readers keep their PascalCase result type
 * while the lookup itself accepts both spellings.
 */
import { pickCase } from './casing';

export type PathCount = { Path: string; Count: number };
export type DeviceCount = { Category: string; Count: number };
export type OriginCount = { Domain: string; Count: number };
export type Bucket = { Period: string; Group: string | null; Count: number };
export type AudienceBucket = { Period: string; Visitors: number; Sessions: number };
export type NavigationCount = { Type: string; Count: number };
export type Granularity = 'week' | 'day' | 'hour';
export type GroupBy = 'path' | 'device' | 'origin';
export type ChartType = 'bars-stacked' | 'bars-grouped' | 'line' | 'area';

export type StatsResult = {
  Total: number;
  UniquePaths: number;
  TopPaths: PathCount[];
  Devices: DeviceCount[];
  Origins: OriginCount[];
  Series: Bucket[];
  Sessions: number;
  Visitors: number;
  Navigations: NavigationCount[];
  AudienceSeries: AudienceBucket[];
  Granularity: Granularity;
  GroupBy: GroupBy | 'total';
};

const asString = (raw: unknown): string => (typeof raw === 'string' ? raw : '');

const asCount = (raw: unknown): number => (typeof raw === 'number' ? raw : 0);

const pathCounts = (raw: unknown): PathCount[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Path: asString(pickCase(entry, 'path')),
        Count: asCount(pickCase(entry, 'count')),
      }))
    : [];

const deviceCounts = (raw: unknown): DeviceCount[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Category: asString(pickCase(entry, 'category')),
        Count: asCount(pickCase(entry, 'count')),
      }))
    : [];

const originCounts = (raw: unknown): OriginCount[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Domain: asString(pickCase(entry, 'domain')),
        Count: asCount(pickCase(entry, 'count')),
      }))
    : [];

const buckets = (raw: unknown): Bucket[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Period: asString(pickCase(entry, 'period')),
        Group: pickCase<string | null>(entry, 'group') ?? null,
        Count: asCount(pickCase(entry, 'count')),
      }))
    : [];

const audienceBuckets = (raw: unknown): AudienceBucket[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Period: asString(pickCase(entry, 'period')),
        Visitors: asCount(pickCase(entry, 'visitors')),
        Sessions: asCount(pickCase(entry, 'sessions')),
      }))
    : [];

const navigationCounts = (raw: unknown): NavigationCount[] =>
  Array.isArray(raw)
    ? raw.map((entry) => ({
        Type: asString(pickCase(entry, 'type')),
        Count: asCount(pickCase(entry, 'count')),
      }))
    : [];

export const normalizePageViewStats = (raw: unknown): StatsResult => {
  const granularity = pickCase<Granularity>(raw, 'granularity');
  const groupBy = pickCase<StatsResult['GroupBy']>(raw, 'groupBy');
  return {
    Total: asCount(pickCase(raw, 'total')),
    UniquePaths: asCount(pickCase(raw, 'uniquePaths')),
    TopPaths: pathCounts(pickCase(raw, 'topPaths')),
    Devices: deviceCounts(pickCase(raw, 'devices')),
    Origins: originCounts(pickCase(raw, 'origins')),
    Series: buckets(pickCase(raw, 'series')),
    Sessions: asCount(pickCase(raw, 'sessions')),
    Visitors: asCount(pickCase(raw, 'visitors')),
    Navigations: navigationCounts(pickCase(raw, 'navigations')),
    AudienceSeries: audienceBuckets(pickCase(raw, 'audienceSeries')),
    Granularity: granularity ?? 'day',
    GroupBy: groupBy ?? 'total',
  };
};

/**
 * The one definition of the /pageviews/stats range rules, mirroring the
 * server's parsing in GetPageViewStats.Parse: max 180 days of lookback,
 * hour granularity only up to 28 days, a valid custom range overriding
 * the days window, and unknown granularities falling back to 'week'.
 */
export const MAX_STATS_DAYS = 180;
export const MAX_HOUR_GRANULARITY_DAYS = 28;
export const DEFAULT_STATS_DAYS = 28;

const DAY_MS = 86_400_000;

/** The lookback window (days ↔ custom from/to) before granularity rules apply. */
export type StatsWindow = {
  days: number;
  from: string | null;
  to: string | null;
};

export type StatsWindowInput = {
  days: number;
  from: string | null;
  to: string | null;
};

export type StatsRangeInput = StatsWindowInput & {
  granularity: string;
};

/** The query range after normalisation; also the shape of the request params. */
export type StatsRange = StatsWindow & {
  granularity: Granularity;
};

/** Days since 1970-01-01 for a yyyy-mm-dd string, or null when unparseable. */
const dayNumberOf = (value: string): number | null => {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? null : Math.round(parsed / DAY_MS);
};

/**
 * Resolves the requested window: a fully valid custom range (both dates
 * parseable, to >= from) overrides the days, otherwise the days apply
 * (capped like the server, defaulting to 28 when not positive).
 */
export const normalizeStatsWindow = (input: StatsWindowInput): StatsWindow => {
  let days =
    Number.isFinite(input.days) && input.days > 0
      ? Math.min(input.days, MAX_STATS_DAYS)
      : DEFAULT_STATS_DAYS;

  let from: string | null = null;
  let to: string | null = null;
  const fromDate = input.from === null ? null : dayNumberOf(input.from);
  const toDate = input.to === null ? null : dayNumberOf(input.to);
  if (fromDate !== null && toDate !== null && toDate >= fromDate) {
    days = Math.min(toDate - fromDate + 1, MAX_STATS_DAYS);
    from = input.from;
    to = input.to;
  }

  return { days, from, to };
};

export const normalizeStatsRange = (input: StatsRangeInput): StatsRange => {
  const granularities: Granularity[] = ['week', 'day', 'hour'];
  const granularity: Granularity = granularities.includes(input.granularity as Granularity)
    ? (input.granularity as Granularity)
    : 'week';

  const { days, from, to } = normalizeStatsWindow(input);
  return {
    days: granularity === 'hour' ? Math.min(days, MAX_HOUR_GRANULARITY_DAYS) : days,
    from,
    to,
    granularity,
  };
};

/** Hour granularity is only offered up to the 28-day cap; beyond it demote to day. */
export const granularityForDays = (granularity: Granularity, days: number): Granularity =>
  granularity === 'hour' && days > MAX_HOUR_GRANULARITY_DAYS ? 'day' : granularity;

/** Query params for one /pageviews/stats call; a custom range replaces days. */
export const statsQueryParams = (range: StatsRange, groupBy: string): URLSearchParams => {
  const params = new URLSearchParams({ granularity: range.granularity, groupBy });
  if (range.from !== null && range.to !== null) {
    params.set('from', range.from);
    params.set('to', range.to);
  } else {
    params.set('days', String(range.days));
  }
  return params;
};

/** Chart row as layerchart consumes it; a null series group reads as Gesamt. */
export type SeriesRow = { Period: string; Group: string; Count: number };

export const toSeriesRows = (stats: StatsResult | null): SeriesRow[] =>
  (stats?.Series ?? []).map((row) => ({
    Period: row.Period,
    Group: row.Group ?? 'Gesamt',
    Count: row.Count,
  }));

export const toAudienceRows = (stats: StatsResult | null): SeriesRow[] =>
  (stats?.AudienceSeries ?? []).flatMap((entry) => [
    { Period: entry.Period, Group: 'Besucher', Count: entry.Visitors },
    { Period: entry.Period, Group: 'Sitzungen', Count: entry.Sessions },
  ]);

/** Splits an ISO period string ('yyyy-MM-dd' or 'yyyy-MM-ddTHH:mm') into parts. */
const splitPeriod = (period: string) => {
  const [datePart = '', timePart] = period.split('T');
  const [year = '', month = '', day = ''] = datePart.split('-');
  return { timePart, day, month, year };
};

/** Tooltip and table label; week periods name the Monday they start on. */
export const formatPeriodLabel = (period: string, granularity: Granularity): string => {
  const { timePart, day, month, year } = splitPeriod(period);
  if (timePart) return `${day}.${month}. ${timePart}`;
  if (granularity === 'week') return `Woche ab ${day}.${month}.${year}`;
  return `${day}.${month}.${year}`;
};

/** Axis label; same as the tooltip label without the week prefix. */
export const formatAxisLabel = (period: string): string => {
  const { timePart, day, month, year } = splitPeriod(period);
  if (timePart) return `${day}.${month}. ${timePart}`;
  return `${day}.${month}.${year}`;
};
