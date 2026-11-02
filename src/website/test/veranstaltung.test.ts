import { expect, test } from 'vitest';
import {
  buildEventJsonLd,
  endsAt,
  formatDateLong,
  formatDayBlock,
  formatTimeRange,
  isUpcoming,
} from '../src/data/veranstaltung';

const at = (iso: string) => new Date(iso);

test('an event with an end is upcoming until that end', () => {
  const event = { start: at('2026-05-30T09:00:00+02:00'), end: at('2026-05-30T12:00:00+02:00') };

  expect(isUpcoming(event, at('2026-05-30T11:59:59+02:00'))).toBe(true);
  expect(isUpcoming(event, at('2026-05-30T12:00:00+02:00'))).toBe(false);
});

test('an event without an end stays upcoming until the end of its day in Vienna', () => {
  const event = { start: at('2026-05-30T09:00:00+02:00') };

  expect(isUpcoming(event, at('2026-05-30T23:59:59+02:00'))).toBe(true);
  expect(isUpcoming(event, at('2026-05-31T00:00:00+02:00'))).toBe(false);
  expect(endsAt(event).toISOString()).toBe('2026-05-30T22:00:00.000Z');
});

test('the end of day follows winter time too', () => {
  const event = { start: at('2026-12-12T10:00:00+01:00') };

  expect(endsAt(event).toISOString()).toBe('2026-12-12T23:00:00.000Z');
});

test('the end of day is Vienna midnight even when the start is late in UTC terms', () => {
  // 23:30 in Vienna is still the 30th, although it is 21:30 UTC.
  const event = { start: at('2026-05-30T23:30:00+02:00') };

  expect(endsAt(event).toISOString()).toBe('2026-05-30T22:00:00.000Z');
});

test('the end of day survives the spring-forward and fall-back days', () => {
  expect(endsAt({ start: at('2026-03-29T09:00:00+02:00') }).toISOString()).toBe(
    '2026-03-29T22:00:00.000Z',
  );
  expect(endsAt({ start: at('2026-10-25T09:00:00+01:00') }).toISOString()).toBe(
    '2026-10-25T23:00:00.000Z',
  );
});

test('dates and times are shown in German and in Vienna time', () => {
  const summer = at('2026-05-30T09:00:00+02:00');
  const winter = at('2026-12-12T18:30:00+01:00');

  expect(formatDateLong(summer)).toBe('Samstag, 30. Mai 2026');
  expect(formatDateLong(winter)).toBe('Samstag, 12. Dezember 2026');
  expect(formatTimeRange({ start: summer, end: at('2026-05-30T12:00:00+02:00') })).toBe(
    '09:00–12:00 Uhr',
  );
  expect(formatTimeRange({ start: winter })).toBe('ab 18:30 Uhr');
});

test('the date block splits a start into month and day', () => {
  expect(formatDayBlock(at('2026-05-30T09:00:00+02:00'))).toEqual({ month: 'Mai', day: '30' });
  expect(formatDayBlock(at('2026-03-01T09:00:00+01:00'))).toEqual({ month: 'März', day: '1' });
});

test('the Event structured data carries the farm as place and omits a guessed end', () => {
  const site = new URL('https://alpakasoelde.at');
  const node = buildEventJsonLd(
    {
      id: 'alpaka-yoga',
      title: 'Alpaka Yoga',
      summary: 'Yoga mit Alpakas.',
      start: at('2026-05-30T09:00:00+02:00'),
    },
    site,
  );

  expect(node).toMatchObject({
    '@type': 'Event',
    name: 'Alpaka Yoga',
    startDate: '2026-05-30T07:00:00.000Z',
    url: 'https://alpakasoelde.at/veranstaltungen/alpaka-yoga/',
    location: { name: 'Alpakasölde', address: { streetAddress: 'Frauenstein 12' } },
  });
  expect(node).not.toHaveProperty('endDate');
});
