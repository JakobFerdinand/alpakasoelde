// The one place that decides whether a Veranstaltung is upcoming, so the
// homepage section, the nav link, the detail pages, the sitemap filter and the
// tests cannot drift apart. Pure on purpose: callers pass "now" in.
import { farm } from './farm';

const timeZone = 'Europe/Vienna';
const locale = 'de-AT';

export interface Timespan {
  start: Date;
  end?: Date;
}

// Offset of Vienna from UTC at an instant, in ms (+1h in winter, +2h in summer).
const viennaOffset = (instant: number) => {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: 'numeric',
    minute: 'numeric',
    second: 'numeric',
  }).formatToParts(instant);
  const get = (type: string) => Number(parts.find((p) => p.type === type)!.value);
  const asUtc = Date.UTC(
    get('year'),
    get('month') - 1,
    get('day'),
    get('hour'),
    get('minute'),
    get('second'),
  );
  return asUtc - Math.floor(instant / 1000) * 1000;
};

// Start of the next calendar day in Vienna. Midnight never sits on a DST
// switch (those happen at 02:00/03:00), so two passes settle the offset.
const nextViennaMidnight = (instant: Date) => {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(instant);
  const get = (type: string) => Number(parts.find((p) => p.type === type)!.value);
  const localMidnight = Date.UTC(get('year'), get('month') - 1, get('day') + 1);
  const guess = localMidnight - viennaOffset(localMidnight);
  return new Date(localMidnight - viennaOffset(guess));
};

export const endsAt = ({ start, end }: Timespan): Date => end ?? nextViennaMidnight(start);

export const isUpcoming = (event: Timespan, now: Date): boolean =>
  endsAt(event).getTime() > now.getTime();

const format = (date: Date, options: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat(locale, { timeZone, ...options }).format(date);

export const formatDateLong = (date: Date) =>
  format(date, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });

const formatTime = (date: Date) =>
  format(date, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

export const formatTimeRange = ({ start, end }: Timespan) =>
  end ? `${formatTime(start)}–${formatTime(end)} Uhr` : `ab ${formatTime(start)} Uhr`;

export const formatDayBlock = (date: Date) => ({
  month: format(date, { month: 'long' }),
  day: format(date, { day: 'numeric' }),
});

export interface VeranstaltungData extends Timespan {
  title: string;
  summary: string;
  price?: string;
  location?: string;
  contact?: { name?: string; phone?: string };
}

export const veranstaltungPath = (id: string) => `/veranstaltungen/${id}/`;

// schema.org Event node for a detail page. Past events stay `EventScheduled`
// and are `noindex` anyway.
export const buildEventJsonLd = (event: VeranstaltungData & { id: string }, site: URL) => ({
  '@context': 'https://schema.org',
  '@type': 'Event',
  name: event.title,
  description: event.summary,
  startDate: event.start.toISOString(),
  // Without an explicit end the derived end of day would be a guess.
  ...(event.end && { endDate: event.end.toISOString() }),
  eventStatus: 'https://schema.org/EventScheduled',
  eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
  url: new URL(veranstaltungPath(event.id), site).toString(),
  location: event.location
    ? { '@type': 'Place', name: event.location }
    : {
        '@type': 'Place',
        name: farm.name,
        address: {
          '@type': 'PostalAddress',
          streetAddress: farm.address.street,
          postalCode: farm.address.postalCode,
          addressLocality: farm.address.locality,
          addressRegion: farm.address.region,
          addressCountry: farm.address.countryCode,
        },
      },
  organizer: { '@type': 'Organization', name: farm.name, url: site.origin },
});
