import { afterEach, describe, expect, test, vi } from 'vitest';
import {
  calculateAge,
  formatCurrency,
  formatDate,
  formatDateForInput,
  formatDateLong,
  formatDuration,
  formatTimestamp,
  toNumber,
} from '../src/utils/formatters';

// Local-time literals (no trailing `Z`) keep the date-only formatters independent
// of the machine time zone; `formatTimestamp` pins Europe/Vienna itself.
const LOCAL_NOON = '2024-01-05T12:00:00';

afterEach(() => {
  vi.useRealTimers();
});

describe('formatDate', () => {
  test('renders de-AT day-first dates', () => {
    expect(formatDate(LOCAL_NOON)).toBe('05.01.2024');
  });

  test('shows an em dash for missing values and echoes unparseable ones', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
    expect(formatDate('irgendwann')).toBe('irgendwann');
  });
});

describe('formatDateLong', () => {
  test('spells the month out in Austrian German', () => {
    expect(formatDateLong(LOCAL_NOON)).toBe('5. Jänner 2024');
  });

  test('falls back to Unbekannt rather than echoing the input', () => {
    expect(formatDateLong(null)).toBe('Unbekannt');
    expect(formatDateLong('irgendwann')).toBe('Unbekannt');
  });
});

describe('formatDateForInput', () => {
  test('produces the yyyy-mm-dd shape a date input expects', () => {
    expect(formatDateForInput(LOCAL_NOON)).toBe('2024-01-05');
  });

  test('produces an empty string for anything a date input cannot hold', () => {
    expect(formatDateForInput(null)).toBe('');
    expect(formatDateForInput('irgendwann')).toBe('');
  });
});

describe('formatCurrency', () => {
  test('formats numbers and numeric strings as EUR', () => {
    // de-AT separates the symbol from the amount with a non-breaking space,
    // written as an escape here so it survives copy-paste.
    expect(formatCurrency(75)).toBe('€\u00a075,00');
    expect(formatCurrency('75.5')).toBe('€\u00a075,50');
  });

  test('keeps zero as an amount instead of treating it as missing', () => {
    expect(formatCurrency(0)).toBe('€\u00a00,00');
  });

  test('shows an em dash for missing and non-numeric values', () => {
    expect(formatCurrency(null)).toBe('—');
    expect(formatCurrency(undefined)).toBe('—');
    expect(formatCurrency('kostenlos')).toBe('—');
  });
});

describe('toNumber', () => {
  test('coerces numeric strings and floors everything else to zero', () => {
    expect(toNumber('42')).toBe(42);
    expect(toNumber(42)).toBe(42);
    expect(toNumber('kostenlos')).toBe(0);
    expect(toNumber(null)).toBe(0);
    expect(toNumber(undefined)).toBe(0);
  });
});

describe('calculateAge', () => {
  const at = (isoNow: string, birth: string) => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(isoNow));
    return calculateAge(birth);
  };

  test('counts full years and only ticks over on the birthday', () => {
    expect(at('2025-06-15T10:00:00Z', '2020-06-15')).toBe('5 Jahre');
    expect(at('2025-06-14T10:00:00Z', '2020-06-15')).toBe('4 Jahre');
  });

  test('uses the singular for a one-year-old', () => {
    expect(at('2025-06-15T10:00:00Z', '2024-06-15')).toBe('1 Jahr');
  });

  test('reports Unbekannt for missing, unparseable and future birth dates', () => {
    expect(calculateAge(null)).toBe('Unbekannt');
    expect(calculateAge('irgendwann')).toBe('Unbekannt');
    expect(at('2025-06-15T10:00:00Z', '2026-01-01')).toBe('Unbekannt');
  });
});

describe('formatDuration', () => {
  test('picks the coarsest unit that still carries detail', () => {
    expect(formatDuration(0)).toBe('0 s');
    expect(formatDuration(45.4)).toBe('45 s');
    expect(formatDuration(90)).toBe('1 min 30 s');
    expect(formatDuration(120)).toBe('2 min');
    expect(formatDuration(3660)).toBe('1 h 1 min');
    expect(formatDuration(3600)).toBe('1 h');
  });

  test('caps at a day and rejects missing or negative durations', () => {
    expect(formatDuration(86_400)).toBe('> 24 h');
    expect(formatDuration(-1)).toBe('—');
    expect(formatDuration(null)).toBe('—');
    expect(formatDuration(undefined)).toBe('—');
  });
});

describe('formatTimestamp', () => {
  test('renders UTC timestamps in Vienna wall-clock time', () => {
    // 08:30 UTC is 10:30 during Central European Summer Time.
    expect(formatTimestamp('2024-06-01T08:30:00Z')).toBe('1.6.2024, 10:30:00');
  });

  test('shows an em dash for missing values and echoes unparseable ones', () => {
    expect(formatTimestamp(null)).toBe('—');
    expect(formatTimestamp('irgendwann')).toBe('irgendwann');
  });
});
