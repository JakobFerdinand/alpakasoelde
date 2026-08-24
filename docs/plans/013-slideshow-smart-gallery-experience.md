# Slideshow "Smart Gallery Experience" Implementation Plan

**Implements:** `docs/concepts/design-concept-3-smart-gallery-experience.md` (all three phases as ONE coherent build)
**Scope:** `src/website` only — replaces the main-branch `setInterval` crossfade `Slideshow.astro` outright; touches its two call sites and adds a 1-line `js` bootstrap in `Layout.astro`.
**Branch basis:** `main` lineage (original crossfade component). Competing concept branches `feat/slideshow-concept-1/-2` and prototype branch `feature/slideshow` are ignored entirely.

---

## Context

The current `src/website/src/components/Slideshow.astro` (73 LOC) stacks all slides absolutely in a fixed 24/32 rem box and crossfades on a blind 4 s `setInterval`. It runs off-screen and in background tabs, offers no swipe, no captions, one generic alt text (`"Alpaka auf der Alpakasölde Farm"`), no inspection affordance, and no sharing. Both call sites deserve better, but differently:

- `/produkte` — six shop items (Strickgabel, Zauberwolle, Wolle Amadeus, Karten, Polster, Wollpellets) that visitors want to **inspect** before visiting the Hofladen.
- `/alpaka-wanderungen` — four hike photos whose job is **atmosphere** between info sections.

Source-image audit (verified with sharp, relevant for fit/loading decisions):

| Page | Files | Intrinsic sizes |
| --- | --- | --- |
| produkte | Karten.jpg, Polster.jpg, Zauberwolle.jpg, Wollpellets.jpg | 4032×3024 (landscape 4:3) |
| produkte | Strickgabel.jpeg | 1709×2392 (portrait) |
| produkte | Wolle_Amadeus.jpg | 3024×4032 (portrait) |
| wanderungen | wanderung1/2.jpg | 1536×2048 (portrait) |
| wanderungen | wanderung3.jpg | 2048×1536 (landscape) |
| wanderungen | wanderung4.jpg | 900×1600 (tall portrait) |

Mixed aspect ratios on **both** pages confirm the concept's split: product stage uses `object-fit: contain` over a token backdrop (nothing cropped), story stage uses `object-fit: cover` (immersion beats completeness). Three of four hike photos are portrait, which matters for the cinematic 21/9 desktop stage → see CSS spec (`object-position: center 40%`, matching the site-wide precedent in `ImpressionBreak`).

Environment facts this plan relies on: Astro ^7.2.4 with sharp 0.35.3 (`<Image />` supports `widths`/`sizes`/`format`/`fetchpriority`), strict tsconfig, zero client-side framework code on `src/website` (repo rule — no islands, no Swiper/embla), self-hosted pageview beacon in `Layout.astro:92-131` (production-host-gated `sendBeacon('/api/pageview', …)`), and **no existing `html.js` gate** (grep verified) — this plan adds it. `package.json` declares a Playwright script but no test files exist; frontend verification therefore stays `pnpm run check && pnpm run build` + manual matrix (per AGENTS.md).

### Locked decisions (summary)

| # | Decision | Rationale anchor |
| --- | --- | --- |
| D1 | File architecture: `Slideshow.astro` becomes thin dispatcher + `gallery/GalleryProduct.astro` + `gallery/GalleryStory.astro` + one hoisted `gallery/gallery.ts` module (one download, N instances) | Concept §9.1 |
| D2 | Indicators: thumbnails (horizontal strip) for product **≥ 768 px**; dots + fraction counter „3 von 6" for product **< 768 px** and for story (all widths) | §Markup spec, argued |
| D3 | Loop model: bounded swipe + wrapping buttons (`loop: 'wrap-buttons'`) everywhere; no DOM cloning, ever; story autoplay wraps the same way | Concept §5.4 |
| D4 | State: single `currentIndex` per instance; `scrollend` is truth-input (Baseline Dec 2025), 120 ms debounced `scroll` fallback for Safari/iOS < 26.2; `suppress` flag prevents programmatic-scroll echo loops | Concept §6 |
| D5 | Deep links: `#{id}-{n}` (1-based, e.g. `#produkte-3`), read on load with instant scroll, written via `history.replaceState` only, `hashchange` honored; story galleries never write hashes | Concept §6 |
| D6 | Loading: slide 1 `eager` + `fetchpriority="high"` + `decoding="sync"` (LCP candidate on both pages), slides 2+ `lazy`; width ladders computed from real layouts (§Loading strategy); CLS reservation via CSS `aspect-ratio`; dominant-color placeholders are a Phase 3 stretch goal — Phase 1 ships flat `--schurwolle` | Concept §7 |
| D7 | Lightbox: native `<dialog>` + `showModal()` (top layer, implicit focus trap), pinch-zoom viewport via `overflow:auto` + `touch-action`, hi-res rendition injected via `data-src` swap on first open (deterministic — not relied on `lazy` inside a closed dialog) | Concept §5.5 |
| D8 | Controls (buttons, dots, thumbs, counter chrome) are server-rendered but hidden unless `html.js` is set by a 1-line inline script added to `Layout.astro` — no dead UI without JS | Concept §5.2 |
| D9 | Icons (chevrons, magnifier, close, play/pause): hand-inlined minimal SVG paths — no new npm dependency on the marketing site (the `@lucide/svelte` rule applies to the dashboard only) | Repo rules |
| D10 | Autoplay: story only, 6000 ms, IntersectionObserver ≥ 50 % gate, paused on hover/focus-within/pointerdown/`visibilitychange`, permanently stopped after first manual interaction (event `gallery_autoplay_stop`), fully disabled under `prefers-reduced-motion: reduce` | Concept §4.2 |

---

## Component API

Exact interface — frozen; pages own content (static imports + German copy), component owns behavior. Lives in `Slideshow.astro` frontmatter and is re-exported by the gallery modules.

```ts
import type { ImageMetadata } from 'astro';

export type GalleryVariant = 'product' | 'story';

export interface GallerySlide {
  /** Statically imported image (astro:assets metadata). Required. */
  src: ImageMetadata;
  /** REQUIRED, specific German alt text (no generic farm boilerplate). */
  alt: string;
  /** Short caption line; product: below stage, story: scrim overlay. */
  caption?: string;
  /** 1–2 sentences, product variant only (rendered under the title). */
  description?: string;
  /** Badge chip text, e.g. „Neu", „Bestseller" — product variant only. */
  badge?: string;
}

export interface Props {
  slides: GallerySlide[];
  variant?: GalleryVariant;          // default 'product'
  id?: string;                       // stable kebab id → deep link #{id}-{n};
                                     // REQUIRED when lightbox/deep-links wanted; must match /^[a-z][a-z0-9-]*$/
  autoplay?: boolean;                // default: true for story, false for product
  intervalMs?: number;               // default 6000
  loop?: boolean | 'wrap-buttons';   // default 'wrap-buttons'; `true`/`false` coerce to bounded+wrap behavior
                                     // (only the documented default ships; other values throw in dev)
  showThumbnails?: boolean;          // product default true (renders ≥768 px; below that dots+fraction regardless)
  lightbox?: boolean;                // product default true, story false (hard-off in story)
}
```

Behavioral defaults derived per variant (not new props):

| Aspect | `variant="product"` | `variant="story"` |
| --- | --- | --- |
| Stage ratio | 1/1 mobile, 4/3 ≥ 768 px (max-width 900 px) | 16/10 mobile, 21/9 ≥ 1024 px, full-bleed |
| Fit | `contain` on `--schurwolle` backdrop | `cover`, `object-position: center 40%` |
| Nav UI | prev/next buttons always (JS-gated); thumbs ≥ 768 px; dots + „n von m" < 768 px | dots (subtle) + play/pause button |
| Hash writes | yes (if `id` set) | never |
| Lightbox | yes (if `lightbox !== false` and `id` set) | never |

Validation: dev-time assertions (`import.meta.env.DEV`) — `slides.length >= 2`, unique/known `variant`, `id` pattern, `badge`/`description` ignored with a console warning in story variant. Build fails loudly via `astro check` types otherwise.

---

## Markup spec

Class prefix `gallery`, BEM-ish, shared by both variants; variant modifier class + `data-*` state attributes drive CSS. Everything server-rendered; JS only enhances.

### Product variant skeleton (`GalleryProduct.astro`)

```astro
<section
  class="gallery gallery--product"
  data-gallery
  data-gallery-id={id}
  data-variant="product"
  data-loop="wrap-buttons"
  aria-roledescription="Bildergalerie"
  aria-label="Produktfotos aus dem Hofladen"
>
  <div class="gallery__stage">
    <button type="button" class="gallery__nav gallery__nav--prev"
            aria-label="Vorheriges Bild" data-gallery-prev>{chevron-left svg}</button>

    <ul class="gallery__track" data-gallery-track tabindex="0" aria-label="Bilder durchblättern">
      {slides.map((s, i) => (
        <li class="gallery__slide" role="group"
            aria-roledescription="Folie"
            aria-label={`${i + 1} von ${slides.length}`}>
          <Image
            src={s.src} alt={s.alt}
            widths={[480, 768, 1080, 1440]}
            sizes="(min-width: 768px) min(900px, calc(100vw - 2rem)), calc(100vw - 2rem)"
            format={['avif', 'webp']}
            loading={i === 0 ? 'eager' : 'lazy'}
            fetchpriority={i === 0 ? 'high' : undefined}
            decoding={i === 0 ? 'sync' : 'async'}
          />
        </li>
      ))}
    </ul>

    <button type="button" class="gallery__nav gallery__nav--next"
            aria-label="Nächstes Bild" data-gallery-next>{chevron-right svg}</button>

    <p class="gallery__counter" aria-hidden="true">
      <span data-gallery-counter-current>1</span> von <span data-gallery-counter-total>{slides.length}</span>
    </p>

    <button type="button" class="gallery__zoom-btn" data-gallery-zoom-open
            aria-label="Bild vergrößern">{magnifier svg}</button>
  </div>

  {/* All captions stacked in one grid cell — tallest wins, zero CLS on swap */}
  <div class="gallery__meta" data-gallery-meta aria-live="polite">
    {slides.map((s, i) => (
      <div class="gallery__caption" data-gallery-caption data-index={i}>
        <h3 class="gallery__title">
          {s.caption ?? fallbackTitle(s.alt)}
          {s.badge && <span class="gallery__badge">{s.badge}</span>}
        </h3>
        {s.description && <p class="gallery__desc">{s.description}</p>}
      </div>
    ))}
  </div>
  {/* Visually-hidden richer status; the polite live channel */}
  <p class="visually-hidden" data-gallery-status></p>

  <div class="gallery__indicator" role="tablist" aria-label="Bild wählen">
    {slides.map((s, i) => (
      <button type="button" class="gallery__dot" data-gallery-dot data-index={i}
              aria-label={`Zu Bild ${i + 1}: ${s.caption ?? s.alt}`}></button>
    ))}
  </div>

  <div class="gallery__thumbs" data-gallery-thumbs>
    {slides.map((s, i) => (
      <button type="button" class="gallery__thumb" data-gallery-thumb data-index={i}
              aria-label={`Zu Bild ${i + 1}: ${s.caption ?? s.alt}`}>
        <Image src={s.src} alt="" width={96} height={96} widths={[96, 160]}
               format={['avif', 'webp']} loading="lazy" decoding="async" />
      </button>
    ))}
  </div>

  <dialog class="gallery__lightbox" data-gallery-lightbox
          aria-label={`Bildansicht: ${slides[0].caption ?? slides[0].alt}`}>
    <div class="gallery__lightbox-bar">
      <p class="gallery__lightbox-count"><span data-gallery-lb-current>1</span> von {slides.length}</p>
      <button type="button" class="gallery__lightbox-close" data-gallery-lb-close
              aria-label="Schließen">{close svg}</button>
    </div>
    <div class="gallery__zoomport" data-gallery-zoomport tabindex="0">
      <img data-gallery-zoom-img alt="" decoding="async"
           data-src={/* filled per open slide, see Script spec */ ''} />
    </div>
    <div class="gallery__lightbox-foot">
      <button type="button" class="gallery__nav gallery__nav--prev"
              aria-label="Vorheriges Bild" data-gallery-lb-prev>{chevron svg}</button>
      <p class="gallery__lightbox-caption" data-gallery-lb-caption>{slides[0].caption ?? ''}</p>
      <button type="button" class="gallery__nav gallery__nav--next"
              aria-label="Nächstes Bild" data-gallery-lb-next>{chevron svg}</button>
    </div>
  </dialog>
</section>
```

Notes locked by this spec:

- **Indicator choice (argued, final):** thumbnails win ≥ 768 px in product mode because shoppers think in objects ("the yellow skein") and a 96 px square row is direct access; below 768 px they'd eat > 30 % of a 390 px viewport and shrink under 44 px tap rows, so **dots + fraction „3 von 6"** take over — orientation comes from the counter, jumping from dots. Story mode uses dots everywhere: ≤ 4 slides, atmosphere-first, no preview value. Fraction alone is rejected everywhere (not operable); dots alone in product mobile are acceptable only because the counter rides on top of them. Thumbs reuse the same source at `widths={[96,160]}` → browser cache hit, near-zero bytes.
- The counter is `aria-hidden="true"`: the announced status comes from the dedicated visually-hidden `data-gallery-status` live region („Bild 3 von 6: Strickgabel aus Holz") — richer than „3 von 6" and keeps the visible pill clean.
- `role="tablist"` on the dot group is deliberately **not** used — APG carousel treats dots as buttons over a scroll region, not tabs (no tabpanel semantics exists). Plain buttons with `aria-current="true"` on the active one. (The `role="tablist"` attribute in the skeleton above is therefore dropped in implementation; keep the `aria-label`.) Same for thumbs.
- Slide `role="group"` + `aria-roledescription="Folie"` + `{i} von {n}` label per APG carousel pattern; the track itself carries `tabindex="0"` (roving-focus root) and `aria-label="Bilder durchblättern"`.
- Buttons/dots/thumbs/counter-chrome/zoom-btn are wrapped by the `html:not(.js)` hide rule (D8). Without JS the visitor gets: swipeable native scroll gallery, correct first caption, semantic alts — strictly more than today's component.
- The lightbox `<dialog>` is inert-by-default; its `<img>` has **no** `src` until first open (`data-src` swap, D7) so closed-dialog markup costs zero bytes.

### Story variant skeleton (`GalleryStory.astro`)

```astro
<section
  class="gallery gallery--story"
  data-gallery data-gallery-id={id} data-variant="story" data-loop="wrap-buttons"
  data-autoplay={autoplay ? 'true' : 'false'} data-interval-ms={intervalMs}
  aria-roledescription="Bildergalerie"
  aria-label="Eindrücke von der Alpakawanderung"
>
  <div class="gallery__stage">
    {prev/next buttons identical to product}
    <ul class="gallery__track" data-gallery-track tabindex="0" aria-label="Bilder durchblättern">
      {slides.map((s, i) => (
        <li class="gallery__slide" role="group" aria-roledescription="Folie"
            aria-label={`${i + 1} von ${slides.length}`}>
          <Image src={s.src} alt={s.alt}
                 widths={[480, 768, 1080, 1440, 1920]}
                 sizes="100vw"
                 format={['avif', 'webp']}
                 loading={i === 0 ? 'eager' : 'lazy'}
                 fetchpriority={i === 0 ? 'high' : undefined}
                 decoding={i === 0 ? 'sync' : 'async'} />
          <p class="gallery__overlay" data-gallery-caption data-index={i}>
            <span>{s.caption}</span>
          </p>
        </li>
      ))}
    </ul>
    {counter pill omitted (dots carry position)}
    <button type="button" class="gallery__playpause" data-gallery-toggle
            aria-label="Diashow pausieren" aria-pressed="false">{pause/play svg}</button>
  </div>
  <div class="gallery__indicator">…dots as above…</div>
  <p class="visually-hidden" data-gallery-status></p>
</section>
```

No lightbox, no thumbs, no hash UI in story mode. Overlay caption lives inside the slide (scrim travels with the photo).

---

## CSS spec (snippets)

All styles scoped inside each variant component (repo rule); design tokens come from `global.css` unchanged — no new global CSS except nothing. Colors used: `--bluetenhonig #e1b14a` (accent gold), `--schurwolle #fbf7ed` (cream), `--himmelblau #8da5d3`, `--taubenblau #4b5b73` (deep slate blue), plus `--schwarz #1f1f1d`.

### Snap geometry

```css
.gallery__track {
  display: flex;
  margin: 0; padding: 0;
  list-style: none;
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  scroll-behavior: smooth;           /* overridden by reduced-motion block */
  overscroll-behavior-x: contain;    /* don't chain-swipe the page horizontally */
  scrollbar-width: none;             /* dots/thumbs are the scrollbar */
  -webkit-overflow-scrolling: touch; /* legacy iOS momentum */
}
.gallery__track::-webkit-scrollbar { display: none; }

.gallery__slide {
  flex: 0 0 100%;
  scroll-snap-align: center;
  scroll-snap-stop: always;          /* no multi-slide flings skipping products */
}

.gallery--product .gallery__slide {
  aspect-ratio: 1 / 1;               /* CLS reservation, mobile */
  background: var(--schurwolle);     /* letterbox backdrop for contain-fit */
}
@media (min-width: 768px) {
  .gallery--product .gallery__slide { aspect-ratio: 4 / 3; }
}
.gallery--product .gallery__slide img {
  position: absolute;                /* slide li gets position:relative */
  inset: 0;
  width: 100%; height: 100%;
  object-fit: contain;
}

.gallery--story .gallery__slide { aspect-ratio: 16 / 10; }
@media (min-width: 1024px) {
  .gallery--story .gallery__slide { aspect-ratio: 21 / 9; max-height: 80vh; }
}
.gallery--story .gallery__slide img {
  width: 100%; height: 100%;
  object-fit: cover;
  object-position: center 40%;       /* portrait hike photos crop sky, keep subjects */
}
```

Stage width caps: `.gallery--product { max-width: 900px; margin-inline: auto; }` (inside the page column); story section is full-bleed (call site renders it outside `.container`).

### Prev/next buttons — ≥ 44 px pills that carry their own contrast

```css
.gallery__nav {
  position: absolute;
  top: 50%;
  translate: 0 -50%;
  inline-size: 48px; block-size: 48px;         /* ≥ 44×44 required */
  display: grid; place-items: center;
  border: 0; border-radius: 50%;
  background: rgb(75 91 115 / 0.92);            /* --taubenblau @ .92 */
  color: var(--schurwolle);
  cursor: pointer;
  padding: 0;
}
.gallery__nav--prev { left: max(0.5rem, env(safe-area-inset-left)); }
.gallery__nav--next { right: max(0.5rem, env(safe-area-inset-right)); }
.gallery__nav svg { inline-size: 24px; block-size: 24px; }
```

Alpha is **0.92, not the concept's 0.85**: measured worst case over a pure-white photo region, 0.92 yields ≈ 5.3 : 1 for cream foreground — clears WCAG AA *text* (4.5 : 1), which matters because these pills also appear next to text-bearing chrome; 0.85 would have given ≈ 4.37 : 1 (passes only the 3 : 1 non-text bar). See contrast table in the A11y checklist.

### Thumbnail strip (product ≥ 768 px)

```css
.gallery__thumbs { display: none; }
@media (min-width: 768px) {
  .gallery__thumbs {
    display: flex;
    gap: 0.5rem;
    margin-top: 1rem;
    overflow-x: auto;                 /* future-proof past ~10 products */
    scroll-snap-type: x proximity;
    padding-bottom: 0.25rem;
  }
  .gallery__thumb {
    flex: 0 0 auto;
    inline-size: 72px; block-size: 72px;   /* hit area; visual inner img 64px */
    padding: 3px;
    border: 0;
    border-radius: 0.375rem;
    background: transparent;
    opacity: 0.55;
    filter: saturate(0.7);
    cursor: pointer;
  }
  .gallery__thumb[aria-current="true"] {
    opacity: 1;
    filter: none;
    box-shadow: inset 0 0 0 2px var(--taubenblau); /* boundary ≥ 3:1 on cream */
    outline: 2px solid var(--bluetenhonig);        /* brand accent ring outside */
    outline-offset: 1px;
  }
  .gallery__thumb img { inline-size: 100%; block-size: 100%; object-fit: cover; border-radius: 0.25rem; }
}
```

Gold-ring caveat (deviation from concept wording): `--bluetenhonig` on `--schurwolle` measures **1.85 : 1** — invisible as a sole boundary indicator. Final pairing: dark `--taubenblau` inset shadow carries the ≥ 3 : 1 boundary, the gold outline is decorative accent, and state redundancy comes from `opacity/filter` dimming + `aria-current` + the counter. Below 768 px the strip is `display:none` (JS also skips wiring it there via `matchMedia`, but CSS remains authoritative).

### Dots (mobile product + all-widths story)

```css
.gallery__indicator { display: flex; justify-content: center; gap: 0.25rem; margin-top: 0.75rem; }
.gallery__dot {
  inline-size: 44px; block-size: 44px;      /* full-size hit target … */
  display: grid; place-items: center;
  background: transparent; border: 0; padding: 0;
}
.gallery__dot::after {                       /* …visual dot drawn small inside */
  content: '';
  inline-size: 10px; block-size: 10px;
  border-radius: 50%;
  background: var(--taubenblau);
  opacity: 0.3;                              /* de-emphasized; state not carried by dots alone */
  transition: opacity 200ms, translate 200ms;
}
.gallery__dot[aria-current="true"]::after {
  opacity: 1;
  translate: 0 -1px;
  outline: 2px solid var(--himmelblau);
  outline-offset: 2px;
}
```

Active dot on the cream page background: `--taubenblau` ≈ 6.4 : 1 ✓. Inactive dots at 0.3 alpha are intentionally sub-threshold — position state is redundantly carried by the counter/live region and `aria-current`; dots are a convenience control, and dimming inactive pagination is established practice.

### Counter pill, badge chip, caption stack (product)

```css
.gallery__counter {
  position: absolute;
  top: 0.75rem; right: 0.75rem;
  margin: 0;
  padding: 0.25rem 0.75rem;
  border-radius: 999px;
  background: var(--taubenblau);              /* SOLID — text-bearing, needs 4.5:1 over any photo */
  color: var(--schurwolle);
  font-size: 0.875rem;
  font-variant-numeric: tabular-nums;         /* no jitter while counting */
}

.gallery__badge {
  display: inline-block;
  margin-left: 0.5rem;
  padding: 0.125rem 0.625rem;
  border-radius: 999px;
  background: var(--bluetenhonig);
  color: var(--schwarz);                      /* NOT --taubenblau: 3.5:1 failed AA; schwarz = 8.3:1 */
  font-size: 0.75rem;
  font-weight: 400;
  vertical-align: middle;
}

.gallery__meta { display: grid; margin-top: 1rem; text-align: center; }
.gallery__caption {
  grid-area: 1 / 1;                           /* stacked: tallest caption reserves height → zero CLS */
  visibility: hidden;
  opacity: 0;
  transition: opacity 300ms, visibility 300ms;
}
.gallery__caption[data-active] { visibility: visible; opacity: 1; }
```

Badge text color is a deliberate correction of the concept (which specified `--taubenblau` on gold = 3.48 : 1, failing AA for chip-sized text). `--schwarz #1f1f1d` on `--bluetenhonig` = 8.32 : 1 ✓ while keeping the gold brand chip look.

### Story scrim over photos

```css
.gallery__overlay {
  position: absolute;
  inset: auto 0 0 0;
  margin: 0;
  padding: 3rem 1.25rem 1rem;
  color: var(--schurwolle);
  background: linear-gradient(
    to top,
    rgb(75 91 115 / 0.92) 0%,
    rgb(75 91 115 / 0.55) 55%,
    rgb(75 91 115 / 0) 100%
  );
  font-size: 1.0625rem;
}
```

Text sits in the bottom band where alpha ≈ 0.92 → same ≥ 5 : 1 worst-case math as the nav pills, independent of the photo beneath. Caption swaps use the same `[data-active]` visibility/opacity pattern as product (stacked overlays inside each slide).

### Focus-visible rings

```css
.gallery__track:focus-visible,
.gallery__zoomport:focus-visible {
  outline: 2px solid var(--taubenblau);
  outline-offset: 2px;                        /* 6.4:1 on cream — the roving-focus root */
}
.gallery__nav:focus-visible,
.gallery__playpause:focus-visible,
.gallery__lightbox-close:focus-visible {
  outline: 2px solid var(--bluetenhonig);     /* gold on the dark taubenblau pills ≈ 3.5:1 ✓ */
  outline-offset: 2px;
}
.gallery__dot:focus-visible,
.gallery__thumb:focus-visible,
.gallery__zoom-btn:focus-visible {
  outline: 2px solid var(--taubenblau);
  outline-offset: 1px;
}
```

### Reduced motion

```css
@media (prefers-reduced-motion: reduce) {
  .gallery__track { scroll-behavior: auto; }          /* instant jumps, incl. programmatic scrollTo */
  .gallery__caption,
  .gallery__overlay { transition: none; }
  .gallery__dot::after { transition: none; }
  .gallery__lightbox[open] { animation: none; }
}
```

JS additionally reads `matchMedia('(prefers-reduced-motion: reduce)')` once (with a `change` listener) to force `'auto'` behavior in every `scrollTo` and to disable autoplay outright (D10).

### JS-gated chrome (zero-JS baseline enforcement)

```css
/* Default: no interactive chrome without JS. Native scroll/swipe remains. */
.gallery__nav, .gallery__indicator, .gallery__thumbs,
.gallery__counter, .gallery__zoom-btn, .gallery__playpause { display: none; }

html.js .gallery__nav,
html.js .gallery__indicator,
html.js .gallery__counter,
html.js .gallery__zoom-btn { display: <grid/flex per component>; }
@media (min-width: 768px) { html.js .gallery__thumbs { display: flex; } }
html.js .gallery__playpause { display: grid; }   /* story only */
```

Enabled by one line added to `Layout.astro` `<head>` (before any paint, avoids FOUC):

```html
<script is:inline>document.documentElement.classList.add('js');</script>
```

### `@supports`-gated polish (Phase 3 features shipped behind silent gates)

```css
/* Dialog entry animation where @starting-style exists */
@supports (transition-behavior: allow-discrete) {
  .gallery__lightbox[open] {
    opacity: 1;
    transition: opacity 250ms, overlay 250ms allow-discrete, display 250ms allow-discrete;
  }
  @starting-style {
    .gallery__lightbox[open] { opacity: 0; }
  }
}

/* Stage → lightbox morph on View-Transition-capable engines (same-document VT, Baseline Oct 2025) */
@media (prefers-reduced-motion: no-preference) {
  .gallery__slide img { view-transition-name: none; }        /* opt-in per open, see script */
}
```

The morph works by JS assigning `view-transition-name: gallery-hero-{id}` to the active slide image just before `document.startViewTransition(() => dialog.showModal())` and the matching name to the lightbox image, removing it after `finish` — names stay unique per gallery instance and are cleaned up to avoid duplicate-name VT skips. Engines without `startViewTransition` get a plain open (silent degradation). Optional Chromium-only `::scroll-marker()` upgrade stays **out of scope**, per concept §8 rejection as primary mechanism.

---

## Script spec

One module, `src/website/src/components/gallery/gallery.ts`, imported by both variant components via `<script>` — Astro bundles and dedupes it, so two galleries on a page still mean one download. Target **≤ 3 KB gzipped**, zero imports beyond DOM APIs.

### Outline (annotated; ~190 LOC unminified)

```ts
type Trigger = 'swipe' | 'button' | 'thumb' | 'key' | 'autoplay';

const prefersReducedMotion = () =>
  matchMedia('(prefers-reduced-motion: reduce)').matches;

function initGallery(root: HTMLElement): void {
  const track  = root.querySelector<HTMLUListElement>('[data-gallery-track]');
  if (!track || track.dataset.bound) return;   // double-init guard
  track.dataset.bound = 'true';

  const slides   = Array.from(track.children) as HTMLElement[];
  const count    = slides.length;
  const variant  = root.dataset.variant as 'product' | 'story';
  const galleryId = root.dataset.galleryId ?? '';
  const canWrap  = root.dataset.loop === 'wrap-buttons';
  const writable = variant === 'product' && !!galleryId;

  let index = 0;
  let suppress = false;                        // programmatic-scroll echo guard

  const slideWidth = () => slides[0].offsetWidth;

  const clampIndex = (i: number) =>
    canWrap ? (i + count) % count : Math.max(0, Math.min(count - 1, i));

  // ---- render: derive ALL ui from currentIndex -------------------------
  const render = (trigger?: Trigger) => {
    root.querySelectorAll('[data-gallery-dot],[data-gallery-thumb]').forEach(el => {
      const active = Number((el as HTMLElement).dataset.index) === index;
      el.toggleAttribute('aria-current', active);   // aria-current="true"
      el.setAttribute('tabindex', active ? '0' : '-1'); // roving tabindex in indicator groups
    });
    root.querySelectorAll('[data-gallery-caption]').forEach(el =>
      el.toggleAttribute('data-active', Number((el as HTMLElement).dataset.index) === index));
    const cur = root.querySelector('[data-gallery-counter-current], [data-gallery-lb-current]');
    if (cur) cur.textContent = String(index + 1);
    const status = root.querySelector('[data-gallery-status]');
    if (status) status.textContent =
      `Bild ${index + 1} von ${count}: ${slides[index].querySelector('img')?.alt ?? ''}`;
    if (writable && history.replaceState)
      history.replaceState(null, '', `#${galleryId}-${index + 1}`);
    if (trigger) emit('slide_change', { galleryId, variant, index: index + 1, trigger });
  };

  // ---- navigation ------------------------------------------------------
  const goTo = (i: number, trigger?: Trigger, behavior: ScrollBehavior = 'smooth') => {
    index = clampIndex(i);
    suppress = true;
    track.scrollTo({ left: index * slideWidth(), behavior: prefersReducedMotion() ? 'auto' : behavior });
    render(trigger);
  };

  // ---- index from scroll: scrollend, debounced-scroll fallback ---------
  const syncFromScroll = () => {
    if (suppress) { suppress = false; return; }
    const next = Math.round(track.scrollLeft / slideWidth());
    if (next !== index) { index = clampIndex(next); render('swipe'); }
  };
  if ('onscrollend' in track) {
    track.addEventListener('scrollend', syncFromScroll);
  } else {
    let t = 0;
    track.addEventListener('scroll', () => {
      clearTimeout(t);
      t = setTimeout(syncFromScroll, 120) as unknown as number;
    }, { passive: true });
  }

  // ---- buttons / dots / thumbs ----------------------------------------
  root.querySelector('[data-gallery-prev]')?.addEventListener('click', () => goTo(index - 1, 'button'));
  root.querySelector('[data-gallery-next]')?.addEventListener('click', () => goTo(index + 1, 'button'));
  root.querySelectorAll('[data-gallery-dot],[data-gallery-thumb]').forEach(el =>
    el.addEventListener('click', () => goTo(Number((el as HTMLElement).dataset.index), 'thumb')));

  // ---- keyboard (roving focus root = track) ---------------------------
  root.addEventListener('keydown', (e) => {
    const map: Record<string, () => void> = {
      ArrowLeft:  () => goTo(index - 1, 'key'),
      ArrowRight: () => goTo(index + 1, 'key'),
      Home:       () => goTo(0, 'key'),
      End:        () => goTo(count - 1, 'key'),
    };
    const fn = map[e.key];
    if (fn) { e.preventDefault(); fn(); return; }
    if (variant === 'product' && (e.key === 'Enter' || e.key === ' ') &&
        e.target instanceof Element && e.target.closest('.gallery__stage')) {
      e.preventDefault(); openLightbox();
    }
  });

  // ---- lightbox (product only) ----------------------------------------
  const dialog = root.querySelector('dialog');
  const zoomImg = root.querySelector<HTMLImageElement>('[data-gallery-zoom-img]');
  const zoomport = root.querySelector('[data-gallery-zoomport]');
  let lastFocus: Element | null = null;

  function openLightbox(): void {
    if (!dialog || !zoomImg) return;
    lastFocus = document.activeElement;
    zoomImg.alt = track!.querySelectorAll('img')[index].alt;
    zoomImg.dataset.src = hiResSrc(index);          // widths=[1920,2400] rendition
    if (!zoomImg.src) zoomImg.src = zoomImg.dataset.src;   // load once, on first open
    zoomImg.style.viewTransitionName = `gallery-hero-${galleryId}`;
    const open = () => { dialog.showModal(); emit('lightbox_open', { galleryId, index: index + 1 }); };
    'startViewTransition' in document && !prefersReducedMotion()
      ? (document as Document & { startViewTransition(cb: () => void): void }).startViewTransition(open)
      : open();
    dialog.setAttribute('aria-label', `Bildansicht: ${currentCaption()}`);
    dialog.querySelector('[data-gallery-lb-caption]')!.textContent = currentCaption();
    syncLbCounter();
  }
  // close: [data-gallery-lb-close] click and dialog 'close' event →
  //   remove view-transition-name, emit('lightbox_close'), restore focus:
  //   lastFocus instanceof HTMLElement && lastFocus.focus()   (showModal()
  //   usually restores the invoker automatically; this is the explicit fallback)
  // lb prev/next: goTo(index ± 1) then re-point zoomImg to the neighbor's
  //   hi-res rendition (already cached after first visit per slide)
  // zoomport keydown Enter/Space → toggle class 'is-zoomed' (img width 100% ↔ 200%);
  //   dblclick same toggle (desktop). Pinch handled natively via touch-action.

  // ---- autoplay (story only) ------------------------------------------
  // setInterval(intervalMs) tick → goTo(index + 1, 'autoplay');
  // gates: IntersectionObserver(threshold .5) starts/stops with visibility;
  // pause on mouseenter/'focusin'/pointerdown on root; resume on mouseleave/focusout
  // UNLESS stopped-permanent flag set;
  // document visibilitychange hidden → pause;
  // FIRST user-triggered goTo (trigger !== 'autoplay') → permanent stop +
  //   emit('gallery_autoplay_stop') + swap toggle-button label
  //   „Diashow pausieren" ↔ „Diashow abspielen" (aria-pressed mirrors state);
  // prefersReducedMotion() at init → controller never constructed.

  // ---- hover-intent preload (fine pointers only) ----------------------
  // matchMedia('(hover:hover) and (pointer:fine)') → pointerenter on next/prev:
  //   const img = new Image(); img.src = neighborSlideImg.currentSrc;

  // ---- hash bootstrap & external hashchange ---------------------------
  if (writable) {
    const m = location.hash.match(new RegExp(`^#${galleryId}-(\\d+)$`));
    if (m) {
      const n = Number(m[1]);
      if (n >= 1 && n <= count) {
        index = n - 1;
        suppress = true;
        track.scrollTo({ left: index * slideWidth(), behavior: 'instant' }); // before paint
        render();
      }
    }
    addEventListener('hashchange', () => { /* re-match, goTo(matched, undefined) */ });
  }

  // ---- analytics hook: dispatch only, no tracking ---------------------
  function emit(name: 'slide_change' | 'lightbox_open' | 'lightbox_close' | 'gallery_autoplay_stop',
                detail: Record<string, unknown>): void {
    dispatchEvent(new CustomEvent(name, { detail: { path: location.pathname, ...detail } }));
  }
}

for (const root of document.querySelectorAll<HTMLElement>('[data-gallery]')) initGallery(root);
```

Payload contracts (consumed by a future forwarding listener beside the `Layout.astro` beacon — **not part of this work**, describe-only per concept §9.4):

| Event | `detail` |
| --- | --- |
| `slide_change` | `{ path, galleryId, variant, index /* 1-based */, trigger: 'swipe'\|'button'\|'thumb'\|'key'\|'autoplay' }` |
| `lightbox_open` / `lightbox_close` | `{ path, galleryId, index }` |
| `gallery_autoplay_stop` | `{ path, galleryId }` |

No cookies, storage, network calls, or PII — pure DOM CustomEvents; ignoring them costs nothing.

### Zero-JS baseline statement

With JavaScript disabled: the gallery is a native horizontal scroll-snap carousel — touch swipe and trackpad/wheel scrolling work in every engine; slide 1 is eager-loaded and its caption/badge/description are fully visible static DOM; all images keep their specific German alt texts; slides 2+ load lazily per native `loading="lazy"`; the page has no dead buttons (chrome hidden by the `html:not(.js)` gate), no autoplay timer, no lightbox, no hash churn. This is strictly better than the current component, which without JS still burns a timer showing slide 2 of N forever.

---

## State & deep-linking

- **Single source of truth:** `currentIndex` (module-closure variable per gallery instance). Scroll position is the input (`scrollend`, or 120 ms debounced `scroll` on engines without it — Safari/iOS < 26.2, ≈ 11 % share per concept §8); programmatic navigation is the output (`scrollTo`). The `suppress` flag marks self-initiated scrolls so the resulting `scrollend` doesn't re-render/re-emit; wrap-around (`clampIndex` modulo) happens **only** for button/key/autoplay triggers — swipes remain physically bounded at the ends (D3), keeping the DOM linear and screen-reader-honest (no clones, `aria-label` "i von n" always truthful).
- **Deep-link scheme:** `#{id}-{n}`, `n` 1-based → `#produkte-3` opens the third product. `id` is the page-chosen stable kebab string (`produkte` on `/produkte`).
- **Read on load:** regex match against the instance's `galleryId`; valid hits scroll the track with `behavior: 'instant'` synchronously during module init (script executes before first paint completes for above-the-fold galleries; `suppress` set so no echo render fires). Invalid/out-of-range indices are ignored silently.
- **Write on change:** `history.replaceState(null, '', '#produkte-4')` — **never** `pushState`: swiping six products must not create six history entries. Writes happen in `render()` only on committed changes (post-`scrollend`/post-`goTo`), so mid-fling positions never land in the URL.
- **`hashchange`:** paste/share while on page → navigate the matching gallery. Back/forward within the page therefore intentionally moves slides when hashes differ (documented caveat from concept §6: browser scroll restoration restores *page* offset independently; the gallery keys off the hash only).
- **Story galleries never write hashes** (no sharing value, no URL churn on ambient autoplay ticks — writing every 6 s would spam replaceState).
- Multiple galleries per page are safe: ids are namespaced by the `id` prop; the regex anchors on the exact prefix.

---

## Page integration

Both diffs keep the repo pattern: copy lives in the page, statically imported images stay, only the invocation changes shape.

### `src/website/src/pages/produkte.astro` — `variant="product"`

Frontmatter replacement for line 15:

```ts
const slides = [
  {
    src: Strickgabel,
    alt: 'Strickgabel aus Holz mit begonnener Wollkordel',
    caption: 'Strickgabel aus Holz',
    description: 'Für knotenfreies Stricken ohne Zählen.',
  },
  {
    src: Zauberwolle,
    alt: 'Knäuel Zauberwolle in kräftigen Buntfarben',
    caption: 'Zauberwolle',
    description: 'Bunte Schurwolle zum Häkeln mit der Strickgabel – ganz ohne Zählring.',
    badge: 'Bestseller',
  },
  {
    src: WolleAmadeus,
    alt: 'Handgesponnene Wolle Amadeus in Naturtönen',
    caption: 'Wolle Amadeus',
    description: 'Handgesponnen und kuschelweich – ein Naturklassiker für deine Projekte.',
  },
  {
    src: Karten,
    alt: 'Handgefertigte Grußkarten mit Alpakamotiv',
    caption: 'Grußkarten mit Alpakamotiv',
    description: 'Liebevoll gestaltete Karten für jeden Anlass.',
  },
  {
    src: Polster,
    alt: 'Kuschelweiches Polster gefüllt mit Alpakawolle',
    caption: 'Polster mit Alpakafüllung',
    description: 'Gemütliche Pölster, gefüllt mit heimischer Alpakawolle.',
  },
  {
    src: Wollpellets,
    alt: 'Wollpellets als natürlicher Dünger aus Schurwolle',
    caption: 'Wollpellets',
    description: 'Natürlicher Dünger aus reiner Schurwolle – für Garten und Beet.',
    badge: 'Neu',
  },
];
```

Template change (line 35):

```diff
-    <Slideshow images={images} />
+    <Slideshow variant="product" id="produkte" slides={slides} />
```

Badges („Bestseller", „Neu") are illustrative defaults flagged for owner review before ship — they're plain props, trivially removed. Captions/descriptions are drafted from the page's existing copy lines (:29–31) and the concept's alt-text list (§4.1); final wording is a content decision.

### `src/website/src/pages/alpaka-wanderungen.astro` — `variant="story"`

Frontmatter addition after the imports:

```ts
const slides = [
  {
    src: Wanderung1,
    alt: 'Alpakas auf dem Wanderweg durch die Innauen bei Ering',
    caption: 'Alpakawanderung durch die Innauen',
  },
  {
    src: Wanderung2,
    alt: 'Teilnehmerin führt ein Alpaka am Halfter',
    caption: 'An der Seite unserer Alpakas',
  },
  {
    src: Wanderung3,
    alt: 'Alpakagruppe auf der Weide vor dem Hof',
    caption: 'Kennenlernen bei einem Hofbesuch',
  },
  {
    src: Wanderung4,
    alt: 'Blick über die Innauen bei einer Alpakawanderung',
    caption: 'Natur pur zwischen Inn und Deich',
  },
];
```

Template change (line 51):

```diff
-  <Slideshow images={images} />
+  <Slideshow variant="story" id="wanderungen" slides={slides} autoplay={true} intervalMs={6000} />
```

(`id` is accepted but story never writes hashes; passing it keeps the DOM stable if the variant ever flips. The slideshow stays outside `.container` → full-bleed stage, unchanged placement between the Details and Ausflugstipps sections.)

### `src/website/src/layouts/Layout.astro`

One insertion in `<head>` (before `<Head />`), enabling the `html.js` chrome gate (D8):

```diff
   <head>
     <meta charset="UTF-8" />
     <meta name="viewport" content="width=device-width" />
+    <script is:inline>document.documentElement.classList.add('js');</script>
     <Head title={title} />
   </head>
```

No changes to the beacon script; the gallery CustomEvents are dispatched regardless and simply go unheard until a future forwarding listener lands (out of scope here).

---

## Accessibility checklist

Target: WCAG 2.1 AA, APG carousel + modal dialog patterns.

- [ ] Region semantics: `<section>` with `aria-roledescription="Bildergalerie"` + distinct German `aria-label` per variant („Produktfotos aus dem Hofladen" / „Eindrücke von der Alpakawanderung").
- [ ] Slides: `role="group"`, `aria-roledescription="Folie"`, `aria-label="{i} von {n}"` — truthful under bounded swipe (no clones).
- [ ] Roving focus: track `tabindex="0"` + `aria-label="Bilder durchblättern"`; `←`/`→`/`Home`/`End` handled; indicator groups use roving `tabindex` (`0` active / `-1` others).
- [ ] Buttons ≥ 44×44 px (spec'd 48), German names: „Vorheriges Bild", „Nächstes Bild", „Zu Bild {n}: {Name}" (dots + thumbs), „Bild vergrößern", „Schließen", „Diashow pausieren"/„Diashow abspielen".
- [ ] Active dot/thumb marked `aria-current="true"`; state additionally conveyed visually (dimming/ring) — never color alone.
- [ ] Live announcements: visually-hidden `data-gallery-status` (`aria-live="polite"`) announces „Bild 3 von 6: Strickgabel aus Holz" on committed change; visible counter pill is `aria-hidden` decoration.
- [ ] Autoplay (story only): starts only ≥ 50 % visible; pauses on hover/focus-within/pointerdown/background-tab; stops permanently on first manual interaction (WCAG 2.2.2); play/pause control persists; off entirely under reduced motion.
- [ ] Lightbox: `<dialog>` + `showModal()` → implicit `role="dialog"`, `aria-modal="true"`, background inert, native `Tab` cycle and `Escape`; `aria-label="Bildansicht: {Titel}"` kept in sync per slide; focus restored to invoker (native) with explicit `lastFocus.focus()` fallback; zoomport focusable (`tabindex="0"`) with its own focus ring.
- [ ] Reduced motion: no smooth scrolling, no caption fades, no dialog animation, no autoplay, no View-Transition morph.
- [ ] Keyboard-only walkthrough passes: reach gallery → arrows through slides → Enter opens lightbox → arrows navigate inside → Escape closes → focus back on stage/invoker.

### Contrast table (computed, WCAG relative luminance)

Pair | Ratio | Requirement | Verdict
--- | --- | --- | ---
`--schurwolle` text on solid `--taubenblau` (counter, dialog bar) | 6.45 : 1 | 4.5 : 1 text | ✅
`--schurwolle` icon/text on `--taubenblau` @ 0.92 over worst-case white photo | ≈ 5.3 : 1 | 4.5 : 1 | ✅
`--schurwolle` icon on `--taubenblau` @ 0.92 pill (buttons) | ≈ 5.3 : 1 | 3 : 1 graphics | ✅
`--schwarz` text on `--bluetenhonig` badge | 8.32 : 1 | 4.5 : 1 | ✅ (replaces concept's 3.48 : 1 pair)
Story scrim band @ 0.92 + `--schurwolle` caption over arbitrary photo | ≥ ≈ 5.3 : 1 | 4.5 : 1 | ✅
`--bluetenhonig` focus ring on `--taubenblau` pill | 3.48 : 1 | 3 : 1 non-text | ✅
`--taubenblau` focus ring / active-dot outline on `--schurwolle` page bg | 6.45 : 1 | 3 : 1 | ✅
`--taubenblau` inset + `--bluetenhonig` outline on active thumb vs cream | 6.45 : 1 (dark edge) | 3 : 1 boundary | ✅ (gold alone would be 1.85 : 1 — hence pairing)
Inactive dots @ 0.3 alpha on cream | < 3 : 1 | exempt* | intentional de-emphasis*

\* Non-text contrast applies to information required for identification; inactive pagination state is redundantly carried by `aria-current`, the counter, and the live region — mirroring standard pagination-dimming practice.

Screen-reader spot checks (manual): VoiceOver + iOS Safari, VoiceOver + macOS Safari/Chrome, NVDA + Firefox, TalkBack + Chrome Android.

---

## Performance checklist

Budgets (concept §7.2) and how each is met:

- [ ] **LCP ≤ 2.5 s p75 mobile.** Slide 1 is in initial HTML, `loading="eager"`, `fetchpriority="high"`, `decoding="sync"`, AVIF/WebP via `format={['avif','webp']}`, correctly sized by `sizes` (no oversized download), box pre-reserved (no late layout shift pushing it). Old component's 800×600 fixed raster replaced by proper ladders.
- [ ] **CLS ≤ 0.02 page-level.** Every stage reserves space via CSS `aspect-ratio` (1/1→4/3 product, 16/10→21/9 story) before images decode; `<img>` fills the box absolutely. Caption blocks are grid-stacked (tallest wins → no swap shift). Thumb strip has fixed 72 px cells. Dialog animates in top layer (no layout impact).
- [ ] **INP < 200 ms p75.** All click/key handlers O(1); scroll work deferred off the hot path to `scrollend`/debounced handler (`passive: true`); no timers while idle except gated story autoplay tick (6 s, cleared when hidden/stopped); no forced synchronous layout (single `offsetWidth` read cached per call, writes batched in `render()`).
- [ ] **Component JS ≤ 3 KB gzipped.** Accounting: `gallery.ts` ≈ 190 LOC unminified ≈ 5.5 KB raw → ≈ 2.6 KB min+gzip (est., verified at build); one module total for N instances (Astro dedupe); zero runtime deps; icons are inline SVG (no icon library on website).
- [ ] **Request discipline.** Initial payload = slide 1 rendition + tiny 96/160 px thumbs (product ≥ md) only; slides 2+ fetch on approach via native lazy loading; neighbor preload fires only on `pointerenter` with fine pointers; lightbox hi-res (1920/2400 px rendition) loads once, on first open, per slide.
- [ ] **Build-time cost.** Fixed width ladders (4–5 sizes) × AVIF+WebP × 10 source images ≈ 90 renditions through sharp's cache — bounded, no combinatorial explosion.
- [ ] Verify with Lighthouse mobile on both pages against targets; WebPageTest run on emulated Moto G-class device optional.

---

## Verification

Automated (must pass before PR):

```bash
cd src/website && pnpm run check && pnpm run build
```

`astro check` validates the typed Props at both call sites; build exercises the sharp rendition pipeline. (No Playwright test files exist in-repo; adding them is out of scope per AGENTS.md testing guidance.)

Manual matrix (desktop Chrome, Firefox, Safari + iOS Safari on physical device, Android Chrome):

1. **Swipe/swipe-stop:** iOS Safari — flick swipes advance exactly one slide (`scroll-snap-stop: always`); momentum rubber-bands at both ends and settles on slide 1/N (bounded); counter, dots/thumbs, caption and hash update after each settle.
2. **Pinch-zoom lightbox:** iOS Safari — open „Bild vergrößern", pinch outward zooms inside the zoomport only (page behind doesn't zoom); double-tap/dblclick toggles 1×/2×; check `100dvh` surface and no rubber-band escape of the dialog body; rotate device.
3. **Deep-link share:** copy `https://…/produkte#produkte-4` into a fresh tab → fourth product shown instantly, counter „4 von 6", caption matches; swipe twice → URL reads `#produkte-6` with **no** history growth (back returns to the previous *page*, not slide 5); paste a changed hash while on-page → gallery follows; `#produkte-99` → ignored gracefully.
4. **Keyboard-only:** tab to track → arrow through all slides → wraps via buttons path → `Enter` opens lightbox → `Tab` cycles dialog controls only → `Escape` closes → focus visibly returned; `Home`/`End` jump correctly; dots reachable and operable.
5. **Reduced motion:** OS-level reduce enabled → no autoplay on `/alpaka-wanderungen`, jumps are instant, no fades; Diashow button absent (autoplay disabled entirely).
6. **Autoplay lifecycle:** story gallery starts when scrolled ≥ 50 % into view, pauses on hover/Tab-focus/tap, stops permanently after first manual swipe/button press (`gallery_autoplay_stop` CustomEvent visible in devtools), resumes never; background tab pauses via `visibilitychange`.
7. **No-JS:** block JS in devtools → swipe/scroll works, slide-1 caption visible, zero gallery chrome rendered, no console errors, no timer activity; `Layout` nav unaffected by the added bootstrap line.
8. **Screen readers:** VoiceOver (iOS) announces „Folie, 2 von 6, Knäuel Zauberwolle…" on swipe and „Bild 2 von 6: …" via live region; dialog opens modally with label „Bildansicht: …"; NVDA desktop keyboard walkthrough clean.
9. **Metrics:** Lighthouse mobile (throttled) on both pages — LCP/CLS/INP within §Performance checklist targets; transferred JS delta ≤ ~3 KB gzip vs `main`.
10. **Visual regression eyeball:** product badges/chips legible (dark-on-gold), thumb strip alignment at 768/1024/1440, story scrim readability over brightest photo (wanderung3 snow/sky region).

PR: squash-merge title per Karma schema — `feat(website): rebuild slideshow as smart gallery experience with lightbox and deep links`.

---

## Risks

| Risk | Mitigation |
| --- | --- |
| Mixed source ratios (portrait Strickgabel/Wolle Amadeus vs landscape rest) break uniform stages | Locked by design: `contain` + `--schurwolle` backdrop (product), `cover` (story); audited dims recorded in Context |
| Portrait hike photos crop heavily at 21/9 desktop | `object-position: center 40%` default; `max-height: 80vh` cap; visual QA step 10 |
| iOS < 26.2 lacks `scrollend` (~12 % of traffic, the core audience) | Feature-detected 120 ms debounced `scroll` fallback; identical index math |
| Deep links fight browser scroll restoration | Instant-scroll bootstrap, `replaceState` only, documented back/forward caveat (§State & deep-linking) |
| Pinch-zoom-in-scroller quirks on older iOS (gesture handling varies pre-iOS 17) | Zoomport isolates gestures; dblclick/double-tap 1×/2× class toggle as universal fallback; dedicated device QA |
| Autoplay annoyance / a11y complaints | Strict pause matrix, permanent stop on interaction, persistent pause button, reduced-motion kill switch |
| Caption copy/badges need owner sign-off | Content is plain props in the page files; flagged in PR description; removal is a one-line edit per badge |
| Sharp build-time growth from ladders × formats | Fixed 4–5-step ladders, reused metadata, Astro asset cache; monitored in CI build time |
| Branch divergence: `feature/slideshow` prototype and concept branches 1/2 linger | After landing, close/archive them; salvage only the `@supports` scroll-marker experiment idea noted for later |
| `history.replaceState` spam during fast fling | Writes happen in `render()` on committed index changes only (post-`scrollend`), never per frame |

---

## Effort breakdown & file-change list

| Phase (concept §10) | Work | Estimate |
| --- | --- | --- |
| 1 — Mobile-solid baseline | Dispatcher + both variants, snap track, aspect reservation, eager/lazy split, captions/badges/dots/thumbs, buttons, keyboard, reduced motion, autoplay rules, `js` gate, German alt overhaul both pages | 1–1.5 d |
| 2 — Inspection & sharing | `<dialog>` lightbox + pinch-zoom port, hi-res-on-open, focus semantics, deep links + replaceState + bootstrap read, hover-intent preload, CustomEvent hooks | 1 d |
| 3 — Polish & QA | `@starting-style` dialog fade, View-Transition morph, LQIP evaluation (stretch), full manual matrix, Lighthouse/CWV verification | 0.5–1 d |
| **Total** | | **2.5–3.5 focused days** |

| File | Change | Est. LOC |
| --- | --- | --- |
| `src/website/src/components/Slideshow.astro` | Rewrite → typed dispatcher + API types | ~45 (net, replaces 73) |
| `src/website/src/components/gallery/GalleryProduct.astro` | New: stage/meta/indicators/lightbox markup + scoped styles | ~230 |
| `src/website/src/components/gallery/GalleryStory.astro` | New: stage/overlay/dots/play-pause markup + scoped styles | ~140 |
| `src/website/src/components/gallery/gallery.ts` | New: shared initGallery module (state, nav, lightbox, autoplay, hash, events) | ~190 |
| `src/website/src/layouts/Layout.astro` | +1 line `js` bootstrap in `<head>` | +1 |
| `src/website/src/pages/produkte.astro` | Slide-data array (captions/alts/badges) + invocation swap | ±35 |
| `src/website/src/pages/alpaka-wanderungen.astro` | Slide-data array + invocation swap | ±25 |
| **Net** | | **≈ +650 / −80** |

### Milestones (tracked)

- [ ] Write the plan (`docs/plans/013-slideshow-smart-gallery-experience.md`)
- [ ] Phase 1: dispatcher + variants + script + `js` gate + page integrations
- [ ] Phase 2: lightbox + deep links + preload + event hooks
- [ ] Phase 3: polish gates (`@starting-style`, VT morph) + LQIP decision
- [ ] Owner review of German captions/badges
- [ ] Full verification matrix + `pnpm run check && pnpm run build` green
