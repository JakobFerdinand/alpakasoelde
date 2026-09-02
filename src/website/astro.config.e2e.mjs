// @ts-check
import baseConfig from './astro.config.mjs';

// The dev toolbar is anchored to the bottom of the viewport and intercepts
// pointer events on the footer, so the e2e dev server runs without it.
// `defineConfig` is only a typing helper, and re-running it over an already
// typed config loses the generic inference it does for `i18n`.
export default {
  ...baseConfig,
  devToolbar: { enabled: false },
};
