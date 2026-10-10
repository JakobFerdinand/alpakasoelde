import { describe, expect, test } from 'vitest';
import { normalizeToolTrace, normalizeToolTraces, normalizeUsage } from '../src/utils/assistant';

describe('normalizeToolTrace', () => {
  test('accepts both casings', () => {
    expect(normalizeToolTrace({ tool: 'pageviews', arguments: '{"days":30}' })).toEqual({
      tool: 'pageviews',
      arguments: '{"days":30}',
    });
    expect(normalizeToolTrace({ Tool: 'gutscheine', Arguments: '{}' })).toEqual({
      tool: 'gutscheine',
      arguments: '{}',
    });
    expect(normalizeToolTrace(null)).toEqual({ tool: '', arguments: '' });
  });

  test('normalizeToolTraces ignores anything that is not an array', () => {
    expect(normalizeToolTraces([{ Tool: 'messages' }])).toEqual([
      { tool: 'messages', arguments: '' },
    ]);
    expect(normalizeToolTraces(null)).toEqual([]);
    expect(normalizeToolTraces({ tool: 'x' })).toEqual([]);
  });
});

describe('normalizeUsage', () => {
  test('accepts both casings and coerces numbers from strings', () => {
    const usage = normalizeUsage({
      InputTokens: '120',
      OutputTokens: 40,
      ReasoningTokens: '17',
      CachedInputTokens: 0,
      Cost: '0.00012',
      Currency: 'EUR',
      InputPricePerMillion: 0.15,
      OutputPricePerMillion: 0.6,
    });

    expect(usage).toEqual({
      inputTokens: 120,
      outputTokens: 40,
      reasoningTokens: 17,
      cachedInputTokens: 0,
      cost: 0.00012,
      currency: 'EUR',
      inputPricePerMillion: 0.15,
      outputPricePerMillion: 0.6,
    });
  });

  test('stays null when the response carries no usage', () => {
    expect(normalizeUsage(null)).toBeNull();
    expect(normalizeUsage(undefined)).toBeNull();
  });

  test('fills unusable numbers with 0 and defaults the currency', () => {
    const usage = normalizeUsage({ inputTokens: 'keine zahlen' });
    expect(usage).toEqual({
      inputTokens: 0,
      outputTokens: 0,
      reasoningTokens: 0,
      cachedInputTokens: 0,
      cost: 0,
      currency: 'EUR',
      inputPricePerMillion: 0,
      outputPricePerMillion: 0,
    });
  });
});
