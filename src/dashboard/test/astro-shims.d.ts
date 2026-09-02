// `astro check` resolves `.astro` imports through its own language plugin, but
// `svelte-check` runs plain tsc and cannot. This wildcard only ever applies
// where real module resolution failed, so it does not weaken `astro check`.
declare module '*.astro' {
  const component: import('astro/runtime/server/index.js').AstroComponentFactory;
  export default component;
}
