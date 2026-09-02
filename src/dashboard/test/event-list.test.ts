import { expect, test } from 'vitest';
import { EVENT_TYPE_ICON_KEYS, normalizeEvents } from '../src/components/event-list';

test('normalizeEvents accepts both API casings and defaults the rest', () => {
  const events = normalizeEvents([
    {
      eventType: 'Entwurmen',
      eventDate: '2025-04-01',
      alpakaNames: ['Richard'],
      comment: 'Frühjahrskur',
      cost: 42,
    },
    {
      EventType: 'Scheren',
      EventDate: '2025-05-02',
      AlpakaNames: ['Ludwig', 'Amadeus'],
      Comment: 'Ganze Herde',
      Cost: 180,
    },
    { eventType: 'Impfen', eventDate: '2025-06-03' },
  ]);

  expect(events).toEqual([
    {
      eventType: 'Entwurmen',
      eventDate: '2025-04-01',
      alpakaNames: ['Richard'],
      comment: 'Frühjahrskur',
      cost: 42,
    },
    {
      eventType: 'Scheren',
      eventDate: '2025-05-02',
      alpakaNames: ['Ludwig', 'Amadeus'],
      comment: 'Ganze Herde',
      cost: 180,
    },
    { eventType: 'Impfen', eventDate: '2025-06-03', alpakaNames: [], comment: '', cost: null },
  ]);
});

test('normalizeEvents returns an empty list for anything that is not an array', () => {
  expect(normalizeEvents(undefined)).toEqual([]);
  expect(normalizeEvents(null)).toEqual([]);
  expect(normalizeEvents({ eventType: 'Impfen' })).toEqual([]);
});

test('every icon key maps a lowercased event type', () => {
  // Lookups happen on lowercased event types, so an uppercase key would never hit.
  for (const eventType of Object.keys(EVENT_TYPE_ICON_KEYS)) {
    expect(eventType).toBe(eventType.toLowerCase());
  }
});
