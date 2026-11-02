// @ts-check
import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import { fileURLToPath } from 'node:url';
import { pastEventPaths } from './src/data/veranstaltung-files.ts';

// Astro drops the 404 page from the sitemap on its own, but not these two:
// `/403/` is an auth error page and `/nachricht-gesendet/` is the contact
// form's post-submit confirmation, so neither belongs in search results.
// Drafts prefixed with `_` are unrouted and never reach the sitemap at all.
const notIndexable = new Set(['/403/', '/nachricht-gesendet/']);

// Past Veranstaltung pages stay online (shared links must not break) but are
// `noindex`, so they also leave the sitemap at the next build.
const pastEvents = new Set(
  pastEventPaths(fileURLToPath(new URL('./src/content/veranstaltungen', import.meta.url)), new Date()),
);

// https://astro.build/config
export default defineConfig({
  site: 'https://alpakasoelde.at',
  // The hero and impression bands render through `<Image layout="full-width">`,
  // and the layout only gets its sizing styles when this is on.
  image: { responsiveStyles: true },
  integrations: [
    sitemap({
      filter: (page) => {
        const { pathname } = new URL(page);
        return !notIndexable.has(pathname) && !pastEvents.has(pathname);
      },
      // A static build genuinely republishes every page, so the build time is
      // an honest lastmod.
      lastmod: new Date(),
    }),
  ],
});
