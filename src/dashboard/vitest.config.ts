/// <reference types="vitest/config" />
import { getViteConfig } from 'astro/config';

// getViteConfig loads astro.config.mjs, so the Svelte integration and the
// `astro:container` virtual module are available to tests.
export default getViteConfig({
  test: {
    // Astro components may only be rendered in the node environment.
    environment: 'node',
    include: ['test/**/*.test.ts'],
  },
});
