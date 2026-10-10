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
