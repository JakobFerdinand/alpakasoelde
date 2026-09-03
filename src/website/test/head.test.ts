import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { expect, test } from 'vitest';
import astroConfig from '../astro.config.mjs';
import Head from '../src/components/Head.astro';

// The container builds its own manifest, so `site` has to be handed to it
// explicitly; take it from astro.config.mjs rather than repeating the origin.
const site = astroConfig.site!;

const render = async (pathname: string, props?: Record<string, unknown>) => {
  const container = await AstroContainer.create({ astroConfig: { site } });
  return container.renderToString(Head, {
    props,
    request: new Request(new URL(pathname, site)),
  });
};

const requiredProps = { title: 'Alpakasölde', description: 'Kleiner Alpakahof in Frauenstein.' };

test('Head emits JSON-LD that parses as valid structured data', async () => {
  const html = await render('/', requiredProps);

  const ldJson = html.match(/<script type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/)?.[1];
  expect(ldJson, 'no application/ld+json block was rendered').toBeTruthy();

  // A payload escaped by Astro instead of inlined would not survive JSON.parse.
  const structuredData = JSON.parse(ldJson!);
  expect(structuredData['@type']).toBe('LocalBusiness');
  expect(structuredData.name).toBe('Alpakasölde');
  expect(structuredData.image).toMatch(new RegExp(`^${site}`));
});

test('Head derives the canonical URL and title from the rendered route', async () => {
  const html = await render('/alpaka-wanderungen', {
    title: 'Alpakawanderungen',
    description: 'Geführte Touren mit unseren Alpakas.',
  });

  expect(html).toContain(`<link rel="canonical" href="${site}/alpaka-wanderungen">`);
  expect(html).toContain('<title>Alpakawanderungen | Alpakasölde</title>');
});

// SVG og:images render as a blank card on Facebook, WhatsApp, LinkedIn and X,
// which is what the logo import used to produce.
test('Head advertises a rasterised social card with its dimensions', async () => {
  const html = await render('/', requiredProps);

  expect(html).toContain(`<meta property="og:image" content="${site}/og-default.jpg">`);
  expect(html).toContain(`<meta name="twitter:image" content="${site}/og-default.jpg">`);
  expect(html).toContain('<meta property="og:image:width" content="1200">');
  expect(html).toContain('<meta property="og:image:height" content="630">');
  expect(html).toMatch(/<meta property="og:image:alt" content="[^"]+">/);
  expect(html).not.toMatch(/og:image" content="[^"]*\.svg"/);
});

// The homepage title carries the brand itself, so appending the suffix would
// render "… | Alpakasölde | Alpakasölde".
test('Head can opt out of the brand suffix', async () => {
  // Ampersand-free so the assertions do not have to mirror Astro's escaping.
  const title = 'Alpakahof in Frauenstein, Oberösterreich';

  const html = await render('/', { ...requiredProps, title, brandSuffix: false });

  expect(html).toContain(`<title>${title}</title>`);
  expect(html).toContain(`<meta property="og:title" content="${title}">`);
  expect(html).not.toContain('| Alpakasölde');
});

test('Head puts the page description into all three description tags', async () => {
  const description = 'Geführte Alpakawanderung im Innviertel, inklusive Hofbesuch.';

  const html = await render('/alpaka-wanderungen', { title: 'Alpaka-Wanderungen', description });

  expect(html).toContain(`<meta name="description" content="${description}">`);
  expect(html).toContain(`<meta property="og:description" content="${description}">`);
  expect(html).toContain(`<meta name="twitter:description" content="${description}">`);
});

// The whole point of making `description` required is that two pages cannot
// silently end up with the same snippet again; a reintroduced default would
// make this pass with identical output.
test('Head does not fall back to a shared description', async () => {
  const [first, second] = await Promise.all([
    render('/produkte', { title: 'Produkte', description: 'Wolle und Accessoires aus dem Hofladen.' }),
    render('/impressum', { title: 'Impressum', description: 'Anbieterkennzeichnung und Kontaktdaten.' }),
  ]);

  const descriptionOf = (html: string) =>
    html.match(/<meta name="description" content="([^"]*)"/)?.[1];

  expect(descriptionOf(first)).toBe('Wolle und Accessoires aus dem Hofladen.');
  expect(descriptionOf(second)).toBe('Anbieterkennzeichnung und Kontaktdaten.');
});
