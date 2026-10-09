# Slideshow „Immersive Storytelling" Implementation Plan

Implements the immersive storytelling concept as a concrete build spec
for `src/website/src/components/Slideshow.astro` and its two consumers. Branch:
`feat/slideshow-concept-2` (based on `main`, i.e. the original setInterval-crossfade slideshow).
The competing branch `feat/slideshow-concept-1` is explicitly out of scope.

## 1. Context

- **Current component** (`Slideshow.astro`, 73 LOC): a fixed-height (`24rem`/`32rem`) `<ul>` of absolutely
  stacked slides, all images hard-coded to `width={800} height={600}` (wrong for the portrait sources),
  generic alt text, and a blind `setInterval(…, 4000)` crossfade that runs forever, ignores
  `prefers-reduced-motion`, tab visibility and viewport position.
- **Consumers** (the only two, verified by grep):
  - `src/website/src/pages/produkte.astro:35` — 6 Hofladen photos (`Strickgabel`, `Zauberwolle`,
    `Wolle_Amadeus`, `Karten`, `Polster`, `Wollpellets`), section on cream stage after an `auwasser`
    intro block.
  - `src/website/src/pages/alpaka-wanderungen.astro:51` — 4 hike photos, sitting between the
    „Details" section and „Ausflugstipps".
- **Design language** to match: `ServiceHero.astro` (full-bleed photo + dark scrim + centered cream type)
  and `ImpressionBreak.astro` (full-width photo with radial vignette). Tokens from `global.css`:
  `--bluetenhonig #e1b14a` (only accent), `--schurwolle #fbf7ed` (stage/caption text),
  `--taubenblau #4b5b73` (secondary glyphs), `--schwarz #1f1f1d` (scrim/grain base),
  `--himmelblau #8da5d3` / `--auwasser #9abfba` (winter tint / page band context).
- **Real image dimensions** (measured with `identify`, needed for CLS reservation):

  | Page | File | Intrinsic W×H | Orientation |
  |---|---|---|---|
  | produkte | Strickgabel.jpeg | 1709×2392 | portrait (~5:7) |
  | produkte | Zauberwolle.jpg | 4032×3024 | landscape 4:3 |
  | produkte | Wolle_Amadeus.jpg | 3024×4032 | portrait 3:4 |
  | produkte | Karten.jpg | 4032×3024 | landscape 4:3 |
  | produkte | Polster.jpg | 4032×3024 | landscape 4:3 |
  | produkte | Wollpellets.jpg | 4032×3024 | landscape 4:3 |
  | wanderungen | wanderung1.jpg | 1536×2048 | portrait 3:4 |
  | wanderungen | wanderung2.jpg | 1536×2048 | portrait 3:4 |
  | wanderungen | wanderung3.jpg | 2048×1536 | landscape 4:3 |
  | wanderungen | wanderung4.jpg | 900×1600 | tall portrait 9:16 |

- **Verified environment facts:** `package.json` has `"check": "astro check"`; `Layout.astro:22` sets
  `<meta name="viewport" content="width=device-width" />` (pinch-zoom NOT disabled — required by the
  lightbox); Astro processes co-located `<script>` tags into one bundled module per component that
  executes **once per page**, so the script must iterate `querySelectorAll` instances.
- **Zero-JS baseline:** the foundation is a plain horizontally scrollable, snap-aligned list. With JS
  disabled the first slide renders fully visible with its caption; every other slide is reachable by
  native touch swipe / trackpad scroll / keyboard focus + arrow keys on the focused scroller. Arrows,
  dots, lightbox, autoplay, reveal animation and scrollbar-hiding are strictly additive (gated behind a
  runtime `data-js="true"` flag) and absent without JS.

## 2. Component API (final, locked)

```ts
// Slideshow.astro frontmatter
import { Image } from "astro:assets";
import type { ImageMetadata } from "astro";

export interface StorySlide {
  src: ImageMetadata;   // imported via astro:assets
  alt: string;          // factual German description of the photograph
  kicker?: string;      // e.g. "01 · Hofladen" or "Moment 02"
  title?: string;       // product / moment name (visible caption layer)
  text?: string;        // exactly one warm "du"-form sentence
  focal?: string;       // object-position override, default CSS "center 45%"
}

export interface Props {
  slides: StorySlide[];
  label: string;                    // German aria-label of the carousel region
  eyebrow?: string;                 // editorial header line (taubenblau caps
                                    //   flanked by gold rules)
}

const { slides, label, eyebrow } = Astro.props;

const intervalMs = 7000;            // one interval for every slideshow
```

Rules:
- No `any`; both pages must compile under `astro check`.
- All caption fields except `alt` are optional; a slide with only `src`+`alt` renders image-only
  (no scrim block, no empty caption box).
- One frame ratio (4/5 mobile, 3/2 from 768 px) and one 7 s autoplay interval for every slideshow
  — there is no `variant` prop. Portrait photos are handled with the per-slide `focal` value.
- Kicker numbering is derived from the slide index in the pages (`${String(i + 1).padStart(2, "0")}
  · ${topic}` / `Moment ${String(i + 1).padStart(2, "0")}`), not hard-coded per slide (§8).
- Colours come from the `global.css` palette tokens via `color-mix`, not raw rgba literals (§4).
- The component reads `slide.src.width` / `slide.src.height` itself and forwards them as explicit
  `width`/`height` props on `<Image>` (single source of truth — pages never duplicate dimensions).

## 3. Markup spec

Full DOM tree of `Slideshow.astro`. Every class, role and ARIA attribute listed here is normative.
State toggling uses **data attributes** (not injected classes) so Astro scoped styles keep working
without `:global()` leaks (concept §7.2).

```html
<section
  class="story"
  data-js="false"                     <!-- script flips to "true"; gates all chrome -->
  data-interval={intervalMs}
  style={`--interval-ms:${intervalMs}ms`}
  role="region"
  aria-roledescription="Karussell"
  aria-label={label}
>
  <!-- Editorial header, rendered only if eyebrow set (no subline exists) -->
  {eyebrow && (
    <header class="story-header">
      <p class="story-eyebrow">{eyebrow}</p>
    </header>
  )}

  <div class="deck-wrap">
    <button class="deck-nav deck-prev" type="button" aria-label="Vorheriges Bild">
      <SlideshowChevron direction="prev" />  <!-- shared component, no inline dup -->
    </button>

    <ul class="deck" tabindex="0"
        aria-label="Bildstreifen – wischen oder mit den Pfeiltasten bewegen">
      {slides.map((slide, i) => (
        <li class="slide" data-state={i === 0 ? "active" : "idle"}
            role="group" aria-roledescription="Folie"
            aria-label={`Bild ${i + 1} von ${slides.length}`}>
          <figure class="slide-card">
            <!-- The whole image IS the lightbox open control: disabled in markup
                 so no-JS users never get a dead button; the script unlocks it. -->
            <button class="slide-media" type="button" disabled aria-haspopup="dialog">
              <Image
                src={slide.src}
                alt={slide.alt}
                width={slide.src.width}
                height={slide.src.height}
                sizes="(min-width: 768px) min(72%, 47.5rem), calc(100vw - 4rem)"
                widths={[640, 960, 1280]}
                loading={i === 0 ? "eager" : "lazy"}
                fetchpriority={i === 0 ? "high" : undefined}
                decoding="async"
                style={slide.focal ? `object-position:${slide.focal}` : undefined}
              />
              <span class="media-zoom" aria-hidden="true">
                <!-- inline magnifier SVG, pointer-events: none display overlay -->
              </span>
              <span class="sr-only">{" – Bild groß anzeigen"}</span>
            </button>
            {(slide.kicker || slide.title || slide.text) && (
              <figcaption class="slide-caption">
                {slide.kicker && <p class="caption-kicker">{slide.kicker}</p>}
                {slide.title && <p class="caption-title" >{slide.title}</p>}
                {slide.text  && <p class="caption-text" >{slide.text}</p>}
              </figcaption>
            )}
          </figure>
        </li>
      ))}
    </ul>

    <button class="deck-nav deck-next" type="button" aria-label="Nächstes Bild">
      <SlideshowChevron direction="next" />
    </button>
  </div>

  <div class="story-dots" role="group" aria-label="Direktnavigation">
    {slides.map((_, i) => (
      <button class="story-dot" type="button"
              aria-label={`Zu Bild ${i + 1} springen`}
              aria-current={i === 0 ? "true" : undefined}>
        <span class="dot-fill"></span>
      </button>
    ))}
  </div>

  <!-- SR-only live region; populated ONLY by button-driven navigation after autoplay stopped -->
  <p class="sr-only" role="status" data-slide-status></p>
</section>

<!-- Always rendered; there is no lightbox prop to switch it off -->
<dialog class="lightbox" aria-label="Bildansicht" data-lightbox>
  <figure class="lb-figure">
    <div class="lb-media"><img class="lb-img" src="" alt="" /></div>
    <figcaption class="lb-caption">
      <p class="caption-kicker" data-lb-kicker></p>
      <p class="caption-title"  data-lb-title></p>
      <p class="caption-text"   data-lb-text></p>
    </figcaption>
  </figure>
  <p class="sr-only" role="status" data-lb-status></p>
  <p class="lb-count" aria-hidden="true" data-lb-count></p>
  <button class="lb-nav lb-prev" type="button" aria-label="Vorheriges Bild">
    <SlideshowChevron direction="prev" />
  </button>
  <button class="lb-nav lb-next" type="button" aria-label="Nächstes Bild">
    <SlideshowChevron direction="next" />
  </button>
  <button class="lb-close" type="button" aria-label="Schließen">
    <!-- inline × SVG -->
  </button>
</dialog>
```

Markup contracts:
- **No hardcoded ids** (the old `id="slideshow"` disappears; two pages must never collide).
- `.deck-nav` buttons and `.story-dots` are rendered into the DOM but hidden until
  `[data-js="true"]` (see §4.9) — no-JS users never see dead controls.
- The lightbox open control is the whole `.slide-media` image button, disabled in markup and
  unlocked by the script; it is named by the image alt plus the sr-only `– Bild groß anzeigen`
  suffix. There is no separate `.slide-expand` button. The caption is `pointer-events: none` so
  clicks over the scrim fall through to the image button underneath.
- `<dialog>` is always rendered. If `window.HTMLDialogElement` is missing (iOS < 15.4), the script
  repurposes it as a fixed overlay `<div role="dialog" aria-modal="true">` with manual
  Esc/focus-trap handling (§6).
- Lightbox `<img>` starts empty (`src="" alt=""`); at open/step time the script fills `srcset`,
  `sizes="100vw"` and `alt` — `src` is resolved by mirroring the browser's w-descriptor selection
  over the slide's own `srcset` candidates (as if `sizes="100vw"`), so neighbour preloading
  fetches exactly what the lightbox will load.
- Chevrons come from a shared `SlideshowChevron.astro` component, not duplicated inline SVG.
- German labels locked: `Vorheriges Bild`, `Nächstes Bild`, `Direktnavigation`,
  `– Bild groß anzeigen` (sr-only suffix), `Bild X von Y`, `Folie`, `Karussell`, `Schließen`,
  `Bildansicht`.

## 4. CSS spec (with snippets)

All styles in the component's scoped `<style>` block. Custom properties cascade from tokens in
`global.css`; no global file changes.

### 4.1 Deck geometry & peek math

The scroller breaks out of the container's side padding on mobile (full-bleed track) and is capped
at the 1200 px container width on desktop. Centering formula: side padding
`--pad-x = (track width − slide width) / 2` makes the **first and last slide centerable**
(`scroll-snap-align: center` can then reach scroll offset 0 / max), and every other slide centers
automatically. Visible neighbour sliver `= --pad-x − --gap`.

```css
.deck-wrap {
  /* full-bleed on mobile */
  margin-inline: calc(-1 * var(--container-pad, 1rem));
}
.deck {
  --gap: 0.75rem;
  --slide-w: calc(100vw - 4rem);
  --pad-x: calc((100% - var(--slide-w)) / 2);  /* % padding → track width */

  display: flex;
  gap: var(--gap);
  overflow-x: auto;
  overscroll-behavior-x: contain;        /* keeps vertical page scroll sacred */
  scroll-snap-type: x mandatory;
  scroll-padding-inline: var(--pad-x);   /* snapport inset matches padding */
  padding-inline: var(--pad-x);
  scrollbar-width: none;
}
.story[data-js="true"] .deck::-webkit-scrollbar { display: none; }

.slide {
  flex: 0 0 var(--slide-w);
  scroll-snap-align: center;
}

@media (min-width: 768px) {
  .deck-wrap { margin-inline: 0; max-width: calc(75rem - 2rem); margin-inline: auto; }
  .deck {
    --gap: 1.5rem;
    --slide-w: min(72%, 47.5rem);        /* ≈ concept's 760 px cap */
  }
}
```

Resulting geometry (locked numbers): mobile card ≈ `100vw − 4rem` (~81 vw @ 390 px) with a
~1.25 rem neighbour sliver; desktop card 760 px inside a 1168 px track → ~14 % peek per side.
(The concept's literal „86 vw card + 7 vw peek × 2 + gap" exceeds 100 vw; these numbers preserve
both cues — see Deviations.)

### 4.2 Slide frame, CLS reservation, crop

```css
.slide-card {
  position: relative;
  overflow: hidden;
  border-radius: 1rem;
  background-color: var(--schurwolle);           /* pre-paint placeholder tone */
  box-shadow: 0 12px 32px color-mix(in srgb, var(--schwarz) 14%, transparent);
  /* THE cls guarantee: frame ratio fixed independent of intrinsic ratio */
  aspect-ratio: 4 / 5;
}
@media (min-width: 768px) {
  .slide-card {
    aspect-ratio: 3 / 2;
    border-radius: 1.25rem;
    box-shadow: 0 24px 64px color-mix(in srgb, var(--schwarz) 18%, transparent);
  }
}
.slide-media img {
  width: 100%; height: 100%;
  object-fit: cover;
  object-position: center 45%;                   /* focal default; inline style overrides */
}
```

Mixed source orientations (portrait Strickgabel/Wolle_Amadeus/wanderung4 vs landscape rest) are
cropped by the fixed frame; there is no per-page variant — every slideshow shares the one ratio
(4/5 mobile, 3/2 from 768 px), and `focal` protects subjects. Every crop needs visual sign-off
before ship (§11).

### 4.3 Caption scrim (AA-safe by construction)

Deviates from the concept's gradient stops (0.88 → **0.95** bottom) so the gold kicker passes AA as
real text — see Deviations and the contrast table below.

```css
.slide-caption {
  position: absolute; inset-inline: 0; bottom: 0;
  z-index: 2;
  /* The caption covers the lower part of the photo; clicks there must fall
     through to the image button underneath. */
  pointer-events: none;
  padding: 3.5rem 1.25rem max(1.25rem, env(safe-area-inset-bottom));
  color: var(--schurwolle);
  background: linear-gradient(
    to top,
    color-mix(in srgb, var(--schwarz) 95%, transparent) 0%,   /* text zone — worst case passes 4.5:1 */
    color-mix(in srgb, var(--schwarz) 62%, transparent) 42%,  /* no text above this line */
    transparent 80%
  );
}
.caption-kicker {
  font-size: 0.75rem; letter-spacing: 0.14em; text-transform: uppercase;
  color: var(--bluetenhonig);
}
.caption-title {
  font-size: clamp(1.25rem, 2.6vw, 1.75rem); font-weight: 400;
}
.caption-text {
  font-weight: 300; font-size: clamp(0.95rem, 1vw, 1.05rem); max-width: 42ch;
  text-shadow: 0 1px 8px rgba(31, 31, 29, 0.35);
}
.caption-kicker, .caption-title { margin: 0 0 0.25rem; }
.caption-text { margin: 0; }
```

**Contrast table (verified pairs; L values computed, worst case = pure-white photo pixel):**

| Foreground | Background | Ratio | Requirement | Verdict |
|---|---|---|---|---|
| `--schurwolle` L=0.931 caption title/text | scrim α 0.95 over white → L=0.063 | **8.7 : 1** | 4.5:1 text | ✅ AAA |
| `--bluetenhonig` L=0.481 kicker | scrim α 0.95 over white → L=0.063 | **4.7 : 1** | 4.5:1 small text | ✅ AA |
| `--taubenblau` L=0.102 chevron/icon | solid `--schurwolle` disc | **6.5 : 1** | 3:1 non-text UI | ✅ |
| active dot: `--bluetenhonig` fill + 2 px `--taubenblau` ring | cream stage | ring boundary 6.5 : 1 | 3:1 non-text | ✅ (ring carries the boundary; bare gold-on-cream is only 1.85 : 1 and is therefore never used unringed) |
| inactive dot: transparent fill + 2 px `--taubenblau` border | cream stage | 6.5 : 1 | 3:1 non-text | ✅ (concept's `rgba(taubenblau,.55)` fill computes to only 1.87 : 1 — rejected, rings instead) |
| focus indicator: 3 px `--bluetenhonig` outline + outer `rgba(75,91,115,.85)` halo | cream or scrim | ≥ 4.7 : 1 via outer layer | 3:1 focus | ✅ compound indicator |

### 4.4 Warm tint + film grain overlays (tier 1 delighters)

Stacking order inside `.slide-card`: `img` (z 0) → tint/grain pseudo-element (z 1) →
`.slide-caption` (z 2) → `.media-zoom` badge (z 3).

```css
.slide-media::before {   /* vignette like ImpressionBreak::before + gold soft-light wash */
  content: ""; position: absolute; inset: 0; z-index: 1; pointer-events: none;
  background:
    radial-gradient(120% 90% at 50% 40%, color-mix(in srgb, var(--bluetenhonig) 6%, transparent), transparent 62%),
    radial-gradient(140% 115% at 50% 50%, transparent 58%, color-mix(in srgb, var(--schwarz) 18%, transparent) 100%);
}
.slide-media::after {    /* static film grain tile, ~5 % */
  content: ""; position: absolute; inset: 0; z-index: 1; pointer-events: none;
  opacity: 0.05;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='160' height='160'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E");
}
@media (prefers-reduced-transparency: reduce) {
  .slide-media::after { display: none; }
}
```

(There is no `data-season` hook; the wash is always the warm gold one.)

### 4.5 Ken Burns — active slide only

Duration derives from the interval (1.5×, 10.5 s with the single 7 s interval) so a breath never
visibly completes/loops.

```css
@keyframes kenburns     { from { transform: scale(1.02); } to { transform: scale(1.09) translate(-1.2%, 1%); } }
@keyframes kenburns-alt { from { transform: scale(1.09) translate(1.2%, -1%); } to { transform: scale(1.02); } }

@media (prefers-reduced-motion: no-preference) {
  .slide[data-state="active"] .slide-media img {
    animation: kenburns calc(var(--interval-ms) * 1.5) ease-in-out both;
  }
  .slide:nth-child(even)[data-state="active"] .slide-media img { animation-name: kenburns-alt; }
  .story[data-autoplay="paused"]  .slide-media img,
  .story[data-autoplay="stopped"] .slide-media img,
  .story[data-dragging] .slide-media img { animation-play-state: paused; }
}
```

Transform-only ⇒ compositor thread; the card's `overflow: hidden` + radius clips the zoom.

### 4.6 Staggered caption entrance

Bound to `[data-state="active"]` toggling; re-triggers on every activation because the property set
restarts when the selector stops matching between slides.

```css
@media (prefers-reduced-motion: no-preference) {
  .slide[data-state="active"] .caption-kicker,
  .slide[data-state="active"] .caption-title,
  .slide[data-state="active"] .caption-text {
    animation: caption-in 320ms ease-out both;
  }
  .slide[data-state="active"] .caption-title { animation-delay: 60ms; }
  .slide[data-state="active"] .caption-text  { animation-delay: 140ms; }
}
@keyframes caption-in {
  from { opacity: 0; transform: translateY(12px); }
  to   { opacity: 1; transform: translateY(0); }
}
```

Optional sugar (skip if it complicates): `@supports` + `sibling-index()` may replace the two
hand-written delays; the nth-child-free map above is fine for ≤ 8 slides and stays.

### 4.7 Viewport entry reveal (once)

The reveal is pure CSS, no JS and no observer: where scroll-driven animations are supported
(`animation-timeline: view()` and no-preference motion) the story fades/rises in while it enters
the viewport; browsers without scroll-driven animations show the section plain — **no entrance
animation at all** (no IntersectionObserver, no `.in-view` class, no `data-css-reveal` attribute;
the earlier IO-based reveal with a CSS upgrade was cut during review — see Deviations).

```css
@supports (animation-timeline: view()) {
  @media (prefers-reduced-motion: no-preference) {
    .story {
      animation: story-reveal both ease-out;
      animation-timeline: view();
      animation-range: entry 10% cover 30%;
    }
  }
}
@keyframes story-reveal {
  from { opacity: 0.001; translate: 0 24px; }
  to   { opacity: 1; translate: 0 0; }
}
```

The reduced-motion kill-switch (§4.10) additionally pins `.story { opacity: 1; translate: none; }`.
No tier-3 parallax was shipped.

### 4.8 Autoplay progress affordance (dots)

Active dot slowly sweeps gold over `--interval-ms` while autoplay is running; static gold disc
otherwise (graceful degradation without registered properties).

```css
@property --dot-progress { syntax: "<number>"; inherits: false; initial-value: 0; }
.story-dots { display: flex; justify-content: center; gap: 0.375rem; margin-top: 1.25rem; }
.story-dot {
  width: 24px; height: 24px;            /* ≥ 24 px hit area */
  display: grid; place-items: center;
  background: none; border: 0; padding: 0; cursor: pointer;
}
.dot-fill {                             /* child span, not ::before */
  width: 8px; height: 8px; border-radius: 50%;
  border: 2px solid var(--taubenblau);   /* inactive: ring, 6.5:1 on cream */
}
.story-dot[aria-current="true"] .dot-fill {
  background: conic-gradient(var(--bluetenhonig) calc(var(--dot-progress) * 360deg), transparent 0);
  border-color: var(--taubenblau);       /* ring guarantees the 3:1 boundary */
}
@media (prefers-reduced-motion: no-preference) {
  .story[data-autoplay="running"] .story-dot[aria-current="true"] .dot-fill {
    animation: dot-fill var(--interval-ms) linear forwards;
  }
}
@keyframes dot-fill { from { --dot-progress: 0; } to { --dot-progress: 1; } }
```

### 4.9 Controls, open control, lightbox

The chevron navigation buttons are shared between deck and lightbox (`.deck-nav`/`.lb-nav` get one
rule block, hover and shadow come from palette tokens via `color-mix`):

```css
.deck-nav, .lb-nav {
  position: absolute; top: 50%; translate: 0 -50%; z-index: 4;
  width: 48px; height: 48px; border-radius: 50%;
  display: grid; place-items: center;
  background: var(--schurwolle); color: var(--taubenblau);
  border: 1px solid var(--taubenblau);
  box-shadow: 0 12px 32px color-mix(in srgb, var(--schwarz) 18%, transparent);
  cursor: pointer;
}
.deck-prev { left: max(1rem, calc((100% - var(--slide-w)) / 2 - 3.5rem)); }
.deck-next { right: max(1rem, calc((100% - var(--slide-w)) / 2 - 3.5rem)); }
.deck-nav:hover, .lb-nav:hover { color: var(--bluetenhonig); }
@media (max-width: 767.98px) { .deck-nav { display: none; } }  /* swipe + dots on phones */

.story:not([data-js="true"]) :is(.deck-nav, .story-dots) { display: none; }  /* additive chrome */
.story:not([data-js="true"]) .media-zoom { display: none; }

/* The whole slide image is the (progressively enabled) open control. */
.slide-media {
  position: absolute; inset: 0; display: block;
  padding: 0; border: 0; background: none; font: inherit;
}
.story[data-js="true"] .slide-media { cursor: zoom-in; }
.slide-media:disabled { cursor: default; }   /* without JS it reads/looks like a plain image */

/* display-only zoom badge inside the button; the button keeps its alt + sr-only name */
.media-zoom, .lb-close {
  width: 44px; height: 44px; border-radius: 50%;
  display: grid; place-items: center;
  background: color-mix(in srgb, var(--schurwolle) 92%, transparent);
  color: var(--taubenblau); border: 1px solid var(--taubenblau);
}
@media (hover: hover) and (pointer: fine) {
  .slide-media:hover .media-zoom, .slide-media:focus-visible .media-zoom { opacity: 1; }
}

/* fullscreen, fully opaque; there is no ::backdrop — the dialog itself is the surface
   the visitor taps to close */
.lightbox {
  border: 0; padding: 0;
  width: 100vw; height: 100vh; height: 100dvh;
  max-width: none; max-height: none;
  background: var(--schwarz); color: var(--schurwolle);
  overflow: hidden;
}
.lightbox:not([open]) { display: none; }
.lightbox.is-open {                          /* iOS < 15.4 div-overlay fallback */
  display: block; position: fixed; inset: 0; z-index: 999; overflow: hidden;
}
.lb-figure { margin: 0; height: 100%; display: flex; flex-direction: column; touch-action: pinch-zoom; }
.lb-media { flex: 1; min-height: 0; display: flex; align-items: center; justify-content: center;
            padding: max(3.5rem, env(safe-area-inset-top) + 1rem) 1rem 0.5rem; }
.lb-media img { width: auto; height: auto; max-width: 100%; max-height: 100%; object-fit: contain; }
.lb-caption { padding: 0.75rem 1.25rem max(1rem, env(safe-area-inset-bottom)); }
.lb-caption.is-empty { display: none; }      /* slides without caption hide the box entirely */
.lb-count { position: absolute; top: max(1rem, env(safe-area-inset-top)); left: 1rem;
            font-size: 0.8rem; letter-spacing: 0.14em; font-variant-numeric: tabular-nums; }
.lb-prev { left: max(1rem, env(safe-area-inset-left)); }
.lb-next { right: max(1rem, env(safe-area-inset-right)); }
@media (hover: none), (pointer: coarse) { .lb-nav { display: none; } }  /* swipe on touch */
.lb-close { position: absolute; top: max(0.75rem, env(safe-area-inset-top)); right: 0.75rem; }

@supports (transition-behavior: allow-discrete) {
  .lightbox[open] { opacity: 1; transition: opacity 200ms ease-out, overlay 200ms ease-out allow-discrete; }
  @starting-style { .lightbox[open] { opacity: 0; } }
  @media (prefers-reduced-motion: reduce) { .lightbox[open] { transition: none; } }
}
```

Lightbox close paths: `.lb-close` click, tap on the empty area around the image (anything that is
not the photo, the caption or a control button; drags/pinches must not close — the script
discriminates gesture vs tap), native Esc (dialog), Esc (fallback trap), system back-swipe.

Focus-visible ring (all interactive elements):

```css
:where(.deck-nav, .story-dot, .slide-media, .lb-nav, .lb-close):focus-visible {
  outline: 3px solid var(--bluetenhonig);
  outline-offset: 2px;
  box-shadow: 0 0 0 6px color-mix(in srgb, var(--taubenblau) 85%, transparent);  /* compound indicator */
}
.slide-media:focus-visible { outline-offset: -3px; }   /* outline inside the image */
.deck:focus-visible { outline: 3px solid var(--bluetenhonig); outline-offset: 2px; }
```

### 4.10 Reduced-motion kill-switch (last word, overrides everything above)

```css
@media (prefers-reduced-motion: reduce) {
  .story *, .story *::before, .story *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
  .story { opacity: 1; translate: none; scroll-behavior: auto !important; }
}
```

JS additionally never arms autoplay when `matchMedia('(prefers-reduced-motion: reduce)').matches`
and uses `behavior: 'auto'` scrolls under reduce (CSS alone cannot stop scripted smooth-scroll).

## 5. Script spec (inline `<script>`, ~670 lines source)

Astro bundles the co-located script once per page; the module iterates instances:

```js
for (const story of document.querySelectorAll(".story")) initStory(story);

function wrap(index, length) { return (index + length) % length; }   // shared wrap-around

initStory(story)
  // 1. finds .deck and [data-slide-status]; early-return if either is missing
  // 2. initDeck(story, deck, statusEl) → DeckHandle — all deck behaviour is live,
  //    so story.dataset.js = "true" right after it returns, independent of the lightbox
  // 3. only then findLightbox(story); if it succeeds: unlock the (disabled) .slide-media
  //    open buttons and initLightbox(...)

initDeck(story, deck, statusEl): DeckHandle          // deck only (no lightbox types)
  // refs: slides[], dots[], prev/next buttons, interval from story[data-interval]
  // active-slide tracking: rAF-throttled scroll handler over slide midpoints,
  //   programmatic-scroll bookkeeping (goTo suppresses re-sync until scrollend/timeout)
  // autoplay engine: autoState "idle"|"running"|"paused"|"stopped" on story[data-autoplay];
  //   eligible() = visible ≥ 40% && !document.hidden && !REDUCED.matches && !hovered && !focused
  //   pause on pointerdown, refresh on pointerup, mouse enter/leave (fine pointers only),
  //   focusin/focusout, document visibilitychange, IntersectionObserver threshold 0.4
  //   STOP (terminal): first intentional interaction — swipe past ~30 % of a card,
  //   nav/dot click, ArrowLeft/Right on the deck
  // controls: prev/next/dot click → step/goTo + announce() ONLY when state === "stopped"
  //   (writes "Bild X von Y" into the role=status region)
  // drag pause: touchstart/touchend/touchcancel set story[data-dragging] (KB freezes),
  //   swipe-stop check on touchend
  // returns { slides, stopAutoplay, showSlide(index) }  ← what the lightbox needs

findLightbox(story): LightboxElements | null         // every dialog part at once; a single
                                                     // missing part means no lightbox at all

readLightboxSlides(slides): LightboxSlide[]          // alt + trimmed kicker/title/text read
                                                     // once at init, never re-queried per step

initLightbox(lb, deckHandle, openButtons, slideData)
  // open(i): stopAutoplay, fill lb image (srcset/sizes="100vw"/alt; src resolved by mirroring
  //   the browser's w-descriptor selection), copy caption texts, counter + status, preload
  //   both neighbours, lock scroll (overflow hidden + scrollbar-gap compensation),
  //   showModal wrapped in startViewTransition when the API exists and motion is allowed
  //   (view-transition name moved between slide card and lb figure; names cleared in
  //   finished.finally via clearTransitionNames)
  // no <dialog> support: fixed overlay fallback with role/aria-modal, .is-open, manual
  //   Esc + focus trap
  // navigation: lbPrev/lbNext click, dialog ArrowLeft/ArrowRight, swipe (touch Δ > 48 px,
  //   suppressed for multi-touch/pinch), counter "X / Y"
  // close: .lb-close, tap on the empty area around the image (click outside button/img/
  //   caption, ignoring post-gesture ghost clicks), native Esc, fallback trap; on close the
  //   deck showSlide(lbIndex) (no animation) + openButtons[lbIndex].focus(), unlock scroll

// The module is organized as initStory / initDeck / findLightbox / readLightboxSlides /
// initLightbox, typed by the LightboxSlide/LightboxElements/DeckHandle interfaces so the
// bundled module stays TypeScript-checked. No `!` non-null assertions; everything is resolved
// in initStory or passed in as a non-null parameter (deck wiring always runs when the deck
// exists, the lightbox wiring only when findLightbox succeeds).
```

Why the budget grew from the planned ≈130 lines: the lightbox grew from an open-one-image modal
into a full gallery — srcset-based source resolution plus neighbour preloading, click/arrow-key/
swipe navigation, counters and status regions, gesture-vs-tap discrimination, viewport-locked
scroll compensation, view-transition open/close morphs and a manual `<dialog>` fallback with focus
trap — and every fallback path (no scroll-driven reveal, no `<dialog>`, reduced motion) keeps the
module plain and feature-detected instead of branching into separate bundles.
No dependencies, no third-party bytes.

## 6. Accessibility checklist

- [ ] Root `role="region"` + `aria-roledescription="Karussell"` + German `aria-label` prop.
- [ ] Each slide `role="group"`, `aria-roledescription="Folie"`, `aria-label="Bild X von Y"`.
- [ ] Real `<button>`s for prev/next/dots and the lightbox open control (never pseudo-element or
      div-clickables); the open control is the whole slide image, named by its alt plus the
      sr-only `– Bild groß anzeigen` suffix, disabled without JS; dots expose
      `aria-current="true"` on the active one.
- [ ] Focus order: prev → deck (`tabindex="0"`, arrow-key scrollable region) → next → dots →
      slide image (open) buttons → (dialog when open). Verified by keyboard-only run-through.
- [ ] Dialog semantics: native `<dialog>` gives `role="dialog"`, modal focus containment and Esc;
      the iOS<15.4 fallback explicitly sets `role="dialog"` + `aria-modal="true"`, traps Tab within
      the dialog and handles Escape manually. Focus returns to the slide image open button of the
      slide last shown (the deck also jumps there) on close in all paths.
- [ ] **aria-live policy:** autoplay advances announce NOTHING (no DOM focus moves, status region
      stays silent while `data-autoplay ≠ "stopped"`). After the visitor permanently stops autoplay,
      button-driven navigation writes `„Bild X von Y"` into the `role="status"` region.
- [ ] Visible captions carry the emotional copy; `alt` stays purely factual/descriptive (may differ).
- [ ] Contrast table §4.3 all-green (text ≥ 4.5:1 incl. gold kicker thanks to α 0.95 scrim;
      UI boundaries ≥ 3:1 via rings/disc borders; compound focus indicator).
- [ ] Touch targets: open control = whole slide image (zoom badge 44×44 display-only),
      dots 24×24 hit area, nav 48×48.
- [ ] Reduced motion: no Ken Burns, no autoplay ever, instant captions, ≤ 200 ms fade, `auto`
      scrolling — everything remains operable (§4.10 + JS gate).
- [ ] Reduced transparency: grain dropped where supported.
- [ ] Screen-reader spot-check VoiceOver iOS + NVDA desktop during QA matrix.

## 7. Performance checklist

- **CLS = 0:** every slide box dimensioned by CSS `aspect-ratio` (§4.2 — frame ratio independent of
  intrinsic ratio, so mixed orientations cannot shift layout) **plus** explicit `width`/`height`
  attributes on `<Image>` forwarded from `slide.src.{width,height}` (pre-CSS intrinsic hint, fixes
  today's wrong hard-coded 800×600). Dots/arrows/header occupy reserved rows from first paint.
- **LCP:** slide 1 `loading="eager" fetchpriority="high"` (below-the-fold eager load accepted per
  concept §6 so the deck is instant on arrival; hero H1 text remains the practical LCP candidate).
  Slides 2+ `loading="lazy" decoding="async"`. Astro emits AVIF/WebP `srcset` automatically;
  `sizes="(min-width: 768px) min(72%, 47.5rem), calc(100vw - 4rem)"` prevents over-fetching.
- **JS budget:** zero dependencies, zero third-party. The planned ≤ 3 KB transfer target no longer
  holds — the script is ≈670 source lines (§5, real budget accounting there).
  CSS additions scoped to the component (~6–8 KB raw, gzipped less).
- Fonts unchanged (site-global Bricolage Grotesque 300/400 already loaded).
- Gates: Lighthouse mobile ≥ 95 perf / ≥ 100 a11y on both pages; CLS 0; compare LCP before/after.
- Animation hygiene: compositor-only properties (`transform`, `opacity`) in all keyframes;
  Ken Burns only ever on the active slide and paused while dragging/paused/offscreen.

## 8. Page integration (shipped state)

Both pages build a `slideData` array, derive the kicker numbers from the slide index, assign
`StorySlide[]` and pass only `slides`, `label` and `eyebrow` — no `variant`, no `subline`, no
`season`, no `lightbox` prop exists.

### 8.1 `src/website/src/pages/produkte.astro`

```ts
const slideData = [
  {
    src: Strickgabel,
    alt: "Strickgabel aus Holz mit einer angestrickten Kordel aus bunter Zauberwolle",
    label: "Hofladen",     // topic for the kicker; the label is merged out below
    title: "Strickgabel",
    text: "Mit diesem urigen Werkzeug aus dem Hofladen strickst du aus unserer Zauberwolle gemütliche Halstücher – ganz ohne Nadeln.",
    focal: "center 40%",
  },
  { src: Zauberwolle,  alt: "Strang Zauberwolle: schwarzes Alpakagarn mit pink, grün und blau verzwirnten Fäden", label: "Hofladen", title: "Zauberwolle", text: "Ein Knäuel, tausend Ideen: handgesponnene Alpakawolle in natürlichen Naturtönen, bereit für dein nächstes Herzensprojekt." },
  { src: WolleAmadeus, alt: "Handgesponnener Strang naturbrauner Alpakawolle von Amadeus", label: "Hofladen", title: "Wolle Amadeus", text: "Von Amadeus und seiner Herde bis zum fertigen Knäuel bleibt die Faser bei uns am Hof – gekämmt, gewaschen und handgesponnen." },
  { src: Karten,       alt: "Drei dunkelgrüne Grußkarten mit dem goldenen Alpaka-Signet der Alpakasölde", label: "Hofladen", title: "Karten", text: "Für jeden Anlass liegt etwas Handgemachtes bereit – vom Geburtstagsgruß bis zum kleinen Danke mit einem Foto unserer Alpakas." },
  { src: Polster,      alt: "Zwei Leinenpölster mit Alpakafüllung, bestempelt mit den Namen Ludwig und Amadeus", label: "Zuhause", title: "Pölster", text: "Herrlich weiche Pölster mit Alpakafüllung – sie wärmen im Winter und lassen an warmen Tagen die Faser atmen." },
  { src: Wollpellets,  alt: "Papiersackerl mit 250 g Alpaka-Wollpellets als Naturdünger, mit roter Schleife", label: "Für den Garten", title: "Wollpellets", text: "Von der Schur zurück auf die Weide: naturreine Wollpellets, die deinen Beeten langsam und sanft Nahrung geben." },
];

const slides: StorySlide[] = slideData.map((slide, i) => {
  const { label, ...rest } = slide;
  return { ...rest, kicker: `${String(i + 1).padStart(2, "0")} · ${label}` };
});
```

```html
<Slideshow slides={slides} label="Fotos aus unserem Hofladen" eyebrow="Aus unserem Hofladen" />
```

(`focal` set only where the subject needs protection; defaults apply elsewhere — visual QA §11.)

### 8.2 `src/website/src/pages/alpaka-wanderungen.astro`

```ts
const slideData = [
  {
    src: Wanderung1,
    alt: "Zwei weiße Alpakas mit Halter auf der Wiese im Abendlicht",
    title: "Start am Hof",
    text: "Nach dem Kennenlernen sucht sich jedes Alpaka seinen Menschen für die nächsten zwei Stunden – meistens entscheidet die Fresslaune.",
    focal: "center 60%",
  },
  { src: Wanderung2, alt: "Alpaka an der roten Leine auf einem Feldweg neben einem grünen Getreidefeld", title: "Die Runde beginnt", text: "Durch die Inn-Auen geht es gemütlich voran – immer im Tempo des gemächlichsten Vierbeins.", focal: "center 70%" },
  { src: Wanderung3, alt: "Zwei Wanderer führen ein schwarzes und ein braunes Alpaka über eine winterliche Wiese", title: "Pause mit Aussicht", text: "Mitten in den Inn-Auen bleibt die Runde stehen: Zeit für Streicheleinheiten, Fotos und das weite Grün des Europareservats." },
  { src: Wanderung4, alt: "Drei Alpakas werden auf einem Güterweg durch die Felder geführt", title: "Zurück am Hof", text: "Nach zwei Stunden kehren alle gemeinsam heim – müde Beine, volle Herzen und garantiert ein Foto zu viel.", focal: "center 32%" },
];

const slides: StorySlide[] = slideData.map((slide, i) => ({
  ...slide,
  kicker: `Moment ${String(i + 1).padStart(2, "0")}`,
}));
```

```html
<Slideshow slides={slides} label="Momentaufnahmen von der Wanderung" eyebrow="Unterwegs am Inn" />
```

Placement unchanged on both pages (produkte: cream gallery band after the `auwasser` intro;
wanderungen: between „Details" and „Ausflugstipps"). Kicker numbering is derived from the slide
index in the pages, not hard-coded in the component. Eager/lazy handling stays inside the
component (slide index 0 eager+high, rest lazy).

## 9. Verification

Gate commands:

```bash
cd src/website && pnpm run check && pnpm test && pnpm run test:e2e
```

End-to-end coverage lives in `src/website/e2e/slideshow.spec.ts` — Playwright on Chromium covering
the deck (controls, dots, autoplay, snap/scroll behaviour) and the lightbox (open, gallery
navigation, close and focus/deck restoration) plus the no-JS baseline; the spec starts its own dev
server. The branches' time cost is why we ask for it before every slideshow change; anything added
to the deck or lightbox needs a matching spec here.

Manual test matrix (device/browser specific cases beyond the e2e coverage):

| Case | Expected |
|---|---|
| iOS Safari 18/26 swipe | native momentum, rubber-band at both ends, mandatory snap-center, neighbour peeks |
| iOS Safari lightbox pinch-zoom | zoom works inside dialog (`touch-action: pinch-zoom`, scaling not disabled by viewport meta); gestured close must NOT fire (swipe/pinch vs tap discrimination); close via tap on the empty area, ×, Esc-equivalent/back-swipe |
| Desktop trackpad / shift-wheel | horizontal scroll works; **vertical wheel scrolls the page, never the deck** |
| Keyboard-only | prev → deck (arrow keys move slides) → next → dots → slide image (open); visible gold focus rings; Enter opens lightbox; Esc closes and restores focus to the open button |
| Lightbox gallery | prev/next buttons and arrow keys step ±1 with wrap-around; swipe on touch devices; counter shows „X / Y"; on close the deck jumps to and focuses the slide last shown |
| Reduced motion (OS toggle) | no autoplay ever, no Ken Burns, captions instant, no entrance animation (scroll-driven reveal not applied), advances jump without smooth-scroll |
| No-JS (block scripts) | first slide + caption visible, others reachable by native scroll/swipe/keyboard scroller; NO arrows/dots/lightbox rendered visible; no dead controls (open buttons stay disabled) |
| Autoplay choreography | one 7 s interval for every slideshow; pauses instantly on hover/focus/pointerdown/tab-hide/<40 % visibility; permanently stops after first swipe past ~30 % of a card or any control press; dot sweep matches interval |
| Crops (visual sign-off) | all 10 slides checked on iPhone SE, iPhone 15 Pro Max, Pixel 8, iPad, 1440 px — especially portrait Strickgabel, Wolle_Amadeus, wanderung4 (9:16) |
| Screen reader spot-check | VoiceOver: region announced as Karussell, slides as „Bild X von Y", no announcements during autoplay, status announces only after interaction-stop |
| Lighthouse mobile | ≥ 95 perf, ≥ 100 a11y, CLS 0, both pages |

## 10. Effort & file-change summary

Phases (per concept §7.5):

| Phase | Content | Estimate |
|---|---|---|
| A | Markup, responsive deck math, captions/scrim, editorial header | 0.5–1 d |
| B | Script: tracking, autoplay engine, arrows/dots, lightbox + fallbacks | 1 d |
| C | Motion polish: Ken Burns, staggers, reveal (+view()-upgrade), grain/tint, VT morph | 0.5–1 d |
| D | Copy/crop QA with owner, compat + a11y matrix, CWV audit | 0.5–1 d |
| **Total** | | **~3–4 dev-days** |

Files changed:

| File | Change | LOC est. |
|---|---|---|
| `src/website/src/components/Slideshow.astro` | full rewrite (frontmatter ~20, markup ~120, styles ~570, script ~670) | ~1380 (replaces 73) |
| `src/website/src/pages/produkte.astro` | slides array + new props | +34 / −2 |
| `src/website/src/pages/alpaka-wanderungen.astro` | slides array + new props | +27 / −2 |

No other files touched (no `global.css` changes; all styles scoped).

## 11. Milestones (tracked)

- [ ] Write plan (this document)
- [ ] Phase A: component markup + deck geometry + captions/scrim + header (both pages render)
- [ ] Phase B: script (chrome unlock, observers, tracking, autoplay, controls, lightbox)
- [ ] Phase C: motion & delighters (Ken Burns, stagger, reveal, grain/tint, dot sweep, VT morph)
- [ ] Page integration diffs applied; `pnpm run check && pnpm run build` green
- [ ] Copy pass with owner (10 captions/alt texts finalized)
- [ ] Visual crop sign-off per slide (focal tuning)
- [ ] Compat/a11y/perf matrix executed (§9); PR `feat(website): immersive storytelling slideshow`

## 12. Risks & mitigations

1. **Crop damage on mixed-orientation product shots** → `focal` prop + per-slide visual sign-off
   (matrix above). Highest-risk: Strickgabel (5:7 portrait in the 4/5→3/2 frames), Wolle_Amadeus,
   wanderung4 (9:16 in the same frames).
2. **Ken Burns jank on low-end devices during swipe** → active-slide-only, paused while
   `data-dragging`, transform-only, throttled-CPU testing in phase D.
3. **Astro style-scoping vs runtime state** → data-attribute selector contract (`data-state`,
   `data-js`, `data-autoplay`); never inject classes the stylesheet can't see.
4. **Astro script executes once per page** → module iterates all `.story` instances (both current
   pages have exactly one, but the code stays instance-safe).
5. **`<dialog>` on iOS < 15.4** → div-overlay fallback branch with manual trap/Esc (tiny, tested by
   feature-flag flip during QA).
6. **Autoplay annoyance regression** (today's 4 s infinite loop) → long intervals, aggressive pause
   rules, permanent stop on first interaction, reduced-motion off-switch.
7. **Duplicate `view-transition-name` aborts transitions silently** → just-in-time assignment +
   guaranteed clear in `finished.finally()`; feature-detected, fallback = instant open.
8. **Competing branch `feat/slideshow-concept-1`** rewrites the same file → merge-order coordination
   required; this implementation is self-contained in Slideshow.astro + the two page diffs, so a
   rebase conflict resolves to "pick one component wholesale".

## 13. Deviations from the concept

1. **Scrim bottom stop 0.88 → 0.95** (mid 0.55 → 0.62): the concept's own math showed gold ≈ 3:1 on
   the scrim and banned gold running text, yet styled the kicker gold. Raising the bottom-zone alpha
   makes the gold kicker a compliant 4.7:1 AA text colour and lifts body copy to 8.7:1; visual cost
   is a slightly deeper bottom band.
2. **Ken Burns duration parametrized** (`calc(var(--interval-ms) * 1.5)` → 10.5 s at the single
   7 s interval) instead of the fixed 11 s, so „breath never visibly loops" holds with the delivered
   uniform interval.
3. **Mobile peek geometry re-derived**: the concept's „86 vw card + ~7 vw peek each side + gap"
   sums beyond 100 vw and is unsatisfiable. Locked solvable numbers preserving both cues:
   full-bleed track, card `calc(100vw − 4rem)` (~81 vw), ~1.25 rem sliver; desktop unchanged from
   the concept (72 %/760 px card, ~14 % peek).
4. **aria-live refinement**: concept omits live regions entirely; plan adds a `role="status"`
   region that is silent during autoplay and announces „Bild X von Y" only for button-driven
   navigation after autoplay has been permanently stopped — satisfies APG carousel guidance without
   yanking screen-reader users.
5. **Inactive dots as taubenblau rings instead of `rgba(…,.55)` translucent fills**: the concept's
   claimed ≈4:1 doesn't survive alpha compositing (computes to 1.87:1); solid 2 px rings measure
   6.5:1 and match the active dot's ring language.
6. **Seasonal hook dropped entirely**: no `data-season` attribute and no winter wash remain — the
   wash is always the warm gold one (see also 9 below).
7. **No `variant` prop** — one frame ratio (4/5 mobile, 3/2 from 768 px) and one 7 s interval for
   every slideshow („unify slideshow behavior by dropping variant prop"); portrait photos are
   handled with the per-slide `focal` value.
8. **No `subline` prop**: the header is the eyebrow only, styled as taubenblau caps flanked by
   gold rules (not gold caps) („remove slideshow subline heading").
9. **No `season` and no `lightbox` prop**: both were removed as unused; the lightbox is always
   rendered and there is no `data-season` hook („remove unused slideshow season and lightbox
   props").
10. **No `.slide-expand` button**: the whole slide image is the open control — disabled in markup,
    unlocked by the script („make slide expand button progressive and keep alt name") — named by
    the image alt plus a visually hidden „– Bild groß anzeigen"; the caption lets clicks through
    to it (`pointer-events: none`) („open lightbox when the slide caption is clicked").
11. **Fullscreen opaque lightbox without `::backdrop`** („make slideshow images open a fullscreen
    lightbox"); a tap on the empty area around the image closes it („close lightbox on tap into
    empty area around image"). No `::backdrop` styling remains — the dialog itself is the surface
    the tap targets.
12. **Lightbox gallery navigation**: prev/next buttons, arrow-key and swipe navigation, a counter,
    and on close the deck jumps to and focuses the slide last shown („sync deck and focus on
    lightbox close, ignore multitouch swipe") — the plan's open/close-only modal didn't cover this.
13. **Viewport reveal is CSS-only**: scroll-driven animation where `animation-timeline: view()` is
    supported, otherwise no entrance animation — no observer, no `.in-view`, no `data-css-reveal`
    („move slideshow reveal and frozen state to css").
14. **Kicker numbers are derived from the slide index in the pages** (`"01 · Hofladen"`,
    `"Moment 01"` via `padStart`) („number slideshow kickers automatically").
15. **Colours come from the palette tokens via `color-mix`** instead of literal `rgba(…)` values
    („derive slideshow colours from palette tokens").
16. **The script grew past the planned ≈130-line budget to ≈670 source lines**: lightbox gallery
    navigation (buttons/arrows/swipe, counter, srcset-based src resolution and neighbour
    preloading, gesture-vs-tap discrimination) and the fallbacks (manual `<dialog>` focus trap,
    feature-detected view transitions, reduced-motion and scroll-driven-reveal gates). The module
    splits into `initStory` (resolve deck + status, early-return on missing parts, then lightbox),
    `initDeck` (all deck behaviour, returns a `DeckHandle`) and `initLightbox` (gallery, open/
    close morphs, fallbacks), typed by `LightboxSlide`/`LightboxElements`/`DeckHandle` interfaces.
17. **End-to-end coverage lives in `src/website/e2e/slideshow.spec.ts`** (Playwright on Chromium,
    own dev server): deck and lightbox behaviour plus the no-JS baseline are asserted end to end
    („cover slideshow deck and lightbox end to end").
