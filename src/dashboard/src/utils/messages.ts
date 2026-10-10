/**
 * Casing-tolerant normalizers for the messages API.
 * The API answers PascalCase, older shapes camelCase — both must keep working.
 */
import { asCount, asString, pickCase } from './casing';

export type Message = {
  Id: string;
  Name: string;
  Email: string;
  Phone: string;
  Message: string;
  Timestamp: string;
  IsSpam: boolean;
};

export type MessagePeriodBucket = { Period: string; Spam: number; Legit: number };

export type MessageStats = {
  Total: number;
  Spam: number;
  Legit: number;
  OldCount: number;
  Series: MessagePeriodBucket[];
};

export const normalizeMessage = (raw: unknown): Message => ({
  Id: asString(pickCase(raw, 'id')),
  Name: asString(pickCase(raw, 'name')),
  Email: asString(pickCase(raw, 'email')),
  Phone: asString(pickCase(raw, 'phone')),
  Message: asString(pickCase(raw, 'message')),
  Timestamp: asString(pickCase(raw, 'timestamp')),
  IsSpam: pickCase(raw, 'isSpam') === true,
});

export const normalizeMessages = (raw: unknown): Message[] =>
  Array.isArray(raw) ? raw.map(normalizeMessage) : [];

const normalizeBucket = (bucket: unknown): MessagePeriodBucket => ({
  Period: asString(pickCase(bucket, 'period')),
  Spam: asCount(pickCase(bucket, 'spam')),
  Legit: asCount(pickCase(bucket, 'legit')),
});

export const normalizeMessageStats = (raw: unknown): MessageStats => {
  const series = pickCase<unknown>(raw, 'series');
  return {
    Total: asCount(pickCase(raw, 'total')),
    Spam: asCount(pickCase(raw, 'spam')),
    Legit: asCount(pickCase(raw, 'legit')),
    OldCount: asCount(pickCase(raw, 'oldCount')),
    Series: Array.isArray(series) ? series.map(normalizeBucket) : [],
  };
};
