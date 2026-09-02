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

test('Head emits JSON-LD that parses as valid structured data', async () => {
  const html = await render('/');

  const ldJson = html.match(/<script type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/)?.[1];
  expect(ldJson, 'no application/ld+json block was rendered').toBeTruthy();

  // A payload escaped by Astro instead of inlined would not survive JSON.parse.
  const structuredData = JSON.parse(ldJson!);
  expect(structuredData['@type']).toBe('LocalBusiness');
  expect(structuredData.name).toBe('Alpakasölde');
  expect(structuredData.image).toMatch(new RegExp(`^${site}`));
});

test('Head derives the canonical URL and title from the rendered route', async () => {
  const html = await render('/alpaka-wanderungen', { title: 'Alpakawanderungen' });

  expect(html).toContain(`<link rel="canonical" href="${site}/alpaka-wanderungen">`);
  expect(html).toContain('<title>Alpakawanderungen | Alpakasölde</title>');
});
