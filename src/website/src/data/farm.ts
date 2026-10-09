// Single source for the facts the pages repeat: brand, contact details,
// address, coordinates and the three services. Callers import from here
// instead of retyping the values.
export const farm = {
  name: 'Alpakasölde',
  description: 'Kleiner Alpakahof in Frauenstein bei Mining, Oberösterreich',
  phone: '+43 699 81375946',
  email: 'kontakt@alpakasoelde.at',
  address: {
    street: 'Frauenstein 12',
    postalCode: '4962',
    locality: 'Mining',
    region: 'Oberösterreich',
    countryCode: 'AT',
    countryName: 'Österreich',
  },
  // Rounded to 6 decimals (~11 cm); more decimals on a geocoded farm address
  // are false precision.
  geo: { latitude: 48.285706, longitude: 13.167817 },
} as const;

export const services = [
  {
    path: '/alpaka-wanderungen',
    navLabel: 'Wanderungen',
    title: 'Alpaka-Wanderungen',
    offerName: 'Alpakawanderungen',
    blurb:
      'Gemütliche Touren durch die Natur mit unseren freundlichen Alpakas inklusive Hofbesuch.',
  },
  {
    path: '/wollverarbeitung',
    navLabel: 'Wollverarbeitung',
    title: 'Wollverarbeitung',
    offerName: 'Wollverarbeitungs-Workshop',
    blurb:
      'Lerne, was mit der Wolle nach dem Scheren passiert und probiere dich im Kardieren und Spinnen am Spinnrad.',
  },
  {
    path: '/produkte',
    navLabel: 'Produkte',
    title: 'Produkte',
    offerName: 'Hofladen & Alpakaprodukte',
    blurb: 'Wolle, Accessoires und mehr – direkt von unseren Alpakas.',
  },
] as const;

// Builds the single LocalBusiness node for the site, emitted on the homepage
// only. NOTE: name, telephone and url must stay character-identical to the
// Google Business Profile; a mismatch between schema and GBP ranks worse in
// the local pack than incomplete schema does. "Frauenstein" is the hamlet,
// 4962 Mining the postal town, which is why addressLocality is Mining.
//
// `openingHours` is deliberately absent: the farm runs by appointment, and a
// guessed schedule earns a "geschlossen" label in the local pack.
export const buildLocalBusinessJsonLd = (site: URL) => {
  const origin = site.origin;

  return {
    '@context': 'https://schema.org',
    '@type': 'LocalBusiness',
    '@id': `${origin}/#business`,
    name: farm.name,
    description: farm.description,
    url: origin,
    telephone: farm.phone,
    email: farm.email,
    image: new URL('/og-default.jpg', site).toString(),
    address: {
      '@type': 'PostalAddress',
      streetAddress: farm.address.street,
      postalCode: farm.address.postalCode,
      addressLocality: farm.address.locality,
      addressRegion: farm.address.region,
      addressCountry: farm.address.countryCode,
    },
    geo: { '@type': 'GeoCoordinates', latitude: farm.geo.latitude, longitude: farm.geo.longitude },
    areaServed: { '@type': 'AdministrativeArea', name: farm.address.region },
    priceRange: '€€',
    // Replaces the old `serviceType` array, which is not a LocalBusiness
    // property and so was ignored.
    hasOfferCatalog: {
      '@type': 'OfferCatalog',
      name: 'Angebote',
      itemListElement: services.map((service) => ({
        '@type': 'Offer',
        itemOffered: {
          '@type': 'Service',
          name: service.offerName,
          url: `${origin}${service.path}/`,
        },
      })),
    },
  };
};
