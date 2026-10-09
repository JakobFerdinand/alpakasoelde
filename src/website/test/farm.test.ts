import { existsSync } from 'node:fs';
import { expect, test } from 'vitest';
import astroConfig from '../astro.config.mjs';
import { buildLocalBusinessJsonLd, services } from '../src/data/farm';

// Take the origin from astro.config.mjs rather than repeating it.
const site = astroConfig.site!;

// Written out in full, copied from the `business` literal the homepage used to
// carry; the module exists so this output never has to be re-derived by hand.
const expected = {
  '@context': 'https://schema.org',
  '@type': 'LocalBusiness',
  '@id': 'https://alpakasoelde.at/#business',
  name: 'Alpakasölde',
  description: 'Kleiner Alpakahof in Frauenstein bei Mining, Oberösterreich',
  url: 'https://alpakasoelde.at',
  telephone: '+43 699 81375946',
  email: 'kontakt@alpakasoelde.at',
  image: 'https://alpakasoelde.at/og-default.jpg',
  address: {
    '@type': 'PostalAddress',
    streetAddress: 'Frauenstein 12',
    postalCode: '4962',
    addressLocality: 'Mining',
    addressRegion: 'Oberösterreich',
    addressCountry: 'AT',
  },
  geo: { '@type': 'GeoCoordinates', latitude: 48.285706, longitude: 13.167817 },
  areaServed: { '@type': 'AdministrativeArea', name: 'Oberösterreich' },
  priceRange: '€€',
  hasOfferCatalog: {
    '@type': 'OfferCatalog',
    name: 'Angebote',
    itemListElement: [
      {
        '@type': 'Offer',
        itemOffered: {
          '@type': 'Service',
          name: 'Alpakawanderungen',
          url: 'https://alpakasoelde.at/alpaka-wanderungen/',
        },
      },
      {
        '@type': 'Offer',
        itemOffered: {
          '@type': 'Service',
          name: 'Wollverarbeitungs-Workshop',
          url: 'https://alpakasoelde.at/wollverarbeitung/',
        },
      },
      {
        '@type': 'Offer',
        itemOffered: {
          '@type': 'Service',
          name: 'Hofladen & Alpakaprodukte',
          url: 'https://alpakasoelde.at/produkte/',
        },
      },
    ],
  },
};

test('the builder emits the exact LocalBusiness literal for the configured site', () => {
  expect(buildLocalBusinessJsonLd(new URL(site))).toStrictEqual(expected);
});

test('the JSON-LD output survives a JSON round-trip unchanged', () => {
  const structuredData = buildLocalBusinessJsonLd(new URL(site));

  expect(JSON.parse(JSON.stringify(structuredData))).toStrictEqual(structuredData);
});

// The services drive nav links and sitemap-relevant page links, so a typo'd
// path should fail loudly even before a page renders.
test('every service path is root-relative and matches a rendered page', () => {
  for (const service of services) {
    expect(service.path.startsWith('/'), `${service.path} should start with /`).toBe(true);
    expect(service.path.endsWith('/'), `${service.path} should not have a trailing slash`).toBe(
      false,
    );
    expect(
      existsSync(new URL(`../src/pages${service.path}.astro`, import.meta.url)),
      `${service.path} has no matching page in src/pages`,
    ).toBe(true);
  }
});
