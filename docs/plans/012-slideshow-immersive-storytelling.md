# Slideshow „Immersive Storytelling" Implementation Plan

Implements `docs/concepts/design-concept-2-immersive-storytelling.md` as a concrete build spec
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
  variant?: "product" | "hike";     // aspect ratios + autoplay interval (product 7 s, hike 9 s)
  eyebrow?: string;                 // editorial header line 1 (gold caps)
  subline?: string;                 // editorial header line 2 (taubenblau)
  season?: "sommer" | "winter";     // theme hook, default "sommer"
  lightbox?: boolean;               // default true
}
```

Rules:
- No `any`; both pages must compile under `astro check`.
- All caption fields except `alt` are optional; a slide with only `src`+`alt` renders image-only
  (no scrim block, no empty caption box).
- `variant` maps internally: `{ product: { intervalMs: 7000 }, hike: { intervalMs: 9000 } }`;
  the interval is emitted as `style="--interval-ms: 7000ms"` on the root so CSS (Ken Burns duration,
  dot-progress sweep) and JS (autoplay timer) read one source of truth.
- The component reads `slide.src.width` / `slide.src.height` itself and forwards them as explicit
  `width`/`height` props on `<Image>` (single source of truth — pages never duplicate dimensions).
- `season` is shipped as a data-attribute hook only (`data-season="winter"` swaps the warm gold wash
  for a `--himmelblau` wash at the same opacity — pure CSS variable swap); deeper seasonal theming
  stays fast-follow per concept §4.5 tier 2.

## 3. Markup spec

Full DOM tree of `Slideshow.astro`. Every class, role and ARIA attribute listed here is normative.
State toggling uses **data attributes** (not injected classes) so Astro scoped styles keep working
without `:global()` leaks (concept §7.2).

```html
<section
  class="story"
  data-variant={variant}              <!-- "product" | "hike" -->
  data-season={season}                <!-- default "sommer" -->
  data-js="false"                     <!-- script flips to "true"; gates all chrome -->
  style={`--interval-ms:${intervalMs}ms`}
  role="region"
  aria-roledescription="Karussell"
  aria-label={label}
>
  <!-- Editorial header (rendered only if eyebrow or subline set) -->
  <header class="story-header">
    <p class="story-eyebrow">{eyebrow}</p>
    <p class="story-subline">{subline}</p>
  </header>

  <div class="deck-wrap">
    <button class="deck-nav deck-prev" type="button" aria-label="Vorheriges Bild">
      <!-- inline chevron SVG -->
    </button>

    <ul class="deck" tabindex="0"
        aria-label="Bildstreifen – wischen oder mit den Pfeiltasten bewegen">
      {slides.map((slide, i) => (
        <li class="slide" data-state={i === 0 ? "active" : "idle"}
            role="group" aria-roledescription="Folie"
            aria-label={`Bild ${i + 1} von ${slides.length}`}>
          <figure class="slide-card">
            <div class="slide-media">
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
            </div>
            {(slide.kicker || slide.title || slide.text) && (
              <figcaption class="slide-caption">
                {slide.kicker && <p class="caption-kicker">{slide.kicker}</p>}
                {slide.title && <p class="caption-title" >{slide.title}</p>}
                {slide.text  && <p class="caption-text" >{slide.text}</p>}
              </figcaption>
            )}
            {lightbox && (
              <button class="slide-expand" type="button"
                      aria-label={`Bild ${i + 1} groß anzeigen`}
                      aria-haspopup="dialog" data-slide-index={i}>
                <!-- inline magnifier SVG -->
              </button>
            )}
          </figure>
        </li>
      ))}
    </ul>

    <button class="deck-nav deck-next" type="button" aria-label="Nächstes Bild">
      <!-- inline chevron SVG -->
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

{lightbox && (
  <dialog class="lightbox" aria-label="Bildansicht" data-lightbox>
    <figure class="lb-figure">
      <div class="lb-media"><img class="lb-img" src="" alt="" /></div>
      <figcaption class="lb-caption">
        <p class="caption-kicker" data-lb-kicker></p>
        <p class="caption-title"  data-lb-title></p>
        <p class="caption-text"   data-lb-text></p>
      </figcaption>
    </figure>
    <button class="lb-close" type="button" aria-label="Schließen">×</button>
  </dialog>
)}
```

Markup contracts:
- **No hardcoded ids** (the old `id="slideshow"` disappears; two pages must never collide).
- `.deck-nav` buttons are rendered into the DOM but visually hidden until `[data-js="true"]`
  (see §4.10) — no-JS users never see dead controls.
- `<dialog>` is rendered only when `lightbox !== false`. If `window.HTMLDialogElement` is missing
  (iOS < 15.4), the script repurposes it as a fixed overlay `<div role="dialog" aria-modal="true">`
  with manual Esc/focus-trap handling (§6).
- Lightbox `<img>` starts empty (`src="" alt=""`); the script fills `src` (1280px rendition),
  `sizes`, `srcset` hint and `alt` at open time.
- German labels locked: `Vorheriges Bild`, `Nächstes Bild`, `Direktnavigation`,
  `Bild X groß anzeigen`, `Bild X von Y`, `Folie`, `Karussell`, `Schließen`, `Bildansicht`.

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
  box-shadow: 0 12px 32px rgba(31, 31, 29, 0.14);
  /* THE cls guarantee: frame ratio fixed independent of intrinsic ratio */
  aspect-ratio: var(--frame-ratio);
}
[data-variant="product"] { --frame-ratio: 4 / 5; }
[data-variant="hike"]    { --frame-ratio: 3 / 4; }
@media (min-width: 768px) {
  [data-variant="product"] { --frame-ratio: 3 / 2; }
  [data-variant="hike"]    { --frame-ratio: 16 / 9; }
  .slide-card { border-radius: 1.25rem; box-shadow: 0 24px 64px rgba(31, 31, 29, 0.18); }
}
.slide-media img {
  width: 100%; height: 100%;
  object-fit: cover;
  object-position: center 45%;                   /* focal default; inline style overrides */
}
```

Mixed source orientations (portrait Strickgabel/Wolle_Amadeus/wanderung4 vs landscape rest) are
cropped by the fixed frame; `focal` protects subjects. Every crop needs visual sign-off before ship
(§11).

### 4.3 Caption scrim (AA-safe by construction)

Deviates from the concept's gradient stops (0.88 → **0.95** bottom) so the gold kicker passes AA as
real text — see Deviations and the contrast table below.

```css
.slide-caption {
  position: absolute; inset-inline: 0; bottom: 0;
  z-index: 2;
  padding: 3.5rem 1.25rem max(1.25rem, env(safe-area-inset-bottom));
  color: var(--schurwolle);
  background: linear-gradient(
    to top,
    rgba(31, 31, 29, 0.95) 0%,   /* text zone — worst-case composite passes 4.5:1 */
    rgba(31, 31, 29, 0.62) 42%,  /* no text above this line */
    rgba(31, 31, 29, 0)    80%
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
`.slide-caption` (z 2) → `.slide-expand` (z 3).

```css
.slide-media::before {   /* vignette like ImpressionBreak::before + gold soft-light wash */
  content: ""; position: absolute; inset: 0; z-index: 1; pointer-events: none;
  background:
    radial-gradient(120% 90% at 50% 40%, rgba(225, 177, 74, 0.06), transparent 62%),
    radial-gradient(140% 115% at 50% 50%, transparent 58%, rgba(31, 31, 29, 0.18) 100%);
}
[data-season="winter"] .slide-media::before {
  background:
    radial-gradient(120% 90% at 50% 40%, rgba(141, 165, 211, 0.09), transparent 62%),
    radial-gradient(140% 115% at 50% 50%, transparent 58%, rgba(31, 31, 29, 0.18) 100%);
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

### 4.5 Ken Burns — active slide only

Duration derives from the interval (1.5×) so a breath never visibly completes/loops even on the 9 s
hike variant (deviation from the fixed 11 s — see Deviations).

```css
@keyframes kenburns     { from { transform: scale(1.02); } to { transform: scale(1.09) translate(-1.2%, 1%); } }
@keyframes kenburns-alt { from { transform: scale(1.09) translate(1.2%, -1%); } to { transform: scale(1.02); } }

@media (prefers-reduced-motion: no-preference) {
  .slide[data-state="active"] .slide-media img {
    animation: kenburns calc(var(--interval-ms) * 1.5) ease-in-out both;
  }
  .slide:nth-child(even)[data-state="active"] .slide-media img { animation-name: kenburns-alt; }
  .story[data-autoplay="paused"]  .slide-media img,
  .story[data-dragging] .slide-media img { animation-play-state: paused; }
  .story[data-frozen] .slide-media img { animation-play-state: paused; }
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

Default: IntersectionObserver adds `.in-view` once (threshold 0.25) → 600 ms fade/rise.
Upgrade: where `animation-timeline: view()` exists, the script skips the observer and pure CSS drives
it (identical visual result).

```css
@media (prefers-reduced-motion: reduce) {
  .story { opacity: 1; transform: none; }               /* kill-switch baseline */
}
.story:not(.in-view) { opacity: 0; translate: 0 24px; }
.story.in-view {
  opacity: 1; translate: 0 0;
  transition: opacity 600ms ease-out, translate 600ms ease-out;
}
@media (prefers-reduced-motion: reduce) {
  .story.in-view { transition-duration: 150ms; }         /* ≤ 200 ms simple fade */
}

/* Pure-CSS upgrade replaces the IO reveal where supported */
@supports (animation-timeline: view()) {
  @media (prefers-reduced-motion: no-preference) {
    .story[data-css-reveal] {
      animation: story-reveal both ease-out;
      animation-timeline: view();
      animation-range: entry 10% cover 30%;
    }
    .story[data-css-reveal]:not(.in-view) { opacity: 1; translate: none; }
  }
}
@keyframes story-reveal {
  from { opacity: 0.001; translate: 0 24px; }
  to   { opacity: 1; translate: 0 0; }
}
```

Script contract: `CSS.supports("animation-timeline", "view()")` && motion OK → set
`data-css-reveal` and skip creating the reveal observer (visibility gating observer always runs).

Tier-3 parallax (fast-follow, snippet for reference only — do not ship in this PR unless trivial):

```css
@supports (animation-timeline: scroll(nearest inline)) { … translateY ±12px against drift … }
```

### 4.8 Autoplay progress affordance (dots)

Active dot slowly sweeps gold over `--interval-ms` while autoplay is running; static gold disc
otherwise (graceful degradation without registered properties).

```css
@property --dot-progress { syntax: "<number>"; inherits: false; initial-value: 0; }
.story-dot {
  width: 24px; height: 24px;            /* ≥ 24 px hit area */
  display: grid; place-items: center;
  background: none; border: 0; padding: 0; cursor: pointer;
}
.story-dot::before {                     /* visual 8 px disc inside hit area */
  content: ""; width: 8px; height: 8px; border-radius: 50%;
  border: 2px solid var(--taubenblau);   /* inactive: ring, 6.5:1 on cream */
}
.story-dot[aria-current="true"]::before {
  background: conic-gradient(var(--bluetenhonig) calc(var(--dot-progress) * 360deg), transparent 0);
  border-color: var(--taubenblau);       /* ring guarantees the 3:1 boundary */
}
@media (prefers-reduced-motion: no-preference) {
  .story[data-autoplay="running"] .story-dot[aria-current="true"]::before {
    animation: dot-fill var(--interval-ms) linear forwards;
  }
}
@keyframes dot-fill { from { --dot-progress: 0; } to { --dot-progress: 1; } }
```

### 4.9 Controls, expand button, lightbox

```css
.deck-nav {
  position: absolute; top: 50%; translate: 0 -50%; z-index: 4;
  width: 48px; height: 48px; border-radius: 50%;
  background: var(--schurwolle); color: var(--taubenblau);
  border: 1px solid var(--taubenblau);
  box-shadow: 0 12px 32px rgba(31, 31, 29, 0.18);
}
.deck-prev { left: max(1rem, calc((100% - var(--slide-w)) / 2 - 3.5rem)); }
.deck-next { right: max(1rem, calc((100% - var(--slide-w)) / 2 - 3.5rem)); }
.deck-nav:hover { color: var(--bluetenhonig); }
@media (max-width: 767.98px) { .deck-nav { display: none; } }  /* swipe + dots on phones */

.story:not([data-js="true"]) :is(.deck-nav, .story-dots) { display: none; }  /* additive chrome */

.slide-expand {
  position: absolute; right: 0.75rem; bottom: max(0.75rem, env(safe-area-inset-bottom));
  z-index: 3;
  width: 44px; height: 44px; border-radius: 50%;
  display: grid; place-items: center;
  background: rgba(251, 247, 237, 0.92); color: var(--taubenblau);
  border: 1px solid var(--taubenblau);
}

.lightbox {
  border: 0; padding: 0; max-width: min(92vw, 1200px); max-height: 92dvh;
  background: var(--schwarz); color: var(--schurwolle);
  border-radius: 1.25rem; overflow: hidden;
}
.lightbox::backdrop {
  background: rgba(31, 31, 29, 0.72);
  backdrop-filter: blur(6px);
}
.lb-figure { margin: 0; touch-action: pinch-zoom; }   /* native pinch-zoom inside dialog */
.lb-media img { display: block; width: 100%; height: auto; max-height: 74dvh; object-fit: contain; }
.lb-caption { padding: 1rem 1.25rem max(1rem, env(safe-area-inset-bottom));
              background: var(--schwarz); }
.lb-close {
  position: absolute; top: max(0.75rem, env(safe-area-inset-top)); right: 0.75rem;
  width: 44px; height: 44px; border-radius: 50%;
  background: rgba(251, 247, 237, 0.92); color: var(--taubenblau);
}
@supports (transition-behavior: allow-discrete) {
  .lightbox[open] { opacity: 1; transition: opacity 200ms ease-out, overlay 200ms ease-out allow-discrete; }
  @starting-style { .lightbox[open] { opacity: 0; } }
}
```

Focus-visible ring (all interactive elements):

```css
:where(.deck-nav, .story-dot, .slide-expand, .lb-close):focus-visible {
  outline: 3px solid var(--bluetenhonig);
  outline-offset: 2px;
  box-shadow: 0 0 0 6px rgba(75, 91, 115, 0.85);   /* compliant compound indicator */
}
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
  .story { scroll-behavior: auto !important; }
  .story:not([data-js]) .deck { scroll-behavior: auto; }
}
```

JS additionally never arms autoplay when `matchMedia('(prefers-reduced-motion: reduce)').matches`
and uses `behavior: 'auto'` scrolls under reduce (CSS alone cannot stop scripted smooth-scroll).

## 5. Script spec (inline `<script>`, budget ≈ 130 lines / ≤ 3 KB min+gzip)

Astro bundles the co-located script once per page; the module iterates instances:

```js
// ── 0 · setup (≈ 8 lines)
const REDUCED = matchMedia("(prefers-reduced-motion: reduce)");
for (const story of document.querySelectorAll(".story")) init(story);

function init(story) {
  // refs: deck, slides[], dots[], prev/next, expands[], status el, lightbox parts
  // ── 1 · unlock chrome (≈ 2 lines)
  story.dataset.js = "true";
  // ── 2 · observers (≈ 15 lines)
  //   visibilityObserver threshold [0.4]: tracks story.dataset.autoplay eligibility
  //   revealObserver threshold 0.25, one-shot → story.classList.add("in-view"), unobserve
  //   skip revealObserver entirely when CSS.supports("animation-timeline","view()")
  //     → instead story.dataset.cssReveal = ""  (pure-CSS reveal takes over)
  // ── 3 · active-slide tracking (≈ 20 lines)
  //   const midpoints = slides.map(s => s.offsetLeft + s.offsetWidth / 2)
  //   debounced scroll handler (rAF-throttled): nearest midpoint to
  //   deck.scrollLeft + deck.clientWidth/2 → setActive(i)
  //   setActive: swap data-state active/idle, dots' aria-current, reset animations
  //     (remove/re-add nothing — data-state change restarts bound keyframes)
  //   'scrollend' listener re-syncs exact index after momentum settles
  // ── 4 · autoplay engine (≈ 35 lines)
  //   state machine on story[data-autoplay]: "idle" | "running" | "paused" | "stopped"(terminal)
  //   eligible() = visible≥40% && !document.hidden && !REDUCED.matches && state!=="stopped"
  //   arm(): clearTimeout; if(!eligible()) return; timer=setTimeout(advance, intervalMs)
  //   advance(): next=(active+1)%n; goTo(next); arm()
  //   goTo(i): deck.scrollTo({ left: midpoints[i]-clientWidth/2,
  //                            behavior: REDUCED.matches ? "auto":"smooth" })
  //   pause(): data-autoplay="paused" + clearTimeout
  //   listeners: mouseenter/mouseleave (pointer:fine only), focusin/focusout,
  //     pointerdown, document visibilitychange, visibilityObserver callback
  //   STOP (permanent): first intentional interaction →
  //     horizontal drag past 30% of a card width (touchstart/touchend ΔX check),
  //     any click on nav/dot/expand, ArrowLeft/Right on the deck
  //     → state="stopped", data-autoplay="stopped", data-frozen="" (KB freezes mid-breath)
  // ── 5 · controls (≈ 20 lines)
  //   prev/next click → step(±1) + announce(); dot click → goTo(i) + announce()
  //   deck keydown ArrowLeft/ArrowRight → step(±1), preventDefault
  //   announce(): ONLY when state==="stopped":
  //     status.textContent = `Bild ${i+1} von ${n}`  (role=status region)
  //     (autoplay advances NEVER announce — SR users are never yanked)
  // ── 6 · lightbox (≈ 30 lines)
  //   open(i): fill lb img {srcset/sizes from slide.src, src=1280 rendition, alt},
  //     copy kicker/title/text nodes; lastFocus = expands[i]
  //     lock scroll: html.style.overflow="hidden" (+ scrollbar-gutter compensation)
  //     if (!window.HTMLDialogElement) fallback: dialog.setAttribute("role","dialog"),
  //       aria-modal="true", add .is-open (fixed overlay), wire Esc + simple focus trap,
  //       remember previously focused element
  //     else: morph support? →
  //       if (document.startViewTransition) assign view-transition-name to the clicked
  //         figure JUST-IN-TIME, wrap showModal() in startViewTransition, clear name in
  //         transition.finished.finally() (duplicate names abort silently otherwise)
  //       else dlg.showModal()
  //   close(): dlg.close()/hide fallback → restore focus to lastFocus, unlock scroll,
  //     clear VT names
  //   close paths: native Esc (dialog), .lb-close click, click on dialog element itself
  //     but outside .lb-figure (backdrop tap), system back-swipe (native)
}
```

Budget accounting: ≈130 lines source ≈ 2.8 KB minified ≈ 1.4 KB gzip — inside the ≤ 3 KB target.
No dependencies, no third-party bytes.

## 6. Accessibility checklist

- [ ] Root `role="region"` + `aria-roledescription="Karussell"` + German `aria-label` prop.
- [ ] Each slide `role="group"`, `aria-roledescription="Folie"`, `aria-label="Bild X von Y"`.
- [ ] Real `<button>`s for prev/next/dots/expand (never pseudo-element or div-clickables) with the
      locked German labels; dots expose `aria-current="true"` on the active one.
- [ ] Focus order: prev → deck (`tabindex="0"`, arrow-key scrollable region) → next → dots →
      expand buttons → (dialog when open). Verified by keyboard-only run-through.
- [ ] Dialog semantics: native `<dialog>` gives `role="dialog"`, modal focus containment and Esc;
      the iOS<15.4 fallback explicitly sets `role="dialog"` + `aria-modal="true"`, traps Tab within
      the dialog and handles Escape manually. Focus returns to the invoking expand button on close
      in all paths.
- [ ] **aria-live policy:** autoplay advances announce NOTHING (no DOM focus moves, status region
      stays silent while `data-autoplay ≠ "stopped"`). After the visitor permanently stops autoplay,
      button-driven navigation writes `„Bild X von Y"` into the `role="status"` region.
- [ ] Visible captions carry the emotional copy; `alt` stays purely factual/descriptive (may differ).
- [ ] Contrast table §4.3 all-green (text ≥ 4.5:1 incl. gold kicker thanks to α 0.95 scrim;
      UI boundaries ≥ 3:1 via rings/disc borders; compound focus indicator).
- [ ] Touch targets: expand 44×44, dots 24×24 hit area, nav 48×48.
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
- **JS budget:** ≤ 3 KB transfer total (target ~1.4 KB gzip), zero dependencies, zero third-party.
  CSS additions scoped to the component (~6–8 KB raw, gzipped less).
- Fonts unchanged (site-global Bricolage Grotesque 300/400 already loaded).
- Gates: Lighthouse mobile ≥ 95 perf / ≥ 100 a11y on both pages; CLS 0; compare LCP before/after.
- Animation hygiene: compositor-only properties (`transform`, `opacity`) in all keyframes;
  Ken Burns only ever on the active slide and paused while dragging/paused/offscreen.

## 8. Page integration diffs

### 8.1 `src/website/src/pages/produkte.astro`

```diff
 import Karten from "../images/produkte/Karten.jpg";
 import Polster from "../images/produkte/Polster.jpg";
 import Wollpellets from "../images/produkte/Wollpellets.jpg";
 
-const images = [Strickgabel, Zauberwolle, WolleAmadeus, Karten, Polster, Wollpellets];
+const slides = [
+  {
+    src: Strickgabel,
+    alt: "Holzerne Strickgabel mit begonnener Wollearbeit auf einem Holztisch",
+    kicker: "01 · Hofladen", title: "Strickgabel",
+    text: "Mit diesem urigen Werkzeug aus dem Hofladen strickst du aus unserer Zauberwolle gemütliche Halstücher – ganz ohne Nadeln.",
+    focal: "center 50%",
+  },
+  {
+    src: Zauberwolle,
+    alt: "Knäuel handgesponnener Alpakawolle in natürlichen Naturtönen",
+    kicker: "02 · Hofladen", title: "Zauberwolle",
+    text: "Ein Knäuel, tausend Ideen: handgesponnene Alpakawolle in natürlichen Naturtönen, bereit für dein nächstes Herzensprojekt.",
+  },
+  {
+    src: WolleAmadeus,
+    alt: "Handgesponnenes Wollknäuel der Alpakawolle Amadeus",
+    kicker: "03 · Hofladen", title: "Wolle Amadeus",
+    text: "Von Amadeus und seiner Herde bis zum fertigen Knäuel bleibt die Faser bei uns am Hof – gekämmt, gewaschen und handgesponnen.",
+  },
+  {
+    src: Karten,
+    alt: "Stapel handgefertigter Karten mit Alpakamotiven",
+    kicker: "04 · Hofladen", title: "Karten",
+    text: "Für jeden Anlass liegt etwas Handgemachtes bereit – vom Geburtstagsgruß bis zum kleinen Danke mit einem Foto unserer Alpakas.",
+  },
+  {
+    src: Polster,
+    alt: "Dekoratives Kissen gefüllt mit weicher Alpakawolle",
+    kicker: "05 · Zuhause", title: "Pölster",
+    text: "Herrlich weiche Pölster mit Alpakafüllung – sie wärmen im Winter und lassen an warmen Tagen die Faser atmen.",
+  },
+  {
+    src: Wollpellets,
+    alt: "Naturreine Wollpellets als Dünger in einer Schale",
+    kicker: "06 · Für den Garten", title: "Wollpellets",
+    text: "Von der Schur zurück auf die Weide: naturreine Wollpellets, die deinen Beeten langsam und sanft Nahrung geben.",
+  },
+];
 ...
 <section class="section">
-  <Slideshow images={images} />
+  <Slideshow
+    slides={slides}
+    label="Fotos aus unserem Hofladen"
+    variant="product"
+    eyebrow="Aus unserem Hofladen"
+    subline="Handgesponnen, gefüllt und verpackt bei uns am Hof."
+    lightbox
+  />
 </section>
```

(`focal` set only where the subject needs protection; defaults apply elsewhere — visual QA §11.)

### 8.2 `src/website/src/pages/alpaka-wanderungen.astro`

```diff
-import impression_72 from "../images/impressions/impression_72.jpg";
-
-const images = [Wanderung1, Wanderung2, Wanderung3, Wanderung4];
+import impression_72 from "../images/impressions/impression_72.jpg";
+
+const slides = [
+  {
+    src: Wanderung1,
+    alt: "Alpakas und ihre Begleiter beim Start der Wanderung am Hof",
+    kicker: "Moment 01", title: "Start am Hof",
+    text: "Nach dem Kennenlernen sucht sich jedes Alpaka seinen Menschen für die nächsten zwei Stunden – meistens entscheidet die Fresslaune.",
+  },
+  {
+    src: Wanderung2,
+    alt: "Alpakas wandern mit ihren Menschen über einen Feldweg in den Inn-Auen",
+    kicker: "Moment 02", title: "Die Runde beginnt",
+    text: "Durch die Inn-Auen geht es gemütlich voran – immer im Tempo des gemächlichsten Vierbeins.",
+  },
+  {
+    src: Wanderung3,
+    alt: "Die Wandergruppe hält mit Blick über das Europareservat Unterer Inn",
+    kicker: "Moment 03", title: "Pause mit Aussicht",
+    text: "Mitten in den Inn-Auen bleibt die Runde stehen: Zeit für Streicheleinheiten, Fotos und das weite Grün des Europareservats.",
+  },
+  {
+    src: Wanderung4,
+    alt: "Alpakas auf dem Heimweg zur Alpakasölde",
+    kicker: "Moment 04", title: "Zurück am Hof",
+    text: "Nach zwei Stunden kehren alle gemeinsam heim – müde Beine, volle Herzen und garantiert ein Foto zu viel.",
+  },
+];
 ...
-  <Slideshow images={images} />
+  <Slideshow
+    slides={slides}
+    label="Momentaufnahmen von der Wanderung"
+    variant="hike"
+    eyebrow="Unterwegs am Inn"
+    subline="Zwei gemütliche Stunden – Momentaufnahmen von der Strecke."
+    lightbox
+  />
```

Placement unchanged on both pages (produkte: cream gallery band after the `auwasser` intro;
wanderungen: between „Details" and „Ausflugstipps"). `season` omitted everywhere (default
`sommer`). Eager/lazy handled inside the component (slide index 0 eager+high, rest lazy).

## 9. Verification

Commands (must pass before PR):

```bash
cd src/website && pnpm run check && pnpm run build
```

Manual test matrix:

| Case | Expected |
|---|---|
| iOS Safari 18/26 swipe | native momentum, rubber-band at both ends, mandatory snap-center, neighbour peeks |
| iOS Safari lightbox pinch-zoom | zoom works inside dialog (`touch-action: pinch-zoom`, scaling not disabled by viewport meta); close via backdrop tap, ×, Esc-equivalent/back-swipe |
| Desktop trackpad / shift-wheel | horizontal scroll works; **vertical wheel scrolls the page, never the deck** |
| Keyboard-only | prev → deck (arrow keys move slides) → next → dots → expand; visible gold focus rings; Enter opens lightbox; Esc closes and restores focus |
| Reduced motion (OS toggle) | no autoplay ever, no Ken Burns, captions instant, reveal ≤ 200 ms fade or none, advances jump without smooth-scroll |
| No-JS (block scripts) | first slide + caption visible, others reachable by native scroll/swipe/keyboard scroller; NO arrows/dots/lightbox rendered visible; no dead controls |
| Autoplay choreography | products 7 s, hikes 9 s; pauses instantly on hover/focus/pointerdown/tab-hide/<40 % visibility; permanently stops after first swipe past ~30 % of a card or any control press; dot sweep matches interval |
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
| `src/website/src/components/Slideshow.astro` | full rewrite (frontmatter ~40, markup ~80, styles ~180, script ~130) | ~430 (replaces 73) |
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
   (matrix above). Highest-risk: Strickgabel (5:7 portrait in 3:2 desktop frame), Wolle_Amadeus,
   wanderung4 (9:16 in 16:9 frame).
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
2. **Ken Burns duration parametrized** (`calc(var(--interval-ms) * 1.5)` → 10.5 s / 13.5 s) instead
   of the fixed 11 s, so „breath never visibly loops" holds for the 9 s hike interval too.
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
6. **Seasonal hook ships as an inert CSS-variable hook only** (`data-season` swaps the gold wash for
   `--himmelblau`); full winter treatment stays fast-follow per the concept's tiering.
