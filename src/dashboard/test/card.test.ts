import { getContainerRenderer } from '@astrojs/svelte/container-renderer';
import { experimental_AstroContainer as AstroContainer } from 'astro/container';
import { loadRenderers } from 'astro:container';
import { expect, test } from 'vitest';
import CardHost from './fixtures/CardHost.astro';

test('Card renders its optional headings, slot and extra class through Astro', async () => {
  const container = await AstroContainer.create({
    renderers: await loadRenderers([getContainerRenderer()]),
  });

  const html = await container.renderToString(CardHost, {
    props: { eyebrow: 'Gutscheine', title: 'Offen', class: 'card--wide' },
  });

  expect(html).toContain('class="card card--wide"');
  expect(html).toContain('Gutscheine');
  expect(html).toContain('Offen');
  expect(html).toContain('42 Gutscheine');
  // `subtitle` was not passed, so its paragraph must stay out of the markup.
  expect(html).not.toContain('card-subtitle');
});
