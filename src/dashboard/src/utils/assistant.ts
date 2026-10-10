/**
 * Casing-tolerant normalizers for the assistant's API responses.
 * Moved out of AssistantChat.svelte so they can be tested.
 */
import { toNumber } from './formatters';

export type ToolTrace = {
  tool: string;
  arguments: string;
};

type ToolTraceRaw = {
  tool?: string;
  Tool?: string;
  arguments?: string;
  Arguments?: string;
};

export type Usage = {
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  cachedInputTokens: number;
  cost: number;
  currency: string;
  inputPricePerMillion: number;
  outputPricePerMillion: number;
};

export type UsageRaw = {
  inputTokens?: number | string | null;
  InputTokens?: number | string | null;
  outputTokens?: number | string | null;
  OutputTokens?: number | string | null;
  reasoningTokens?: number | string | null;
  ReasoningTokens?: number | string | null;
  cachedInputTokens?: number | string | null;
  CachedInputTokens?: number | string | null;
  cost?: number | string | null;
  Cost?: number | string | null;
  currency?: string | null;
  Currency?: string | null;
  inputPricePerMillion?: number | string | null;
  InputPricePerMillion?: number | string | null;
  outputPricePerMillion?: number | string | null;
  OutputPricePerMillion?: number | string | null;
};

export type AskResultRaw = {
  reply?: string;
  Reply?: string;
  session?: unknown;
  Session?: unknown;
  tools?: ToolTraceRaw[];
  Tools?: ToolTraceRaw[];
  usage?: UsageRaw | null;
  Usage?: UsageRaw | null;
};

/**
 * Normalizes a tool trace, handling both camelCase and PascalCase field names.
 */
export const normalizeToolTrace = (eintrag: ToolTraceRaw | null | undefined): ToolTrace => ({
  tool: eintrag?.tool ?? eintrag?.Tool ?? '',
  arguments: eintrag?.arguments ?? eintrag?.Arguments ?? '',
});

export const normalizeToolTraces = (eintraege: unknown): ToolTrace[] =>
  Array.isArray(eintraege) ? eintraege.map(normalizeToolTrace) : [];

/**
 * Normalizes the per-request usage, handling both camelCase and PascalCase field names.
 * Older or partial responses carry no usage at all, which stays `null`.
 */
export const normalizeUsage = (verbrauch: UsageRaw | null | undefined): Usage | null => {
  if (!verbrauch) return null;

  return {
    inputTokens: toNumber(verbrauch.inputTokens ?? verbrauch.InputTokens),
    outputTokens: toNumber(verbrauch.outputTokens ?? verbrauch.OutputTokens),
    reasoningTokens: toNumber(verbrauch.reasoningTokens ?? verbrauch.ReasoningTokens),
    cachedInputTokens: toNumber(verbrauch.cachedInputTokens ?? verbrauch.CachedInputTokens),
    cost: toNumber(verbrauch.cost ?? verbrauch.Cost),
    currency: verbrauch.currency ?? verbrauch.Currency ?? 'EUR',
    inputPricePerMillion: toNumber(
      verbrauch.inputPricePerMillion ?? verbrauch.InputPricePerMillion,
    ),
    outputPricePerMillion: toNumber(
      verbrauch.outputPricePerMillion ?? verbrauch.OutputPricePerMillion,
    ),
  };
};
