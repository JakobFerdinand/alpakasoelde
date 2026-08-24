# Design Concept 2 — "Immersive Storytelling Slideshow"

**Status:** Proposal · **Date:** August 2026 · **Author:** Creative front-end concept
**Scope:** Replacement for `src/website/src/components/Slideshow.astro` as used on
`src/pages/produkte.astro` (6 Hofladen product photos) and `src/pages/alpaka-wanderungen.astro`
(4 hike photos). No other components are touched.

---

## 1. Vision & Moodboard-in-Words

Today the slideshow is a letterboxed TV in the corner of the room: a fixed-height box,
images floating on cream, a silent 4-second crossfade nobody asked for. The pages around it
already tell stories — `ServiceHero` opens them like a magazine cover (full-bleed photo,
dark scrim, big centered title), and `ImpressionBreak` cuts through the page like a full-width
photo spread in a print feature. Only the slideshow interrupts the narrative instead of
advancing it.

**The vision:** turn the slideshow into the *center spread* of each page. A calm, cinematic
photo essay — farm magazine meets modern web. Each slide becomes a small editorial card:
a generously cropped photograph with a slow, almost imperceptible Ken Burns breath, and a
short German story fragment set in the site's own voice ("du"-form, warm, personal).
You flip through it with your thumb the way you'd leaf through a lookbook; the next card
peeks in from the edge and invites the next swipe. On desktop it reads as a framed gallery
wall: arrows, dots, unhurried ambient autoplay that pauses the moment you reach for it.

**Moodboard in words, mapped to existing tokens:**

| Reference | What we take from it |
|---|---|
| `ServiceHero` | Full-bleed confidence, dark gradient scrim with cream type on top (`--schurwolle` on near-black), centered composition |
| `ImpressionBreak` | Full-bleed photographic rhythm, vignette treatment (`radial-gradient` darkening at edges), images treated as atmosphere, not thumbnails |
| `--bluetenhonig #e1b14a` | Warm gold as the *only* accent: active dot, focus rings, kicker labels, arrow hover — the candlelight of the brand |
| `--schurwolle #fbf7ed` | Page stage behind the deck; caption text colour |
| `--taubenblau #4b5b73` | Inactive UI glyphs, secondary caption meta text on light ground |
| `--schwarz #1f1f1d` | Scrim base colour (warm-black, matches body text ink) and grain tint |

Texture ideas carried from the physical farm: paper-grain film texture at ~5 % opacity,
a whisper of warm gold light-tint (soft-light blend), deep-but-soft shadows like wool felt
on wood. Nothing glossy, nothing neon — the deck should feel like prints laid on a wooden
table in the Hofladen.

---

## 2. Goals / Non-Goals

### Goals
1. **Emotional upgrade:** each slide carries a one-sentence German story fragment, not just a filename-less photo.
2. **Mobile-first gesture UX:** native-feeling swipe with peeking neighbours, rubber-band overscroll, snap physics — zero custom gesture code.
3. **Calm ambient autoplay** that behaves like candlelight, not a billboard: long intervals, instant pause on interaction/offscreen/tab-hidden, hard off-switch for reduced-motion users.
4. **Editorial typography** for captions with WCAG-AA-safe scrim technique using existing tokens.
5. **Zero-dependency implementation:** pure CSS foundation + ≤ 3 KB inline vanilla JS; works with JavaScript disabled.
6. **Detail-lover path:** tap-to-expand lightbox with pinch-zoom (especially for product shots).
7. **No layout shift, lazy loading below the fold**, budget-friendly Core Web Vitals.

### Non-Goals
- No Svelte islands, frameworks, or carousel libraries (website stays zero-client-JS-by-default).
- No replacement of `ImpressionBreak` or `ServiceHero`; this is one component.
- No video, no audio, no cross-page view transitions, no SSR changes.
- No CMS/backoffice authoring UI — captions live in the page frontmatter arrays.
- No Chrome-only CSS (`::scroll-button`, `::scroll-marker`, anchor positioning) as a *foundation* — they may only decorate, never carry, the experience (see §8).

---

## 3. UX Flow

### 3.1 Mobile (primary)

1. User scrolls past the intro ("Produkte" copy on `auwasser` ground / "Details" list).
   The deck enters the viewport and gently fades/rises into place once (one-time reveal).
2. First slide fills most of the width (~86 vw card, ~7 vw of the next card peeking on the
   trailing edge — the card-deck cue). Caption sits on the lower scrim: gold kicker
   (`01 · Hofladen`), product/moment title, one warm sentence.
3. Natural horizontal swipe with native momentum; iOS rubber-band at both ends comes free
   from native scroll physics; mandatory snap centers each card.
4. Tapping a card's expand affordance (bottom-right magnifier icon, 44 px target) opens the
   lightbox: larger image, full caption, pinch-zoom enabled, close via tap-outside, × button,
   or system back-swipe.
5. Below the card: five/six dots (current = gold). Swiping updates them.
6. If untouched for the dwell time, the deck advances itself slowly — until the first touch,
   which permanently hands control to the visitor for the rest of the visit.

### 3.2 Desktop

1. Deck renders inside the 1200 px container as a 16:9 framed card with visible neighbour
   slivers; deeper drop-shadow, larger radius.
2. Circular prev/next buttons (cream disc, `taubenblau` chevron, gold on hover) float over
   the card edges; visible on section hover/focus-within, always keyboard-reachable.
3. Trackpad horizontal swipe and shift-wheel scroll work natively. **Vertical mouse wheel is
   deliberately *not* hijacked** — page scroll must stay sacred.
4. Keyboard: Tab reaches prev button → track (arrow-key scrollable region) → next button →
   dots → expand buttons. Enter/Space on a card expands it.
5. Same autoplay rules as mobile; hovering pauses immediately.

### 3.3 Placement changes (both pages)

- The deck gains a small editorial header inside the container: eyebrow label + one line.
  - Produkte: eyebrow **„Aus unserem Hofladen"**, sub-line „Handgesponnen, gefüllt und verpackt bei uns am Hof."
  - Wanderungen: eyebrow **„Unterwegs am Inn"**, sub-line „Zwei gemütliche Stunden – Momentaufnahmen von der Strecke."
- Produkte page: the slideshow section keeps its cream (`--schurwolle`) stage, creating a
  deliberate band rhythm: `auwasser` intro → cream gallery → following sections.
- Wanderungen page: deck sits between "Details" and "Ausflugstipps" exactly as today.

---

## 4. Visual Design Spec

### 4.1 Layout & geometry

```
┌───────────────────────────────────────────────┐ cream stage (--schurwolle)
│   EYEBROW (gold, letterspaced caps, 12px)     │
│   Sub-line (taubenblau, 15px)                 │
│                                               │
│ ╭──╮ ╭─────────────────────────────╮ ╭──╮    │
│ │  │ │                             │ │  │    │  ← card deck, snap-center
│ │pe│ │        photograph            │ │pe│    │
│ │ek│ │      object-fit: cover       │ │ek│    │
│ ╰──╯ │▓▓▓▓▓▓▓▓▓▓ scrim ▓▓▓▓▓▓▓▓▓▓▓▓│ ╰──╯    │
│      │ KICKER                       │          │
│      │ Titel (20–28px)              │ [⤢]      │ ← expand button
│      │ Ein warmer Satz (15px)       │          │
│      ╰─────────────────────────────╯          │
│              ● ○ ○ ○ ○ ○                      │ ← dots row
└───────────────────────────────────────────────┘
```

| Property | Mobile < 768 px | Desktop ≥ 768 px |
|---|---|---|
| Slide width | `calc(100vw − 3.5rem)` (~86 vw) | `min(72%, 760px)` of container |
| Peek | ~7 vw each side | ~12–14 % each side |
| Gap | 1 rem | 1.5 rem |
| Aspect (product variant) | **4 / 5** (portrait bias) | **3 / 2** |
| Aspect (hike variant) | **3 / 4** | **16 / 9** |
| Radius | 1 rem | 1.25 rem |
| Shadow | `0 12px 32px rgba(31,31,29,.14)` | `0 24px 64px rgba(31,31,29,.18)` |
| Stage padding-block | 2.5 rem | 4 rem |

Mixed source orientation is handled by `object-fit: cover` in the fixed-aspect frame.
⚠️ Two product shots are portrait (`Strickgabel.jpeg`, `Wolle_Amadeus.jpg`) while four are
landscape — every slide gets an optional `focal` property (maps to `object-position`,
default `center 45%`) so the owner can protect the subject from cropping. Verify each crop
visually before ship (rollout checklist §10).

### 4.2 Caption & scrim (AA-safe by construction)

Caption block anchored bottom-left inside the card:

```css
.slide-caption {
  position: absolute; inset-inline: 0; bottom: 0;
  padding: 3.5rem 1.25rem max(1.25rem, env(safe-area-inset-bottom));
  color: var(--schurwolle);
  background: linear-gradient(
    to top,
    rgba(31, 31, 29, 0.88) 0%,     /* --schwarz @ 88 % */
    rgba(31, 31, 29, 0.55) 42%,
    rgba(31, 31, 29, 0) 78%
  );
}
.slide-caption p { text-shadow: 0 1px 8px rgba(31, 31, 29, 0.35); }
```

**Contrast math (worst case = pure-white photo pixel under the scrim):**
- Bottom zone α = 0.88 → composite luminance ≈ `0.88·0.013 + 0.12·1.0 = 0.131`;
  `--schurwolle` (L ≈ 0.93) yields **≈ 5.4 : 1** ✅ AA normal text.
- Title/kicker sit in the ≥ 0.85 α zone; the fading upper zone carries no text.
- Gold `--bluetenhonig` measures only ≈ 3 : 1 on the scrim → reserved for **non-text UI**
  (active dot, expand icon, kicker underline) which needs 3 : 1 ✅ — never for running text.
- On the cream stage outside the card, inactive dots use `rgba(75,91,73…)` → actually
  `rgba(--taubenblau, .55)` ≈ 4 : 1 against cream; active dot = gold disc with a 2 px
  `--taubenblau` ring to guarantee the 3 : 1 non-text boundary on light ground.

Typography: kicker = 0.75 rem, uppercase, letter-spacing 0.14 em, `--bluetenhonig` with
`--schurwolle` text-shadow-free; title = Bricolage Grotesque 400 at clamp(1.25rem, 2.6vw, 1.75rem);
body sentence = 300 weight, 0.95–1.05 rem, max-width 42ch.

### 4.3 Copy structure & German examples

Per-slide schema (frontmatter-authored):

```ts
{
  src: Strickgabel,                    // astro:assets ImageMetadata
  alt: "Holzne Strickgabel mit begonnener Wollearbeit auf einem Holztisch",
  kicker: "01 · Hofladen",
  title: "Strickgabel",
  text: "Mit diesem urigen Werkzeug aus dem Hofladen strickst du aus unserer Zauberwolle gemütliche Halstücher – ganz ohne Nadeln.",
  focal: "center 50%",                 // optional object-position
}
```

Rules: `alt` describes the *photograph* factually (screen-reader audience, may differ from
the visible caption); `title`/`text` are the emotional layer, one sentence max, "du"-form,
no marketing superlatives — the site's existing warm tone.

**Produkte page — two finished captions:**

1. **Zauberwolle** — Kicker `02 · Hofladen`
   > „Ein Knäuel, tausend Ideen: handgesponnene Alpakawolle in natürlichen Naturtönen, bereit für dein nächstes Herzensprojekt."
2. **Wollpellets** — Kicker `06 · Für den Garten`
   > „Von der Schur zurück auf die Weide: naturreine Wollpellets, die deinen Beeten langsam und sanft Nahrung geben."

**Alpaka-Wanderungen page — two finished captions:**

1. **Start am Hof** — Kicker `Moment 01`
   > „Nach dem Kennenlernen sucht sich jedes Alpaka seinen Menschen für die nächsten zwei Stunden – meistens entscheidet die Fresslaune."
2. **Pause mit Aussicht** — Kicker `Moment 03`
   > „Mitten in den Inn-Auen bleibt die Runde stehen: Zeit für Streicheleinheiten, Fotos und das weite Grün des Europareservats."

### 4.4 Lightbox

- Native `<dialog>` element (iOS Safari 15.4+, universal elsewhere); fallback branch:
  if `!window.HTMLDialogElement`, toggle a fixed-position overlay `<div>` — identical visuals.
- Content: largest `srcset` rendition (Astro `<Image widths=[1280,1920]>`), full caption
  underneath on a `--schwarz` panel, close × top-right (safe-area aware).
- **Pinch-zoom:** the page viewport meta is `width=device-width` (Layout.astro:22) — scaling
  is *not* disabled, so native pinch-zoom works inside the dialog; additionally set
  `touch-action: pinch-zoom` on the figure. Do not introduce `user-scalable=no`.
- Close paths: Esc (native dialog), backdrop click, × button; focus returns to the invoking
  expand button; scroll locked behind the dialog.
- Where supported, the thumbnail *morphs* into the lightbox via same-document View
  Transition (§8); elsewhere it simply opens instantly.

### 4.5 Delighters (tiered, all optional)

| Tier | Delighter | Technique | Off-state |
|---|---|---|---|
| 1 (ship) | Film grain | Inline SVG `feTurbulence` data-URI tile, opacity .05, one static layer | n/a (static, free) |
| 1 (ship) | Warm brand tint | `radial-gradient` vignette (like `ImpressionBreak::before`) + gold soft-light wash at 6 % | n/a |
| 2 (fast follow) | Seasonal hook | `[data-season="winter"]` swaps tint token gold → `--himmelblau` wash, grain up a notch; set via prop, later via month | default "sommer" |
| 3 (enhancement) | Layer parallax during swipe | `animation-timeline: scroll(nearest inline)` nudging caption ±12 px against image drift | static caption |

---

## 5. Motion Spec

All timings assume `prefers-reduced-motion: no-preference`. Under `reduce`, §5.6 applies.

### 5.1 Transition vocabulary
Primary transition = **slide** (native scroll-snap translation). It is the honest gesture
metaphor on mobile. Crossfade and scale remain *options* in code (CSS class variants) but are
not defaults; a hybrid is explicitly out of scope for v1 to keep physics truthful.

### 5.2 Ken Burns (per-slide breath)
```css
@media (prefers-reduced-motion: no-preference) {
  .slide.is-active img { animation: kenburns 11s ease-in-out both; }
  .slide:nth-child(even).is-active img { animation-name: kenburns-alt; }
}
@keyframes kenburns     { from { transform: scale(1.02) } to { transform: scale(1.09) translate(-1.2%, 1%) } }
@keyframes kenburns-alt { from { transform: scale(1.09) translate(1.2%, -1%) } to { transform: scale(1.02) } }
```
- Runs **only on the active slide** (class toggled by JS), duration ≈ 1.5× the autoplay
  interval so the breath completes just after the advance — never visibly loops.
- Transform-only ⇒ compositor thread; `overflow: hidden` + radius clip on the frame.
- Paused whenever the deck is paused (autoplay pause state also suspends KB via
  `animation-play-state`), and suspended during active pointer-dragging.

### 5.3 Caption entrance (staggered)
On slide activation, children animate in sequence: kicker → title → sentence.
Each: `opacity 0→1`, `translateY(12px)→0`, 320 ms ease-out, delays 0/60/140 ms.
Implementation: re-trigger by toggling `is-active` (animations bound to the class), or
`@starting-style` where baseline (verified §8). Reduced motion: children render instantly.

### 5.4 Viewport entry reveal (once)
Whole deck: `opacity 0→1`, `translateY(24px)→0`, 600 ms ease-out, triggered by
IntersectionObserver adding `.in-view` (fires once, threshold 0.25). Optional upgraded
version gated by `@supports (animation-timeline: view())` replaces IO with a pure-CSS
`view()` timeline — identical visual result.

### 5.5 Ambient autoplay choreography
| Rule | Value |
|---|---|
| Interval | Produkte 7 s · Wanderungen 9 s (hike moments breathe longer) |
| Start conditions | Component ≥ 40 % visible **and** tab focused **and** reduced-motion off |
| Pause triggers | hover, focus-within, pointerdown, offscreen (< 40 %), `visibilitychange` |
| Stop trigger | First intentional interaction (swipe past 30 % of a card, arrow/dot click, expand) — autoplay ends for the session; KB freezes on current framing |
| Advance | `scrollTo({ left, behavior: 'smooth' })`; wrap-around to slide 1 |
| Progress affordance | Active dot slowly fills gold (2 px ring sweep) over the interval — calm countdown, no bars |

Rationale: "pause on first interaction" beats "resume after N seconds" — visitors who grab
control are *reading*, and a moving page under a reading thumb is the #1 carousel complaint.

### 5.6 `prefers-reduced-motion: reduce`
No Ken Burns, no autoplay (ever), no entrance staggers (captions static), entry reveal becomes
simple opacity fade ≤ 200 ms or none, smooth-scroll advances become `behavior: 'auto'`.
Lightbox morph skipped. Everything remains fully operable.

---

## 6. Accessibility & Performance Guardrails

**Structure & semantics**
- Root: `<section role="region" aria-roledescription="Karussell" aria-label={label}>`
  (e.g. „Fotos aus unserem Hofladen").
- Each slide: `<figure role="group" aria-roledescription="Folie" aria-label="Bild 2 von 6">`.
- Real `<button>` elements for prev/next (`aria-label="Vorheriges Bild"` / `"Nächstes Bild"`)
  and dots (`aria-current="true"` on active) — these replace the pseudo-element carousel
  controls that Safari/Firefox lack (§8).
- Visible captions double as the accessible description; `alt` stays purely descriptive.
- Focus order: prev → track → next → dots → expands; visible `:focus-visible` ring in
  `--bluetenhonig`, 3 : 1 against both cream and scrim.

**Contrast** — see §4.2; verified ≥ 4.5 : 1 text, ≥ 3 : 1 UI boundaries, both themes.

**No CLS:** every slide box is dimensioned by CSS `aspect-ratio` *plus* intrinsic
width/height attributes from `astro:assets` — space is reserved pre-image-load. Fonts are
site-global (no new loads). Dots/arrows occupy reserved rows (no late insertion).

**Loading strategy:** slide 1 `loading="eager" fetchpriority="high"`, remaining slides
`loading="lazy" decoding="async"`. Astro emits AVIF/WebP `srcset` automatically. Estimated
added weight: ~4 KB CSS (scoped) + ≤ 3 KB JS (min+gzip) — total well under the 2–3 KB JS
budget target; **zero** third-party bytes.

**Works without JS:** the foundation is a plain horizontally scrollable, snap-aligned list —
first slide fully visible with caption, all others reachable by touch/trackpad swipe.
Scrollbar is hidden *only* after the script sets `data-js` on the root (no-JS users keep a
native scrollbar + keyboard-focusable scroller). Arrows/dots/lightbox/autoplay are additive.

**Reduced motion / reduced transparency:** §5.6; grain layer additionally dropped under
`(prefers-reduced-transparency: reduce)` where supported (cheap media query, ignored elsewhere).

**Screen-reader sanity:** autoplay never mutates DOM focus or announces anything; advancing
is purely visual, so SR users are never yanked. `aria-live` intentionally omitted.

---

## 7. Technical Sketch

### 7.1 Props interface (page-facing API)

```ts
// Slideshow.astro frontmatter
import type { ImageMetadata } from "astro";

export interface StorySlide {
  src: ImageMetadata;
  alt: string;              // factual German image description
  kicker?: string;          // e.g. "01 · Hofladen"
  title?: string;           // product / moment name
  text?: string;            // one warm sentence
  focal?: string;           // object-position, default "center 45%"
}

export interface Props {
  slides: StorySlide[];
  label: string;                          // German aria-label for the region
  variant?: "product" | "hike";           // drives aspect-ratio + interval
  eyebrow?: string; subline?: string;     // editorial header (§3.3)
  season?: "sommer" | "winter";           // theme hook (default "sommer")
  lightbox?: boolean;                     // default true
}
```

Pages change minimally: `const images = [...]` becomes an array of objects; everything else
stays server-rendered in the page frontmatter.

### 7.2 DOM skeleton

```
<section role="region" aria-roledescription="Karussell" aria-label …>
  header.story-header (eyebrow + subline)
  <div class="deck-wrap">
    <button class="nav prev" aria-label>‹</button>
    <ul class="deck" tabindex="0">                <!-- overflow-x auto, snap -->
      <li class="slide" role="group" aria-roledescription="Folie" …>
        <figure>
          <Image src … loading eager|lazy />       <!-- + tint/grain overlays -->
          <figcaption class="slide-caption">kicker/title/text</figcaption>
          <button class="expand" aria-label="Bild groß anzeigen">⤢</button>
        </figure>
      </li> ×N
    </ul>
    <button class="nav next">›</button>
  </div>
  <div class="dots" role="group" aria-label="Direktnavigation"><button/> ×N</div>
</section>
<dialog class="lightbox"> … </dialog>
<script>  // ~120 lines vanilla, Astro-processed, deferred
```

Single-file component per repo convention (markup + scoped styles + co-located script).
JS caveats: Astro scopes styles — runtime-toggled state uses data attributes
(`[data-state="active"]`) rather than injected class names, avoiding `:global()` leaks.

### 7.3 Script responsibilities (~2–3 KB)

1. Set `data-js` root flag (unlock enhanced chrome, hide scrollbar).
2. IntersectionObserver → `.in-view` reveal; visibility gating for autoplay.
3. Active-slide tracking: `scrollend`/debounced `scroll` + slide midpoint check → toggles
   `is-active`, syncs dots, restarts KB/stagger animations.
4. Autoplay engine: `setTimeout` chain honouring §5.5 (hover/focus/interaction/hidden/offscreen).
5. Prev/next/dots click handlers → `scrollTo`.
6. Lightbox: open/close `<dialog>` (+ HTMLDialogElement fallback), focus restore,
   optional `document.startViewTransition` morph with just-in-time `view-transition-name`
   assignment (cleared afterwards — duplicate names abort silently).

### 7.4 CSS technique list

Foundation (universal): Flexbox track, `overflow-x: auto`, `scroll-snap-type: x mandatory`,
`scroll-snap-align: center`, `scroll-padding-inline`, `aspect-ratio`, `object-fit: cover`,
`clamp()`, custom properties, `env(safe-area-inset-*)`, `<dialog>` + `::backdrop`,
`backdrop-filter` (optional caption blur; universally safe incl. unprefixed Safari 18),
`:has()` for "any-slide-focused" states, keyframe transforms/opacity only.

Progressive enhancement (@supports-gated): `animation-timeline: view()/scroll(nearest)`
for the entry reveal and caption parallax; `@starting-style` for dialog fade-in;
same-document View Transitions for the lightbox morph; `sibling-index()` for stagger delays
(fallback: nth-child map for ≤ 8 slides). **Deliberately avoided as foundations:**
`::scroll-button()`, `::scroll-marker`, `scroll-marker-group`, anchor positioning (§8).

### 7.5 Effort estimate

| Phase | Content | Estimate |
|---|---|---|
| A | Markup, responsive deck, captions/scrim, editorial header | 0.5–1 d |
| B | JS: tracking, autoplay engine, arrows/dots, lightbox + fallbacks | 1 d |
| C | Motion polish: KB, staggers, reveal, VT morph, grain/tint | 0.5–1 d |
| D | Copy pass with owner, crop/focal tuning, QA matrix, CWV + axe audit | 0.5–1 d |
| **Total** | | **~3–4 dev-days** |

### 7.6 Risks & mitigations

1. **Crop damage on mixed-orientation product shots** → `focal` prop + visual sign-off per slide (§10).
2. **Ken Burns jank on low-end Androids during swipe** → KB only on active slide, paused during drag, transform-only, tested on throttled CPU.
3. **Astro style-scoping vs JS-toggled state** → data-attribute selectors contract (§7.2).
4. **`<dialog>` on iOS < 15.4** → div-overlay fallback branch (tiny).
5. **Autoplay perceived as intrusive** → conservative intervals, aggressive pause rules, permanent stop on first interaction, hard reduced-motion off-switch.
6. **Owner copy effort** → six + four sentences; provide drafts above, owner edits freely; captions degrade gracefully if fields omitted (image-only slide).

---

## 8. Browser-Support Reality Check (verified August 2026)

Sources: caniuse tables pulled 2026-08 (StatCounter July 2026 weighting), MDN, web.dev.

| Technique | Status Aug 2026 | Verdict for us |
|---|---|---|
| Scroll-snap (type/align/padding) | Universal, years stable | ✅ **Mobile-safe foundation** |
| `object-fit`, `aspect-ratio`, `clamp()`, custom props | Universal | ✅ Foundation |
| `<dialog>` / `::backdrop` | iOS 15.4+, all evergreen; else div fallback | ✅ Foundation (with fallback) |
| `backdrop-filter` (unprefixed) | All engines incl. Safari 18+ | ✅ Safe (cosmetic only anyway) |
| `:has()` | Baseline since Dec 2023 | ✅ Safe |
| Native lazy-loading `loading=lazy` | Universal (video/audio added Mar 2026) | ✅ Foundation |
| `@starting-style` | Cross-browser baseline reached 2026 (Chrome 117+, Safari 17+, Firefox 129+) | ✅ Safe w/ instant-show degradation |
| Same-document **View Transitions** | **Baseline Newly Available Oct 2025**: Chrome/Edge 111+, Safari/iOS 18+, Firefox 144+; ~90 % global | 🟡 Enhancement — feature-detect `document.startViewTransition`; fallback = instant open |
| Scroll-driven animations `animation-timeline: view()/scroll()` | Chrome/Edge 115+, **Safari/iOS 26.0+**, Firefox 157+; ~85 % global; MDN still "limited availability" | 🟡 Enhancement — gate with `@supports`; IO-based version is the shipped default |
| **`::scroll-button()` / `::scroll-marker` / `:target-current`** | **Chromium-only**: Chrome/Edge 135+, Opera 120+, Samsung Internet 29+. **Firefox ❌ (through 157)**, **Safari & iOS Safari ❌ (through 26.6!)**; ~69.75 % global | 🔴 **Never load-bearing.** This is precisely why the `feature/slideshow` prototype fails on iPhone Safari and Firefox — our arrows/dots are real HTML buttons everywhere |
| Anchor positioning | Chrome 125+, Safari 26+, Firefox 147+; ~81 % | ⛔ Not needed — plain absolute positioning suffices |
| `sibling-index()` | Chrome + Safari; Firefox in progress | 🟡 Optional stagger sugar with nth-child fallback |

**Net effect:** the experience floor (swipe deck + captions + scrims) ships on effectively
every 2019+ phone browser with zero guards; every flourish degrades to *less animation*,
never to *broken layout* or *unreachable content*.

---

## 9. Responsive & Device Notes (mobile-first recap)

- Portrait product shots dominate mobile reading → 4/5 frame; landscape hike vistas get 3/4
  on phones, opening to 16/9 on tablets/desktop where width is abundant.
- Safe areas: caption padding-bottom uses `max(1.25rem, env(safe-area-inset-bottom))`;
  lightbox controls respect inset-top (notch) and inset-bottom (home indicator).
- Touch targets: expand button 44×44 px, dots ≥ 24 px hit area (visual 8 px), nav buttons 48 px.
- `overscroll-behavior-x: contain` on the deck prevents vertical-page-scroll capture fights
  at the horizontal extremes while preserving iOS rubber-band feel inside the track.
- Hover-dependent UI (arrows) always mirrored by touch-reachable equivalents (swipe + dots).

---

## 10. Rollout Plan

1. **Branch** `feat/story-slideshow` off `main`; implement Phases A→C behind no flags (component-local).
2. **Copy workshop** with owner: finalise 6 product + 4 hike captions/alt texts (drafts §4.3).
3. **Visual QA:** per-slide `focal` review on iPhone SE, iPhone 15 Pro Max, Pixel 8, iPad, 1440 px desktop; check crops, scrim legibility over brightest photo regions, notch/home-indicator insets.
4. **Compat QA:** iOS Safari 18.x & 26.x, Android Chrome, Firefox Android, desktop tri-engine; verify no-JS mode (block JS), reduced-motion mode (OS toggles), keyboard-only run-through, screen reader spot-check (VoiceOver iOS).
5. **Performance gates:** Lighthouse mobile ≥ 95 perf / ≥ 100 a11y on both pages; CLS = 0; JS transfer ≤ 3 KB; compare LCP before/after (slide 1 eager).
6. **Ship:** swap imports on `produkte.astro` + `alpaka-wanderungen.astro` (props only — pages otherwise untouched), squash-merge PR titled `feat(website): immersive storytelling slideshow` with before/after clips attached.
7. **Fast-follow backlog:** seasonal hook wiring, caption-parallax enhancement, evaluate adopting native `::scroll-marker` as a Chromium bonus layer once Firefox/Safari land it (design unchanged — our dots would simply become the native ones).
