// @ts-check
import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

// Astro drops the 404 page from the sitemap on its own, but not these two:
// `/403/` is an auth error page and `/nachricht-gesendet/` is the contact
// form's post-submit confirmation, so neither belongs in search results.
// Drafts prefixed with `_` are unrouted and never reach the sitemap at all.
const notIndexable = new Set(['/403/', '/nachricht-gesendet/']);

// https://astro.build/config
export default defineConfig({
  site: 'https://alpakasoelde.at',
  integrations: [
    sitemap({
      filter: (page) => !notIndexable.has(new URL(page).pathname),
    }),
  ],
});
