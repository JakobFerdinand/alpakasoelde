# Dashboard Svelte Islands Migration Plan

## Goal

Consolidate the interactive layers of the internal dashboard (`src/dashboard`) on
Svelte by migrating every client-side `<script>` block and imperative
`document.createElement` DOM code into Svelte 5 islands, mounted from thin Astro
wrappers with `client:only="svelte"` — the same pattern already proven by
`MessageStats.astro` + `MessageStatsChart.svelte` (plan `004`).

The Astro shell stays: layout, `Head.astro`, pages, scoped styles, `astro:assets`
image handling, `astro check`, and the entire SWA build/deploy pipeline are
untouched. This is the "Path A" (islands) decision from the branch discussion; it
defers a full SvelteKit rewrite, and the Svelte components written here port
~verbatim if that is ever attempted.

## Decisions

- **Keep Astro, expand Svelte islands.** The dashboard is 16 `.astro` files + 1
  `.svelte` file; the interactive weight is vanilla JS inside Astro `<script>`
  blocks (`messages.astro`, `Alpakas.astro`, `alpakas.astro`, `gutscheine.astro`,
  `Events.astro`, `DashboardNavbar.astro`, `Modal.astro`, `event-list.ts`). All of
  that becomes reactive Svelte 5 (`$state`/`$derived`/`$props`, runes as used in
  `MessageStatsChart.svelte`).
- **Islands own their section.** Each migrated region becomes a Svelte component
  that renders its full `<section>` (like `MessageStatsChart`) and is mounted by a
  thin `.astro` wrapper file via `<Component client:only="svelte" />`. The Astro
  page files stay (they define the route) but become compositions of islands.
- **Data flow unchanged.** All data already arrives via client-side `fetch` to
  `/api/*` (`/api/alpakas`, `/api/events`, `/api/messages`, `/api/gutscheine`).
  No backend, auth, or `staticwebapp.config.json` change. `.auth` (navbar user)
  stays as a fetch too.
- **Icons without template cloning.** `EventList.astro` and `messages.astro`
  currently render lucide icons into hidden `<template>`s and clone them at
  runtime (`cloneIcon`, `cloneIconTemplate`). In Svelte, import the icon
  components directly from `@lucide/svelte` (already a dependency) — no cloning.
  Event icons resolve by the same `EVENT_TYPE_ICON_KEYS` mapping.
- **Modals become Svelte components.** `Modal.astro` exposes imperative
  `openModal()`/`closeModal()` on the DOM element; a Svelte `Modal.svelte` takes a
  `$bindable open` prop and emits `close`/backdrop-click/Escape. Focus is
  currently handled by the browser (`hidden` + dialog role); keep behaviour
  equivalent.
- **Presentational wrappers convert too.** `Card`, `FormField`, and `Modal` are
  only used from regions that move into Svelte, so they become Svelte components
  (Svelte children/snippets instead of `<slot>`). This keeps a single paradigm
  and removes the `.astro`→`.svelte` interop edge cases.
- **Accessibility preserved.** Every `aria-sort`/`aria-pressed`/`aria-live`/
  `role="dialog"`/sr-only table and the WCAG AA colour choices in the current
  pages must be carried over 1:1. `astro check` + `svelte-check` gate regressions.
- **CSS stays scoped.** Each Svelte component keeps its styles scoped to the
  component (Svelte scoping replaces Astro scoping); design tokens
  (`--weidegruen`, `--backstein`, ...) and `global.css` layout classes
  (`.section`, `.container`, `.card`, `.form-field`) continue to be referenced as
  global styles. `:global(...)` is used only where the component must reach into
  the shared stylesheet (e.g. `.card`/`.form-field` wrappers rendered by Svelte).

## Milestones (tracked)

- [x] Create git branch `feat/dashboard-svelte-islands`
- [x] Write the plan (`docs/plans/005-svelte-islands-migration.md`)
- [ ] Migrate `EventList` + `Events` (dashboards overview & alpaka detail share it)
- [ ] Migrate `Modal`, `Card`, `FormField` (shared building blocks)
- [ ] Migrate `Alpakas` (overview list, add-alpaka form, event form)
- [ ] Migrate `alpakas.astro` detail page (view/edit/photo upload)
- [ ] Migrate `messages.astro` (inbox table: sort/filter/expand/delete)
- [ ] Migrate `gutscheine.astro` + `GutscheinListe` (form, list, redeem dialog)
- [ ] Migrate `DashboardNavbar` script (mobile toggle + `.auth/me` user)
- [ ] Verify: `pnpm run build` + `astro check` + `svelte-check`, manual pass
- [ ] Deploy on `main` merge

## 1. Shared building blocks (`src/dashboard/src/components`)

- `Modal.svelte`: `export let open = $bindable(false)` (+ `label` prop). Owns the
  backdrop, close button, Escape and backdrop-click handling, `aria-hidden`/`hidden`
  toggling, `document.body.style.overflow`. Emits nothing on its own — parent
  binds `open`. Ports the logic from `Modal.astro`'s script verbatim.
- `Card.svelte`: props `eyebrow?`, `title?`, `subtitle?`, `class?`; renders
  `{@render children?.()}` (Svelte 5 snippet children) — drop-in for `Card.astro`.
- `FormField.svelte`: props `label`, `id`, `hint?`, `required?`; snippet children
  inside the label/slot — drop-in for `FormField.astro`.
- Remove `Card.astro`, `FormField.astro`, `Modal.astro` once their call sites are
  Svelte.

## 2. Event list (`EventList` + `Events`)

Shared by the dashboard root and the alpaka detail page, currently
`EventList.astro` (icon templates) + `Events.astro` (fetch) + `event-list.ts`
(imperative table builder).

- `EventList.svelte`: props `id`, `emptyText`, `loadingText?`, `showAlpakaNames?`.
  Holds `$state` events + loading/error; fetches `/api/events` on mount; renders
  the table declaratively with `{#each}`. Event icons imported directly from
  `@lucide/svelte` (`Worm`, `Scissors`, `Syringe`, `Stethoscope`,
  `CircleEllipsis`) via the `EVENT_TYPE_ICON_KEYS` mapping (move the mapping and
  `normalizeEvents` into the component or a small `event-list.ts` that no longer
  touches the DOM).
- `Events.svelte`: the dashboard-root section (`Letzte Ereignisse`) delegating to
  `EventList`; `Events.astro` becomes a thin wrapper mounting it.
- Keep the sr-only/loading/empty/error states and the event-table markup/styles.

## 3. `Alpakas` overview (`src/components/Alpakas.astro`)

List + two modals + both forms.

- `Alpakas.svelte`: `$state` alpaka rows (fetch `/api/alpakas` on mount),
  clickable rows with Enter/Space navigation to `/alpakas?id=`, avatar/age cells.
  Owns the two `Modal.svelte` instances (add-alpaka, add-event) and their forms:
  multipart POST `/api/alpakas`, JSON POST `/api/events` with the existing
  validation (required type, ≥1 alpaka, date, cost/comment optional, 15 MB photo
  cap, 100-char name limit).
- Populate the event-form `<select multiple>` from the loaded alpaka list; keep
  the default-first-selection behaviour and the date prefill.
- `Alpakas.astro` becomes the wrapper mounting `<Alpakas client:only="svelte" />`.
- Port the button/`loading` states and `alert(...)` feedback as-is.

## 4. Alpaka detail (`src/pages/alpakas.astro`)

Currently a large imperative view/edit switch.

- `AlpakaDetail.svelte`: reads `?id=` from the URL on mount, fetches
  `/api/alpakas/{id}`, renders the read-only card (photo/placeholder, name,
  age, birth date), the edit button, and toggles a reactive edit form
  (`PUT /api/alpakas/{id}` multipart with the same validations). Events section
  renders `<EventList showAlpakaNames={false} />` once loaded.
- `alpakas.astro` page keeps `<DashboardLayout>` and mounts the island.
- Preserve the 404 / missing-id / error states and the 15 MB photo check.

## 5. Messages inbox (`src/pages/messages.astro`)

The largest migration (plan `003` built this page as imperative vanilla JS).

- `MessagesPage.svelte`: `$state` for `messages`, `filter`, `sortKey`,
  `sortDir`; one derived visible+sorted list rendered with `{#each}`. Keep
  `Intl.Collator('de', { sensitivity: 'base', numeric: true })` sorting,
  spam/old markers, filter segmented control (`aria-pressed`), summary line
  (`aria-live`), two-line clamp + `Mehr/Weniger` toggle (measure overflow with a
  `$effect`/`bind:clientWidth`), delete flow (`DELETE /api/messages/{id}` with
  `confirm`), sticky header, and loading/empty/error states.
- Icons `Trash2`, `ShieldAlert`, `MailX`, `ArrowUp`, `ArrowDown`, `ArrowUpDown`
  imported directly from `@lucide/svelte`.
- `messages.astro` page keeps `<DashboardLayout>` + the global-scope table styles
  and mounts `<MessagesPage client:only="svelte" />`. The hidden icon
  `<template>`s are deleted.

## 6. Gutscheine (`src/pages/gutscheine.astro` + `GutscheinListe.astro`)

- `GutscheinListe.svelte` + `GutscheinePage.svelte` (or one component): fetch
  `/api/gutscheine`, render the table with sums (`formatCurrency`), propose the
  next number (`suggestNextGutscheinnummer`), the create form (POST
  `/api/gutscheine`), and the redeem `<dialog>` (POST
  `/api/gutscheine/{nr}/einloesen`, min=kaufdatum validation). Keep
  `normalizeGutschein` for the camel/Pascal case fields.
- `gutscheine.astro` keeps `<DashboardLayout>` + page shell and mounts the island.
- Remove `GutscheinListe.astro`/`Card.astro` usage; `Card` is now Svelte.

## 7. Navbar (`src/components/DashboardNavbar.astro`)

- `DashboardNavbar.svelte`: port the mobile toggle (`aria-expanded`, `.open`
  class) and the `/.auth/me` user fetch (name + GitHub avatar). The logo and
  links stay as-is. Wrap the whole nav in the island, or keep the static markup
  in the Astro component and only island the script — prefer the full Svelte
  component for consistency, mounted from `DashboardLayout` via
  `<DashboardNavbar client:only="svelte" />`.
- Keep `astro:assets` `<Image>` for the logo; Svelte can still render Astro
  components only through wrappers, so if the logo stays in an Astro wrapper it
  stays static — decide during implementation, no functional difference.

## 8. Dependency & tooling touches

- `svelte`, `@astrojs/svelte`, `layerchart`, `d3-*`, `@lucide/svelte` are already
  installed (from plan `004`); add `svelte-check` as a devDependency and a
  `"check:svelte"` (or fold into the existing `check`) script if it does not
  duplicate `astro check` coverage. `astro check` already type-checks Svelte
  files via the integration.
- No change to `astro.config.mjs` (integration present), `tsconfig.json`,
  `staticwebapp.config.json`, or the CI workflow (it runs `pnpm run check` +
  `pnpm run build`, which we keep green).

## 9. Verification

- `cd src/dashboard && pnpm run build` (runs `astro check`)
- `cd src/dashboard && pnpm exec svelte-check` (if added)
- `git status`/`git diff` review before opening the PR
- Manual browser pass against a locally running Functions host + SWA emulator:
  add/edit alpaka, add event, sort/filter/delete a message, create + redeem a
  gutschein, mobile nav toggle, `/403` page still renders, chart widget on the
  root still works (regression check for the islands pattern).

## Known limitations / notes

- The dashboard keeps two platforms (Astro shell + Svelte islands) by design; the
  alternative full SvelteKit rewrite is deferred and this work does not block it
  (the Svelte components are framework-portable).
- `client:only="svelte"` islands render nothing until hydrated; pages that
  currently show server-rendered shells (e.g. `gutscheine` SSR table) will show
  the island's own loading state instead — acceptable for an auth-gated tool, but
  noted as a visible change.
- `dashboard-api.Tests`/`website-api.Tests` are referenced in `alpakasoelde.slnx`
  and `AGENTS.md` but not present in the working tree; this plan is frontend-only,
  so verification relies on builds, `astro check`, `svelte-check`, and manual QA.
- Some `:global(...)` selectors in migrated components reach into shared layout
  classes (`global.css`); keep those references intentional and minimal.
