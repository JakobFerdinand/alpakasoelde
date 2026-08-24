# Design Concept 1 — Progressive-Enhancement Carousel

| | |
|---|---|
| **Status** | Draft for review |
| **Date** | 2026-08-24 |
| **Component** | `src/website/src/components/Slideshow.astro` |
| **Used on** | `/produkte` (6 product photos), `/alpaka-wanderungen` (4 hike photos) |
| **Replaces** | `main` crossfade version + `feature/slideshow` CSS-carousel prototype |
| **Scope** | Concept only — no implementation in this document |

---

## 1. Problem statement

### 1.1 Today (`main`)

The shipped `Slideshow.astro` is a fixed-height box (`24rem`, `32rem` ≥ 768px) with absolutely stacked slides, a bare `setInterval` opacity crossfade every 4 s, and `object-fit: contain` images on a cream plate. It has real problems beyond aesthetics:

- **No user control.** No arrows, no dots, no swipe surface — visitors cannot navigate; they can only wait.
- **Autoplay misbehaves.** The interval keeps firing when the tab is hidden, the section is scrolled off-screen, or the user is interacting; it never pauses and never stops. It also ignores `prefers-reduced-motion`.
- **A11y gaps.** No carousel semantics, no keyboard access, one generic alt text ("Alpaka auf der Alpakasölde Farm") for every photo, no live announcements.
- **Multi-instance fragility.** Hard-coded `id="slideshow"` plus `document.querySelectorAll('[image-slide]')` means a second instance on a page would double-drive both (and the id would collide). Today it happens to be safe because both pages mount exactly one — but the component doesn't guarantee that.
- **Fixed height, not intrinsic ratio.** Works, but reserves vertical space unrelated to the actual image ratio; on small phones the letterboxed 24rem box dominates the section.

### 1.2 The prototype (`feature/slideshow`) — why it fails in the real world

The owner's prototype (commit `16acbdf`) is genuinely good design: a scroll-snap carousel with browser-generated `::scroll-button(left/right)` arrows anchored beside the scroller, `::scroll-marker` dots with `:target-current` highlighting, and an autoplay driven purely by CSS — each card carries a hidden `<span class="snapper">` that is the *real* snap target; a keyframe animation teleports that span into the neighbouring card during the last 5 % of each cycle, and `scroll-behavior: smooth` makes the scroller glide after it. It looks great — **in Chrome and Edge ≥ 135 only.**

The failure is categorical, not cosmetic. As of August 2026:

- `::scroll-button()`, `::scroll-marker()`, `::scroll-marker-group` and `:target-current` are implemented **only in Chromium 135+** (Chrome/Edge, and recent Samsung Internet). **Safari/iOS Safari have not shipped them in any release through 26.6 (July 2026)**; Firefox ships them only behind a flag. Global support ≈ **70–75 %**, but that average hides the damage: support on **iOS — the majority of this site's traffic — is effectively 0 %**.
- Without those pseudo-elements the prototype renders as a naked horizontal strip: no arrows, no dots, no indication that anything scrolls. On iOS it degrades to "a row of pictures you must know to swipe."
- The prototype's **autoplay also has structural flaws** independent of support: the keyframed snapper animates unconditionally — CSS animations don't pause when the tab is hidden, when the carousel is off-screen, or while the user's finger is mid-drag. If the animation flips the snap target during a touch gesture, `x mandatory` snapping yanks the scroll out from under the user's finger. Hover/focus pauses exist but are desktop-only signals. There is no way to permanently stop it, and the reduced-motion block correctly kills it — the only guard that works.
- Its **anchor positioning** (`anchor-name` / `position-anchor` / `position-area`) for the buttons is now Baseline-newly-available (see §4) but only reached Safari in 26.0 and Firefox in 147 — iPhones on iOS 18.x, still a meaningful share of a mobile-heavy audience, get nothing.

**Conclusion:** the prototype bets the whole UX on features that are Chromium-exclusive today. The vision is right; the delivery vehicle is 18–24 months early.

### 1.3 The opportunity

Everything the prototype expresses can be delivered today, identically, with a thin standards-based baseline (native scroll-snap + three real `<button>` elements + ~50 lines of dependency-free vanilla JS) and a small set of `@supports`-gated enhancements. The fancy parts of the platform that ARE ready (scroll-snap, `overscroll-behavior`, `aspect-ratio`, `@starting-style`, `scrollend`, View Transitions, `color-mix`) get used immediately; the parts that aren't (`::scroll-button()` et al.) get a documented, reversible upgrade path.

---

## 2. Goals and non-goals

### Goals

1. **G1 — Universal parity.** Arrows, dot indicators, snap behaviour and autoplay work on iOS Safari 17+, Android Chrome, Firefox (desktop + Android), Samsung Internet, desktop Chrome/Edge/Safari. One visual result everywhere.
2. **G2 — Preserve the owner's vision.** Same look and feel as `feature/slideshow`: full-width snap cards, round gold arrows mid-height, dot row underneath, gentle autoplay that glides one card at a time.
3. **G3 — Zero-framework.** Inline vanilla TS/JS in the `.astro` component (bundled/hoisted by Astro), no client framework, no hydration, no carousel library. Repo rule: lean scripts, modern CSS first.
4. **G4 — Civilized autoplay.** Pauses on hover, focus, touch, off-screen, hidden tab; stops permanently after deliberate user interaction; fully disabled under `prefers-reduced-motion`; never fights an in-progress gesture.
5. **G5 — Accessible by construction.** Keyboard-operable (arrows/Home/End), German-labelled controls, live slide announcements, WCAG-AA contrast using brand tokens, reduced-motion honoured in CSS *and* JS.
6. **G6 — Zero layout shift.** Reserved aspect-ratio box; Astro `<Image />` with intrinsic dimensions; AVIF/WebP for free.
7. **G7 — Documented upgrade path.** A named phase in which the DOM controls are swapped for the owner's beloved native `::scroll-button()`/`::scroll-marker()` UI the moment those reach Baseline Newly Available — without redesigning the component.

### Non-goals

- No infinite/cloned-slide looping (rejected — §6.3).
- No lightbox/zoom, no captions overlay, no video slides (future concepts).
- No shared npm carousel dependency; no dashboard changes; no API changes.
- No retro-fit of the marketing site's other static image sections (out of scope).
- Not making the pseudo-element carousel work via polyfills (the available polyfills are heavy, imperfect and contradict the lean-script rule).

---

## 3. Browser support matrix (verified August 2026)

Status compiled from official release notes, MDN/web-features and caniuse (sources §15). Percentages are approximate global page-share capable of using the feature.

| Feature | Chrome/Edge | Safari / iOS Safari | Firefox | Samsung Internet (Android) | Baseline status (Aug 2026) | ~Global support |
|---|---|---|---|---|---|---|
| CSS scroll-snap (type/align/stop) | 69+ | 11+ | 81+ (older partial) | 10+ | Widely available | ~97 % |
| `overscroll-behavior` | 63+ | 16+ (scrollers) | 59+ | 8.2+ | Widely available | ~95 % |
| `aspect-ratio` | 88+ | 15+ | 89+ | 15+ | Widely available | ~96 % |
| `::scroll-button()` | **135+** (Mar 2025) | **not shipped** (≤ 26.6, Jul 2026) | flag only | ~29/30 only (engine-dependent, fleet lags) | Limited availability | **~70–75 %** |
| `::scroll-marker()` / `::scroll-marker-group` / `:target-current` | **135+** | **not shipped** | flag only | ~29/30 only | Limited availability | **~70–75 %** |
| CSS anchor positioning (`anchor-name`, `position-anchor`, `position-area`) | 125+ (May 2024) | 26.0+ (Sep 2025) | 147+ (enabled by default, Jan 2026) | 27+ | Newly available (since Jan 2026) | ~91 % |
| `scrollend` event | 114+ | 26.2+ (Dec 2025) | 109+ | ~28+ | Newly available (since Dec 2025) | ~93 % |
| Same-document View Transitions (`document.startViewTransition`) | 111+ | 18.0+ (Sep 2024) | 144+ (Oct 2025) | ~28+ | Newly available (since Oct 2025) | ~93–95 % |
| `@starting-style` (+ `transition-behavior: allow-discrete`) | 117+ | 17.5+ | 129+ | ~28+ | Newly available (since Aug 2024) | ~90 % |
| `animation-timeline: scroll()` / `view()` | 115+ (Jul 2023) | 26.0+ (Sep 2025) | **not shipped** (flag only, still true mid-2026) | 23+ | Limited availability | ~80 % |
| `prefers-reduced-motion` | 74+ | 10.2+ | 63+ | 8.2+ | Widely available | ~97 % |
| `color-mix()` | 111+ | 16.2+ | 113+ | 23+ | Widely available | ~95 % |

Notes and caveats:

- **Carousel pseudo-elements:** Chrome's own launch post (Chrome 135, March 2025) already listed Firefox and Safari as "not supported"; nothing changed through Safari 26.6 (July 2026 release notes contain no carousel features) and Firefox 154 (still flag-gated). The web-features explorer continues to classify `::scroll-button` as *Limited availability*. Some secondary blogs claim Safari support since "18.2" — that is wrong; trust the release notes.
- **Samsung Internet:** SI 28 (Apr 2025) is based on Chromium 130 → **no** carousel pseudo-elements. SI 29 (Oct 2025) / SI 30 (rolling out spring 2026, M143 on Windows builds) almost certainly carry a Chromium ≥ 140 core → features present **on paper**, but Galaxy fleet adoption trails by months-to-years because updates ride OS/One UI cycles. Treat Samsung conservatively: assume the base layer must carry the experience.
- **Anchor positioning** is *nearly* safe (91 %) but the missing 9 % skews exactly toward older iPhones — i.e., this site's core audience. Fine as an enhancement; wrong as a foundation.
- **`scrollend`** needs a tiny debounced-`scroll` fallback for Safari 26.0/26.1 and older (≈ 5 % and shrinking fast).

**Decision input:** the only features the prototype truly depends on are stuck at "Chromium-only." Every other ingredient is Baseline-newly-or-widely available today. That asymmetry dictates the architecture in §5.

---

## 4. Architecture — layered enhancement strategy

```
┌────────────────────────────────────────────────────────────────┐
│  Layer 3 (Phase 3, deferred): native CSS carousel takeover     │
│  @supports selector(::scroll-button(*)) { … }                  │
│  Owner's pseudo-element UI replaces DOM controls; JS shrinks   │
├────────────────────────────────────────────────────────────────┤
│  Layer 2 (ship now): free polish where supported               │
│  @starting-style entrance, scrollend-driven dot sync (with     │
│  debounce fallback), optional scroll()-driven progress hairline│
├────────────────────────────────────────────────────────────────┤
│  Layer 1 (ship now): THE experience, everywhere                │
│  Native scroll-snap scroller + real <button> arrows/dots +     │
│  ~50-line vanilla controller (nav, autoplay, a11y, gestures)   │
├────────────────────────────────────────────────────────────────┤
│  Layer 0 (no-JS / ancient browsers): content is reachable      │
│  Horizontal swipe/scroll scroller, all images visible,         │
│  scrollbar visible as affordance; controls render disabled     │
└────────────────────────────────────────────────────────────────┘
```

### 4.1 Layer 0–1: the baseline (this is the product)

Semantic, server-rendered markup; the scroller is a real overflow container with `scroll-snap-type: x mandatory`; three real `<button>`s (prev, next, dots-as-buttons) rendered in SSR HTML; one small inline module script wires behaviour. With JavaScript broken/disabled the images remain fully browsable by touch/scrollbar — the buttons simply do nothing, which is why they are real buttons and not injected by JS (also keeps the interaction latency at zero on first paint).

### 4.2 Layer 2: progressive polish, `@supports`-gated, zero-risk

- **Dot sync:** prefer `scrollend` on the scroller; fall back to a passive `scroll` listener with a 120 ms trailing debounce where `'onscrollend' in Element.prototype` is false (Safari ≤ 26.1). Feature-detected at runtime, one branch.
- **Entrance micro-animation** of dots/arrows via `@starting-style` (supported ~90 %, silently absent elsewhere).
- **Optional** reading-progress hairline under the carousel via `animation-timeline: scroll()` inside `@supports (animation-timeline: scroll())` — pure sugar; Chromium + Safari 26 only, invisible elsewhere. Cheap delight, no dependency.

### 4.3 Layer 3: the owner's native carousel, preserved as a documented endgame

The `feature/slideshow` pseudo-element rules (buttons, markers, `:target-current`, anchor placement) are kept **verbatim in the concept as a future `@supports` block**. When `::scroll-button()`/`::scroll-marker()` reach Baseline Newly Available (realistically late 2026 → newly, widely ~2028), we flip one switch: hide the DOM controls inside the same `@supports` query and delete most of the JS (autoplay becomes the *only* remaining script, unless native cyclical scrolling lands first — it is explicitly on the Chromium roadmap). Nothing about the Layer-1 markup needs to change: the scroller element and cards stay exactly as they are; pseudo-elements attach to the same nodes.

### 4.4 Decision: keep the pseudo-element approach as enhancement, or rebuild on standard controls?

**Recommendation: rebuild the production baseline on standard DOM controls now; archive the pseudo-element UI as the Phase-3 enhancement. Do not run both control systems simultaneously.**

Trade-off analysis:

| Criterion | A) Ship dual-path today (pseudo-elements in Chrome, DOM elsewhere) | B) Single DOM path now, native later (chosen) |
|---|---|---|
| Visual consistency | Two subtly different control implementations to keep in sync forever | Pixel-identical everywhere; one screenshot in PR review |
| QA surface | Every change tested twice (Chrome-native + DOM-fallback), plus `@supports` interactions | One matrix |
| A11y | Native markers/buttons are excellent, but diverge from DOM path semantics (tablist-like `<a>` markers vs. buttons) — screen-reader users get different experiences per browser | One semantic model, one German label set |
| Effort | +0.5–1 day upfront, permanent tax | Baseline effort only |
| Honours owner's vision | Yes, immediately — in Chrome only | Yes, everywhere, visually equivalent; native version arrives automatically in Phase 3 |
| Risk of `@supports` false-positive bugs (partial impls, Samsung oddities) | Real — Samsung's engine lag makes partial-support combinations likely | Minimal — enhancement gates are additive sugar only |
| Reversibility | Poor (entangled) | Excellent (Phase 3 is additive deletion) |

Why B wins for *this* repository: mobile-heavy traffic means the "enhanced" path would serve a minority of visitors while defining the design bar the majority must be measured against anyway; the DOM path replicates the prototype's look with ordinary flexbox (no anchor positioning needed — the arrows are positioned by the component grid, which is simpler than the prototype's `position-area` dance); and the repo rules favour one lean script over parallel mechanisms. The pseudo-element work is not thrown away — it is scheduled (§4.3), and the scroller markup is deliberately designed so adopting it later is subtraction, not surgery.

---

## 5. Component anatomy (markup outline)

```htmlc
<!-- SSR output of <Slideshow /> -->
<section
  class="slideshow"
  role="region"
  aria-roledescription="Karussell"
  aria-label={label}                     <!-- z. B. „Produktfotos“ -->
  data-autoplay={autoplay ? "" : undefined}
  data-interval={interval}
  style={`--slides:${images.length}; --slide-ratio:${aspect};`}
>
  <div class="slideshow__frame">
    <ul class="slideshow__track" tabindex="0">
      {images.map((img, i) => (
        <li class="slideshow__slide">
          <Image
            src={img.src}
            alt={img.alt}
            width={img.src.width} height={img.src.height}  <!-- from ImageMetadata -->
            loading={i === 0 ? "eager" : "lazy"}
            decoding="async"
            sizes="(min-width: 768px) 768px, 100vw"
          />
        </li>
      ))}
    </ul>

    <!-- Real controls, SSR-rendered, styled to match the prototype -->
    <button class="slideshow__arrow" type="button" data-dir="-1"
            aria-label="Vorheriges Bild" aria-controls="…track-id">
      <!-- lucide chevron-left as inline SVG (site is .astro-only; no @lucide/svelte here) -->
    </button>
    <button class="slideshow__arrow" type="button" data-dir="1"
            aria-label="Nächstes Bild" aria-controls="…track-id">
      <!-- chevron-right -->
    </button>
  </div>

  <div class="slideshow__footer">
    <div class="slideshow__dots" role="group" aria-label="Bildauswahl">
      {images.map((_, i) => (
        <button type="button" class="slideshow__dot"
                aria-label={`Zu Bild ${i + 1}`}
                aria-current={i === 0 ? "true" : undefined}></button>
      ))}
    </div>
    <p class="visually-hidden" aria-live="polite" data-slideshow-status></p>
  </div>
</section>
```

Details that matter:

- **Unique ids generated** via `crypto.randomUUID()` slice or a module-level counter in frontmatter (fixes `main`'s `id="slideshow"` collision; enables multi-instance safety).
- **The scroller is the `<ul>`** with `overflow-x:auto; scroll-snap-type:x mandatory; overscroll-behavior-x:contain`. Cards keep `scroll-snap-align:center; scroll-snap-stop:always`.
- **Arrows flank the frame** via component grid (`grid-template-columns: auto 1fr auto` on wide viewports; overlaid half-inset on ≥1024px to mirror the prototype's side placement; hidden on <768px where swiping is primary — dots remain).
- **Dots are buttons, not links**: no URL fragments (the prototype's markers behave like `<a>`; fragments pollute history and scroll the page). `aria-current="true"` marks the active dot.
- **Counter/live region** is visually hidden; dots convey position visually.

### Props interface draft

```ts
import type { ImageMetadata } from "astro";

export interface SlideshowImage {
  src: ImageMetadata;   // imported asset — Astro gives width/height/format
  alt: string;          // REQUIRED, German, per-photo (fixes main's generic alt)
}

export interface Props {
  images: SlideshowImage[];
  /** sr-only heading / region label, e.g. "Produktfotos" */
  label?: string;              // default: "Fotogalerie"
  /** autoplay on load (never under prefers-reduced-motion) */
  autoplay?: boolean;          // default: true
  /** dwell time per slide in ms */
  interval?: number;           // default: 5000
  /** reserved box ratio; "auto" = tallest supplied image */
  aspect?: "4/3" | "3/2" | "1/1" | "16/9" | "auto"; // default: "4/3"
}
```

Both call sites migrate mechanically: `images={images}` stays; wrap entries as `{ src: Strickgabel, alt: "Strickgabel für Zauberwolle aus Alpakawolle" }` etc. (per-photo German alts are a content task for the owner — placeholder texts ship meanwhile).

---

## 6. Interaction specification

### 6.1 Touch (primary audience)

- **Native momentum scrolling stays native.** We do not intercept touch events for panning; the scroller is a plain overflow container, so iOS inertia, finger-tracking and rubber-banding behave exactly like the rest of the platform. (History note: `-webkit-overflow-scrolling: touch` is obsolete — iOS has had momentum scrolling in web content by default since iOS 13; adding it today is dead weight.)
- **Page-scroll containment:** `overscroll-behavior-x: contain` on the track stops the horizontal flick from chaining into the page's vertical scroll/back-swipe at the ends. Vertical page scrolling *through* the carousel stays possible because the scroller only claims the x-axis (`touch-action: pan-x pan-y` left explicit).
- **Snap discipline:** `scroll-snap-type: x mandatory` + `scroll-snap-stop: always` per slide guarantees one-photo-per-flick; fast swipes can't skip past photos unnoticed (important for the 4-image gallery where every photo matters).
- **Rubber-band at boundaries:** bounded carousels hit iOS edge elasticity naturally; that is *desired* feedback (§6.3 explains why we don't fight it with clones).
- **Gesture detection for autoplay:** `pointerdown` on the section sets `gestureActive` until `pointerup/pointercancel` + `scrollend` (or debounce timeout); the autoplay timer refuses to fire while set, and the pending tick is discarded rather than queued — the carousel never moves under a finger.
- Tap targets: dots ≥ 44 × 44 px effective hit area (visual dot 10–12 px on a padded button), arrows 48 px.

### 6.2 Desktop pointer

- Arrows click-navigate with smooth `scrollTo`. Disabled state (`disabled` attribute + reduced opacity) at either end — mirroring the native `::scroll-button():disabled` behaviour we'll inherit in Phase 3.
- Trackpad horizontal swipe and Shift+wheel work natively; vertical wheel is intentionally **not** hijacked (no wheel-to-horizontal translation — that fights browser UX and INP; the arrows cover discoverability).
- Optional drag-to-scroll with mouse is explicitly **out** for v1: it duplicates native behaviour poorly and adds the largest chunk of JS for the smallest audience.

### 6.3 Keyboard

On the scroller (`tabindex="0"`, labelled via the region):

| Key | Action |
|---|---|
| `→` / `↓` | Next slide (`preventDefault`) |
| `←` / `↑` | Previous slide |
| `Home` | First slide |
| `End` | Last slide |
| `Tab` | Exits to arrows → dots (natural order); arrows/dots are individually focusable standard buttons |

Rationale: roving-tabindex/tablist patterns buy nothing for six inert photos and complicate the Phase-3 swap (native markers implement their own focusgroup). Arrow keys on a focused scroller + plain buttons is the WAI-APG-compatible minimum that survives every layer.

### 6.4 Loop strategy — decided: bounded, with an autoplay cut

| Strategy | Pros | Cons | Verdict |
|---|---|---|---|
| Cloned-slide infinite (clone head/tail, jump without transition) | Seamless endless swipe, familiar from Swiper et al. | Duplicated `<Image>` payload (LCP/memory), wrong "Bild X von N" counts, breaks `:target-current` marker mapping later, clone-seam rubber-band glitches on iOS, extra JS for seam masking | **Rejected** — cost >> benefit for 4–6 photos |
| Wrap-around jump (next at end ⇒ animated scroll to start) | Endless feel without clones | Long multi-slide whoosh on 6 images reads as breakage; disorienting; fights snap physics | **Rejected** for manual nav |
| **Bounded** (hard stops, arrows disable at ends) | Zero JS for edges, honest spatial model, native rubber-band as end-feedback, `:disabled` maps 1:1 to future `::scroll-button():disabled`, trivially accessible | No endless browsing | **Chosen** for user navigation |
| Autoplay cut | At the end, autoplay performs an **instant** (`behavior:"auto"`) reset to slide 1 and resumes gliding — the same perceptual move as the prototype's `tostart` keyframe (99 %→100 % teleport), just expressed in JS | Brief cut instead of reverse sweep | **Chosen** for autoplay only |

This hybrid keeps the owner's "always moving forward" slideshow rhythm while manual exploration stays predictable and loop-free.

---

## 7. Autoplay specification

Single `setTimeout` chain (not `setInterval` — avoids pile-ups when a tick is skipped):

```text
state: { index, stopped, paused, gestureActive, timer }

canRun()  = autoplay && !stopped && !paused && !gestureActive
           && inViewport && !document.hidden && !reducedMotion

tick():
  if (!canRun()) return                 // re-armed by whatever resumed us
  if (atEnd) jumpInstantTo(0)           // the “cut” (§6.4)
  else scrollToSlide(index + 1, smooth)
  schedule(tick, interval)

pause conditions (paused=true while any holds):
  - IntersectionObserver: section < 50 % visible        (off-screen)
  - document.visibilitychange: tab hidden               (background)
  - pointerenter / focusin on section                   (desktop hover/keyboard)
  - gestureActive (finger down until scroll settles)

permanent stop (stopped=true, never resumes, timer cleared):
  - any click/tap on arrows or dots
  - a completed *deliberate* scroll gesture on the track
    (pointerdown→scrollend with displacement > one slide / 3)
  - keyboard navigation

reduced motion:
  - matchMedia('(prefers-reduced-motion: reduce)').matches
    ⇒ autoplay never starts at all; programmatic moves use
      behavior:'auto'; CSS transitions/animations disabled (§8)
  - listen for change events (user toggles mid-session)
```

Design notes:

- **Interval 5000 ms** (up from the crossfade's 4 s): a scroll animation consumes ~400–600 ms of perceived time; 5 s gives each photo a fair dwell without feeling stalled. Configurable per instance via props.
- **Never mid-gesture:** ticks are gated by `gestureActive`, and a tick that would land while a *programmatic* smooth scroll is still running is suppressed by tracking `scrollend` (debounce fallback on Safari ≤ 26.1). Worst case one tick is dropped — correct behaviour.
- **Battery/data respect:** pausing off-screen and on hidden tabs fixes the worst offence of both predecessors (the crossfade ran forever; the CSS snapper likewise).
- **Announcements stay silent during autoplay** — only user-initiated navigation writes to the live region, so screen readers don't chatter every five seconds.

---

## 8. Accessibility specification

- **Semantics:** `role="region"` + `aria-roledescription="Karussell"` + German `aria-label` (from the `label` prop, e.g. „Produktfotos“, „Eindrücke von den Alpaka-Wanderungen”). WAI-ARIA APG carousel pattern, minimal viable subset.
- **Labels (exact strings):**
  - Prev/next buttons: `aria-label="Vorheriges Bild"` / `"Nächstes Bild"`
  - Dots: `aria-label="Zu Bild 1"` … `"Zu Bild 6"`, active dot additionally `aria-current="true"`
  - Live region: `aria-live="polite"`, announces `„Bild 2 von 6“` after user-initiated navigation only.
- **Focus visibility:** `:focus-visible` gets `outline: 3px solid var(--taubenblau); outline-offset: 2px` on cream backgrounds (contrast 6.5:1 ✓); over photos the outline switches to cream via the scrimmed context (below). Never `outline: none`.
- **Contrast (WCAG 1.4.3 / 1.4.11), computed from brand tokens:**

| Pair | Ratio | AA verdict |
|---|---|---|
| Gold `--bluetenhonig #e1b14a` on cream `--schurwolle #fbf7ed` | **1.85 : 1** | ✗ fails everything — the prototype's gold-arrows-on-page look is *not* shippable as-is |
| Cream glyph on `--taubenblau #4b5b73` button | **6.5 : 1** | ✓ AA text & graphics |
| Gold accent on `--taubenblau` | **3.5 : 1** | ✓ graphics/large text only |
| `--himmelblau #8da5d3` inactive dot on cream | **2.3 : 1** | ✗ fails 3:1 non-text — do not use raw |
| `--taubenblau` dot on cream | **6.5 : 1** | ✓ |

  **Resulting control styling (preserves the prototype's *feel*, legally):**
  - Arrows: 56 px circles, solid `var(--taubenblau)` fill, **cream** chevron glyph (SVG stroke), 2 px gold ring on hover — gold becomes the accent, not the fill. Over photos, the same solid token circle needs no scrim; where a translucent variant is wanted (overlay style ≥1024px), use `background: color-mix(in srgb, var(--taubenblau) 85%, transparent)` — worst case over a pure-white photo the blend still yields ≈ 4.1:1 against the cream glyph, above the 3:1 graphics floor.
  - Dots: inactive = 8 px `var(--taubenblau)`; active = 12 px `var(--bluetenhonig)` with 2 px `var(--taubenblau)` border (border alone satisfies non-text contrast; the gold centre is decorative reinforcement) plus `transform: scale(1.15)`.
  - Any caption/scrim gradient over photos: `linear-gradient(to top, color-mix(in srgb, var(--taubenblau) 90%, transparent), transparent)` keeps cream text ≥ 4:1 even over white imagery.
- **Reduced motion:** CSS side — `@media (prefers-reduced-motion: reduce) { .slideshow *, .slideshow::before { transition: none !important; animation: none !important; } }`; JS side — autoplay hard-disabled, `behavior:"auto"` jumps (§7). Double enforcement so either layer failing alone is harmless.
- **Touch target size** ≥ 44 px (WCAG 2.5.8 / platform norms) — see §6.1.
- **Screen-reader slide content:** each `<li>` is plain flowing content (image + alt), so SR users browse the gallery as a list; the live-region counter is convenience, not the only path.

---

## 9. Performance & robustness plan

- **CLS = 0 by construction:** the frame reserves `aspect-ratio: var(--slide-ratio)` (default 4/3 ≈ the source photos' 800×600); `<Image>` emits intrinsic `width/height` from `ImageMetadata`. No JS-measured heights ever. `aspect: "auto"` mode falls back to the largest child's ratio computed at build time in frontmatter (still SSR-static).
- **Image delivery:** Astro `<Image />` → AVIF/WebP + responsive `srcset/sizes` automatically. First slide `loading="eager"` (likely in initial viewport on mobile at these page positions), rest `lazy`; all `decoding="async"`. Both current usages sit below the hero, so LCP is untouched by the component.
- **Script budget:** one inline `<script>` (Astro bundles/hoists as `type="module"`, deduplicated per page). Target ≤ 1.5 KB min+gzip. Zero dependencies, no framework hydration (repo rule).
- **SSR-safe:** no top-level `window`/`document` access outside the module's `for (const root of document.querySelectorAll('[data-slideshow]'))` init; component renders identically with JS disabled (Layer 0).
- **Multi-instance safe:** all queries scoped to the component root; ids generated uniquely; state closed over per instance (fixes `main`'s global-query bug).
- **Resize / orientation change:** `mandatory` snap re-settles natively; a `resize` listener (rAF-throttled) re-syncs the dot index to the snapped slide via the same routine as `scrollend`. No layout recalculation of our own is needed because nothing is measured.
- **Event hygiene:** `{ passive: true }` on scroll listeners; `IntersectionObserver` + `matchMedia` cleaned up implicitly with the page; no timers leak because the chain dies on `stopped` and on `disconnected` guard.
- **Robustness fallbacks:** `scrollend` → debounced `scroll`; `color-mix()` → solid-token declarations declared first (graceful override); View Transitions not required by any critical path (optional wrap-cut polish only, guarded by `'startViewTransition' in document`).

---

## 10. Implementation sketch

### 10.1 CSS organization (scoped `<style>` in the component)

```css
/* ── Layer 1: universal base ─────────────────────────────── */
.slideshow { /* grid: frame + footer; tokens, aspect-ratio box */ }
.slideshow__track {
  display: flex; gap: var(--gap);
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  overscroll-behavior-x: contain;
  scroll-behavior: smooth;             /* JS overrides per-move */
  scrollbar-width: none;               /* dots replace it */
}
.slideshow__slide { flex: 0 0 100%; scroll-snap-align: center; scroll-snap-stop: always; }
/* arrows: grid-placed circles, taubenblau/cream, disabled states */
/* dots: flex row, states via [aria-current] */

@media (prefers-reduced-motion: reduce) { /* kill transitions/anims */ }

/* ── Layer 2: gated polish ───────────────────────────────── */
@supports (transition-behavior: allow-discrete) and (top: calc(1px * sign(1))) {
  /* @starting-style entrance for dots/arrows (≈90 %) */
}
@supports (animation-timeline: scroll()) {
  /* optional progress hairline under the frame (Chromium + Safari 26) */
}

/* ── Layer 3: native takeover — DO NOT SHIP UNTIL BASELINE (§4.3) ──
@supports selector(::scroll-button(*)) and selector(::scroll-marker) {
  .slideshow__arrow, .slideshow__dots { display: none; }
  .slideshow__track { anchor-name: --ss; scroll-marker-group: after; }
  .slideshow__track::scroll-button(left)  { content: "‹" / "Vorheriges Bild"; position-anchor: --ss; … }
  .slideshow__slide::scroll-marker { content: ""; … }
  .slideshow__slide::scroll-marker:target-current { … }
}
*/
```

### 10.2 Script outline (inline `<script>`, estimated ~55–70 readable lines / ~1.2 KB min)

```text
const REDUCED = matchMedia('(prefers-reduced-motion: reduce)');
for (const root of document.querySelectorAll('.slideshow')) init(root);

function init(root) {
  track, slides, prevBtn, nextBtn, dots[], status, opts{interval, autoplay}
  state { i:0, stopped:false, paused:false, gesture:false, timer }

  goTo(n, {smooth, announce})   // clamp, scrollTo({left: slide.offsetLeft}), sync()
  sync()                        // aria-current on dots, disabled on arrows, status text
  next()/prev()                 // goTo(i±1, {announce:true})

  events:
    root 'click' (delegated)  → arrow/dot ⇒ stop(); goTo(target)
    track 'keydown'           → Arrow/Home/End ⇒ stop(); goTo()
    track 'scrollend'         → syncIndexFromScroll()   [else debounced 'scroll']
    track 'pointerdown/up', 'wheel' → gesture flag; settled gesture ⇒ stop()
    root 'pointerenter/leave', 'focusin/focusout'   → pause/resume
    new IntersectionObserver  → inView ⇒ pause/resume, arm()
    document 'visibilitychange'
    REDUCED.addEventListener('change')

  arm(): if canRun() timer = setTimeout(tick, interval)
  tick(): if (!canRun()) return; atEnd ? jump(0) : goTo(i+1,{smooth}); arm()
}
```

Line-count reality check: the above compresses to roughly 55 lines of tight vanilla TS; with comments and the debounce fallback expect ~80. Still comfortably within "lean script" territory and smaller than any carousel option on npm.

### 10.3 Effort estimate

| Task | Estimate |
|---|---|
| Markup + base CSS (Layer 1) | 0.5 d |
| Controller script (nav, autoplay, a11y, guards) | 0.5 d |
| Layer-2 polish + reduced-motion/QA passes (iOS Safari, Android Chrome, SI, FF, desktop) | 0.5 d |
| Page migrations (props/alts), Playwright smoke assertions, PR material | 0.5 d |
| **Total** | **≈ 2 dev-days** |
| Phase 3 native swap (later, when Baseline) | ≈ 0.5 d |

### 10.4 Risks & mitigations

| Risk | Mitigation |
|---|---|
| iOS edge-cases in `overscroll-behavior` on older iOS 16/17 | Containment is progressive; worst case is today's status quo (page chains scroll). No functional breakage. |
| Samsung Internet fleet on old engines | Base layer is engine-agnostic; nothing critical behind a gate. |
| `scrollend` gap (Safari ≤ 26.1) | Debounced `scroll` fallback, feature-detected. |
| Autoplay annoyance regression vs. prototype | Permanent-stop rule + pause matrix (§7); owner sign-off on 5 s dwell. |
| Per-photo alts missing at migration | Placeholder German alts derived from filenames; content task ticketed for owner. |
| Spec drift of Overflow-5 pseudos before Phase 3 | Enhancement is quarantined in one commented `@supports` block; adoption re-reviewed against the spec at that time. |

---

## 11. Degradation matrix

| Feature / mechanism | Chrome desktop | iOS Safari | Firefox | Samsung Internet | Fallback behaviour |
|---|---|---|---|---|---|
| Scroll-snap carousel | ✓ | ✓ | ✓ | ✓ | — (base) |
| DOM arrows + dots + JS controller | ✓ | ✓ | ✓ | ✓ | Without JS: scroller still swipe/scrollable; controls idle |
| `overscroll-behavior-x: contain` | ✓ | ✓ 16+ | ✓ | ✓ | Older iOS: page may chain-scroll at ends (cosmetic) |
| `aspect-ratio` reservation | ✓ | ✓ 15+ | ✓ | ✓ | Ancient: brief reflow after image metadata (CLS > 0 only there) |
| `@starting-style` entrance | ✓ 117+ | ✓ 17.5+ | ✓ 129+ | ~✓ | Controls simply appear without fade |
| `scrollend` sync | ✓ 114+ | ✓ 26.2+ | ✓ 109+ | ~✓ | Debounced `scroll` fallback everywhere else |
| `animation-timeline: scroll()` progress hairline | ✓ | ✓ 26+ | ✗ (flag only, Aug 2026) | ✓ | Hidden — decorative only |
| Same-document View Transition on autoplay cut | ✓ 111+ | ✓ 18+ | ✓ 144+ | ~✓ | Instant cut (already the designed behaviour) |
| `::scroll-button()` / `::scroll-marker()` / `:target-current` (Phase 3) | ✓ 135+ | **✗ until Apple ships** | **✗ until flag lifts** | partial (v29+) | DOM controls remain — Phase 3 never ships before Baseline |
| Anchor positioning (Phase 3 button placement) | ✓ 125+ | ✓ 26+ | ✓ 147+ | ✓ 27+ | Phase 3 gated together with pseudo-elements; DOM grid layout unaffected |
| `prefers-reduced-motion` handling | ✓ | ✓ | ✓ | ✓ | — (base) |

Reading: every column delivers the complete product; only decorative sugar varies.

---

## 12. Rollout & migration plan

### 12.1 Branch strategy

`feat/slideshow-v2` off `main` (the prototype branch stays frozen as reference; its CSS is quoted, not merged).

### 12.2 Migrating from `main` (crossfade version)

1. Replace the component body wholesale; keep the exported component name and directory.
2. **Breaking prop change (internal):** `images: any[]` → `SlideshowImage[]` (`{src, alt}`). Update both pages in the same commit — `produkte.astro` (6 entries) and `alpaka-wanderungen.astro` (4 entries); derive placeholder alts from filenames.
3. Delete: absolute stacking, `.hidden/.visible` classes, `setInterval` block, hard-coded `id`. Net effect: similar byte footprint, dramatically more capability.
4. Behavioural change to communicate in the PR: crossfade-in-place → swipeable snap deck (screenshots + a short clip attached per repo guidelines).

### 12.3 Migrating from `feature/slideshow` (prototype)

Concept-by-concept mapping:

| Prototype element | Fate in v2 |
|---|---|
| `.carousel` flex + snap + gap + `--count` | Kept (as `.slideshow__track`; `--slides` retained for potential styling) |
| `::scroll-button(left/right)` + anchor placement | Moved verbatim into the commented Phase-3 `@supports` block; replaced by grid-placed real buttons with identical visuals (token-fixed contrast) |
| `::scroll-marker` + `:target-current` dots | Same treatment; replaced by `[aria-current]` dots |
| `.snapper` spans + `tonext/tostart/snap` keyframes | Deleted; autoplay responsibility moves to the JS stepper implementing the same forward-glide-and-cut rhythm with proper pausing |
| `scroll-marker-group: after` | Phase-3 block |
| Reduced-motion media block | Kept, extended to JS side |
| Card sizing / 500 px breakpoint | Kept (full-width slide on mobile, centred card on desktop per page context) |

### 12.4 Verification checklist before merge

- `pnpm run build` (runs `astro check`) in `src/website` — green.
- Playwright smoke (existing harness): arrows navigate, dots reflect state, `aria-current` moves, keyboard arrows/Home/End work, autoplay stops permanently after interaction, reduced-motion context disables autoplay.
- Manual device pass: iPhone (iOS 17 or 18 **and** 26), Android Chrome, Samsung Internet (an *old* Galaxy build if obtainable), Firefox desktop + Android, desktop Chrome/Safari/Firefox.
- Lighthouse: CLS = 0 on `/produkte` and `/alpaka-wanderungen`; no LCP regression.
- `requests.http`/APIs untouched; no infrastructure impact.

### 12.5 Phase 3 trigger (documented, not scheduled)

When `::scroll-button()`/`::scroll-marker()` appear in stable Safari **and** non-flagged Firefox (web-features flips the carousel feature to *Newly available*): uncomment the Layer-3 block, hide DOM controls inside it, delete the corresponding controller branches, re-run the checklist. Estimated 0.5 day. Watch items: Chromium's planned native cyclical scrolling (would eliminate the autoplay cut) and "bring your own elements" for markers (would let us keep German-labelled custom dots natively).

---

## 13. Open questions for the owner

1. Autoplay dwell: 5 s proposed (was 4 s crossfade / 2 s snapper-cycle). Confirm.
2. Arrows on mobile: hidden below 768 px (recommend) or always visible?
3. Final German alt texts per photo (placeholders will ship).
4. Should `/produkte` eventually show product *cards* (name + price) instead of bare photos? The anatomy leaves room for a caption slot, but that is a separate concept.

---

## 14. Summary of the recommendation

Ship a **single-path, standards-based scroll-snap carousel** with real German-labelled `<button>` controls and a ~60-line vanilla controller that reproduces the prototype's look and autoplay rhythm with correct pausing/stopping semantics; gate genuinely-ready platform sugar (`@starting-style`, `scrollend`, optional scroll-driven progress) behind `@supports`; preserve the owner's `::scroll-button()`/`::scroll-marker()` implementation as a prepared Phase-3 `@supports` takeover that activates when the features reach Baseline Newly Available. Bounded navigation for users, cut-to-start wrap for autoplay. Zero CLS, zero dependencies, zero framework — and identical behaviour for the iPhone-and-Firefox majority that the prototype currently excludes.

---

## 15. Sources (accessed August 2026)

- Chrome for Developers — *Carousels with CSS* (Mar 2025): Chrome 135 ship post; explicit "Firefox: not supported / Safari: not supported" tables; accessibility model of scroll buttons/markers.
- MDN — `::scroll-button()`, `::scroll-marker`, *Creating CSS carousels* guide; web-features explorer entry *scroll-buttons*: **Limited availability**, usage ≈ 0.005 % of page loads (feature young).
- Apple Safari release notes 26.0 (Sep 2025: CSS Anchor Positioning, Scroll-driven Animations), 26.2 (Dec 2025: **scrollend**), 26.4 (Mar 2026), 26.5 (May 2026), 26.6 (Jul 2026) — **no carousel pseudo-elements in any release**.
- MDN — Firefox 147 release notes (Jan 2026): CSS anchor positioning enabled by default; Firefox 144 (Oct 2025): same-document View Transitions → web.dev *Baseline Newly available* post (Oct 14 2025).
- InfoQ (Apr 2026) / MDN — `scrollend` Baseline Newly available since Dec 2025 (Chrome 114, Firefox 109, Safari 26.2).
- caniuse — `@starting-style`: global support 90.65 % (data snapshot Mar 2026); Chrome 117 / Firefox 129 / Safari 17.5.
- Mozilla Connect + web-platform-tests/interop#1033 — CSS scroll-driven animations still unimplemented in Firefox (flag-only) as of Jul 2026; shipped Safari 26.0; Baseline *Limited availability*.
- Browser release calendar — Samsung Internet 28 = Blink 130 (Apr 2025), SI 29 stable Oct 2025, SI 30 rolling out 2026 (Windows build = M143); fleet-update lag discussion.
- WAI-ARIA Authoring Practices — Carousel pattern.
- Repository artefacts: `main` `Slideshow.astro`, `git show feature/slideshow:src/website/src/components/Slideshow.astro` (commit `16acbdf`), `produkte.astro`, `alpaka-wanderungen.astro`, `global.css` design tokens, `AGENTS.md` conventions.
