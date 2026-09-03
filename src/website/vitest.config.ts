/// <reference types="vitest/config" />
import { getViteConfig } from 'astro/config';

// getViteConfig loads astro.config.mjs, so `Astro.site` and the image/asset
// pipeline behave in tests exactly as they do in a build.
export default getViteConfig({
  test: {
    // Astro components may only be rendered in the node environment.
    environment: 'node',
    // Keep Playwright's `e2e/` specs out of the Vitest run.
    include: ['test/**/*.test.ts'],
  },
});
