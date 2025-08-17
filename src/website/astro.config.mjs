// @ts-check
import { defineConfig } from 'astro/config';

// https://astro.build/config
export default defineConfig({
  site: 'https://alpakasoelde.at',
  vite: {
    assetsInclude: ['**/*.HEIC', '**/*.heic', '**/*.*']
  }
});
