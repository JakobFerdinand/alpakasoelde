# Slideshow — Progressive-Enhancement Carousel Implementation Plan

| | |
|---|---|
| **Status** | Ready for implementation |
| **Date** | 2026-08-24 |
| **Implements** | `docs/concepts/design-concept-1-progressive-enhancement-carousel.md` |
| **Component** | `src/website/src/components/Slideshow.astro` (rewrite) |
| **Consumers** | `src/website/src/pages/produkte.astro`, `src/website/src/pages/alpaka-wanderungen.astro` |
| **Branch** | `feat/slideshow-concept-1` → PR into `main`; branch `feature/slideshow` stays frozen as reference |
| **Scope** | Plan only — no code changed by this document |

---

## Context

The shipped `Slideshow.astro` on `main` is a fixed-height crossfade box driven by a bare `setInterval`: no user control, autoplay that never pauses (hidden tab, off-screen, mid-interaction, reduced motion), no carousel semantics, one generic alt text for every photo, and a hard-coded `id="slideshow"` that makes a second instance on a page double-drive both slideshows. The owner's prototype on `feature/slideshow` (commit `16acbdf`) has the right vision — scroll-snap deck, gold round arrows, dot row, gentle autoplay — but bets it entirely on `::scroll-button()` / `::scroll-marker()` / `:target-current`, which as of August 2026 are Chromium-only (~0 % of this site's majority iOS traffic).

This plan converts the concept's recommendation (§4.4 decision B) into a build spec: **one standards-based path** — SSR-rendered scroll-snap scroller + real German-labelled `<button>` controls + one ~60-line vanilla controller — with genuinely-ready platform sugar gated behind `@supports`, and the owner's pseudo-element UI preserved verbatim as a commented Phase-3 takeover block. Bounded navigation for users; cut-to-start wrap for autoplay only.

### Locked decisions (resolve the concept's open questions §13)

| # | Decision | Value |
|---|---|---|
| D1 | Autoplay dwell | **5000 ms** (prop-configurable, clamped to 2000–15000 ms) |
| D2 | Arrows on mobile | **Hidden below 768 px** (swipe is primary); dots remain |
| D3 | Per-photo alt texts | Placeholder German alts derived from filenames ship now; final copy is a separate content task for the owner |
| D4 | Caption slot / product cards | Out of scope for v1 |
| D5 | Loop strategy | **Bounded** manual navigation (arrows disable at ends); **instant cut-to-start** for autoplay only |
| D6 | View Transitions | Not used anywhere in v1 (concept listed VT-guarded wrap-cut as optional sugar — omitted entirely) |

### Measured image geometry (drives CLS reservation)

Verified with ImageMagick against the actual sources:

| Page | Images | Orientations | Tallest h/w | Reserved ratio (`--ss-ratio-w/h`) |
|---|---|---|---|---|
| `/produkte` | Strickgabel 1709×2392, Zauberwolle/Karten/Polster/Wollpellets 4032×3024, Wolle_Amadeus 3024×4032 | mixed portrait + landscape | 2392/1709 ≈ 1.400 | **1709 / 2392** |
| `/alpaka-wanderungen` | wanderung1/2 1536×2048, wanderung3 2048×1536, wanderung4 900×1600 | mixed portrait + landscape | 1600/900 = 16/9 | **900 / 1600** |

Note: the concept sketched a default of `4/3` "≈ the source photos' 800×600" — that assumption is wrong for both pages. All sets are mixed-orientation; the frame reserves the tallest child's ratio (the `"auto"` mode), non-matching photos letterbox via `object-fit: contain` on the cream plate, exactly like today's look. No photo is ever cropped.

---

## 1. Component API

Exact frontmatter contract for `Slideshow.astro`. No `any`, no optional-member access without guards.

```ts
---
import { Image } from "astro:assets";
import type { ImageMetadata } from "astro";

export interface SlideshowImage {
  src: ImageMetadata;   // imported asset — Astro provides width/height/format
  alt: string;          // REQUIRED, German, per-photo
}

export interface Props {
  images: SlideshowImage[];
  /** sr-only region label, e.g. "Produktfotos" */
  label?: string;       // default: "Fotogalerie"
  /** autoplay on load; never under prefers-reduced-motion */
  autoplay?: boolean;   // default: true
  /** dwell time per slide in ms; clamped to [2000, 15000] */
  interval?: number;    // default: 5000
  /**
   * reserved frame ratio. "auto" = tallest supplied image (max height/width),
   * computed at build time from ImageMetadata. Explicit values use the CSS
   * syntax "W / H", e.g. "4 / 3".
   */
  aspect?: "auto" | `${number} / ${number}`; // default: "auto"
}
```

Frontmatter responsibilities (all build-time, SSR-static):

1. `const uid = Math.random().toString(36).slice(2, 8)` — unique per render; suffixes element ids so multiple instances per page can never collide (fixes `main`'s `id="slideshow"` bug).
2. Ratio resolution when `aspect === "auto"`: `const tallest = images.reduce((a, b) => b.src.height / b.src.width > a.src.height / a.src.width ? b : a)`, then `--ss-ratio-w: tallest.src.width; --ss-ratio-h: tallest.src.height`.
3. `const interval = Math.min(15000, Math.max(2000, props.interval ?? 5000))`.
4. Responsive `sizes` string derived from the resolved ratio so srcset matches the actually-displayed width (formula and resulting values in §6).

Breaking internal change: `images: any[]` → `SlideshowImage[]`. Both pages migrate in the same commit (§6).

---

## 2. Markup spec

SSR output outline of `<Slideshow />` (Astro template; `{…}` are frontmatter expressions):

```html
<section
  class="slideshow"
  data-slideshow                                  <!-- JS init hook -->
  data-autoplay={autoplay ? "" : undefined}
  data-interval={interval}
  role="region"
  aria-roledescription="Karussell"
  aria-label={label}                              <!-- e.g. „Produktfotos“ -->
  style={`--ss-ratio-w:${ratioW}; --ss-ratio-h:${ratioH};`}
>
  <div class="slideshow__stage">                  <!-- grid/flex row: arrow | frame | arrow -->
    <button class="slideshow__arrow slideshow__arrow--prev" type="button"
            data-dir="-1" aria-label="Vorheriges Bild"
            aria-controls={`ss-track-${uid}`} disabled>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
           stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="m15 18-6-6 6-6"></path>          <!-- lucide chevron-left -->
      </svg>
    </button>

    <div class="slideshow__frame">
      <ul class="slideshow__track" tabindex="0" id={`ss-track-${uid}`}>
        {images.map((img, i) => (
          <li class="slideshow__slide">
            <Image
              src={img.src}
              alt={img.alt}
              width={img.src.width}
              height={img.src.height}
              widths={[400, 800, 1200].filter((w) => w <= img.src.width)}
              sizes={sizesAttr}
              loading={i === 0 ? "eager" : "lazy"}
              fetchpriority={i === 0 ? "high" : undefined}
              decoding="async"
              class="slideshow__img"
            />
          </li>
        ))}
      </ul>
    </div>

    <button class="slideshow__arrow slideshow__arrow--next" type="button"
            data-dir="1" aria-label="Nächstes Bild"
            aria-controls={`ss-track-${uid}`} disabled>
      <!-- lucide chevron-right: <path d="m9 18 6-6-6-6"></path> -->
    </button>
  </div>

  <div class="slideshow__footer">
    <div class="slideshow__dots" role="group" aria-label="Bildauswahl">
      {images.map((_, i) => (
        <button type="button" class="slideshow__dot"
                aria-label={`Zu Bild ${i + 1}`}
                aria-current={i === 0 ? "true" : undefined}
                disabled></button>
      ))}
    </div>
    <p class="sr-only" aria-live="polite" data-slideshow-status></p>
  </div>
</section>
```

Rules that matter:

- **Controls are real `<button type="button">` elements rendered server-side**, not injected by JS — zero latency on first paint, and they can carry the SSR `disabled` attribute (Layer-0 guarantee: without JS nothing looks clickable-but-dead). The controller removes `disabled` during init.
- **Unique ids**: `ss-track-{uid}` referenced by both arrows via `aria-controls`.
- **Dots are buttons, not links** (no URL fragments polluting history); active dot carries `aria-current="true"`, moved by JS.
- **Live region**: empty `<p>`, `aria-live="polite"`; receives `„Bild X von N“` after *user-initiated* navigation only — never during autoplay.
- **`.sr-only`** is defined scoped inside this component (clip pattern), matching the existing convention in `Hero.astro:47`, `ImpressionBreak.astro:46`, `Contact.astro:150` — no global.css addition.
- **Icons**: inline SVG chevrons (lucide paths, `stroke="currentColor"`), because the site is `.astro`-only — `@lucide/svelte` is dashboard-only.
- The scroller is the `<ul>` itself; each slide is plain flowing list content (image + German alt), so screen-reader users browse the gallery as a list even without any controls.

---

## 3. CSS spec (scoped `<style>` in Slideshow.astro)

Organized bottom-up: universal base → controls/tokens → focus/reduced-motion → `@supports` enhancement layers. Two-space indent, no global additions.

### 3.1 Layer 1 base — scroller, snap, CLS reservation

```css
.slideshow {
  --frame-max-h: 32rem;             /* continuity with old 768px+ box height */
  inline-size: 100%;
}

/* CLS reservation: exact ratio box, height-capped by width clamp.
   Invariant: displayed height = min(100vw-ish, 32rem) — never exceeds the cap,
   ratio always holds, zero layout shift, no media queries needed. */
.slideshow__frame {
  position: relative;
  inline-size: min(100%, calc(var(--frame-max-h) * var(--ss-ratio-w) / var(--ss-ratio-h)));
  margin-inline: auto;
  aspect-ratio: var(--ss-ratio-w) / var(--ss-ratio-h);
  background-color: var(--schurwolle);   /* cream plate behind letterboxed photos */
}

.slideshow__track {
  display: flex;
  block-size: 100%;
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  overscroll-behavior-x: contain;   /* horizontal flick must not chain to page/back-swipe */
  scroll-behavior: smooth;          /* JS overrides per-move where needed */
}

/* Scrollbar is the Layer-0 affordance; hide it only once JS took over
   (data-ready set by the controller). Without JS: visible scrollbar + swipe. */
.slideshow[data-ready] .slideshow__track { scrollbar-width: none; }
.slideshow[data-ready] .slideshow__track::-webkit-scrollbar { display: none; }

.slideshow__slide {
  flex: 0 0 100%;
  min-inline-size: 0;
  display: flex;
  scroll-snap-align: center;
  scroll-snap-stop: always;         /* one photo per flick — no skipping */
}

.slideshow__img {
  inline-size: 100%;
  block-size: 100%;
  object-fit: contain;              /* never crop product/hike photos */
}
```

Resulting geometry: `/produkte` renders a centred card ≈ 366 px wide × 32rem tall on desktop (full-width ≈ 94 vw on a 390 px phone); `/alpaka-wanderungen` ≈ 288 px wide × 32rem tall on desktop (74 vw on a 390 px phone).

### 3.2 Controls — token pairings with verified AA contrast (from concept §8)

Contrast facts locked by the concept — do **not** re-derive or restyle:

| Pair | Ratio | Verdict |
|---|---|---|
| `--bluetenhonig` #e1b14a glyph on `--schurwolle` #fbf7ed | 1.85 : 1 | ✗ forbidden (prototype's gold-on-page arrows are NOT shippable) |
| `--schurwolle` chevron on solid `--taubenblau` #4b5b73 circle | 6.5 : 1 | ✓ AA text & graphics |
| `--bluetenhonig` accent ring/border on `--taubenblau` | 3.5 : 1 | ✓ graphics only (hover ring, active-dot border) |
| raw `--himmelblau` #8da5d3 dot on `--schurwolle` | 2.3 : 1 | ✗ forbidden for inactive dots |
| `--taubenblau` dot on `--schurwolle` | 6.5 : 1 | ✓ |

```css
/* Arrows: 56px circles (satisfies ≥44px target), taubenblau fill + cream glyph;
   gold is the hover ACCENT, never the fill. */
.slideshow__arrow {
  display: grid;
  place-items: center;
  inline-size: 56px;
  block-size: 56px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background-color: var(--taubenblau);
  color: var(--schurwolle);
  cursor: pointer;
  transition: background-color 150ms ease;
}
.slideshow__arrow:hover:not(:disabled) {
  /* 85% taubenblau over worst-case white photo still ≈ 4.1:1 vs cream glyph */
  background-color: color-mix(in srgb, var(--taubenblau) 85%, transparent);
  box-shadow: 0 0 0 2px var(--bluetenhonig);   /* gold accent ring, 3.5:1 on blue */
}
.slideshow__arrow svg { inline-size: 28px; block-size: 28px; }
.slideshow__arrow:disabled { opacity: 0.4; cursor: default; }

/* Placement: flanking columns ≥768px, half-inset overlay ≥1024px (prototype look),
   hidden below 768px (decision D2 — dots carry navigation on mobile). */
.slideshow__stage { display: flex; align-items: center; }
.slideshow__arrow { display: none; }
@media (min-width: 768px) {
  .slideshow__stage { gap: 0.75rem; }
  .slideshow__arrow { display: grid; }
}
@media (min-width: 1024px) {
  .slideshow__stage { position: relative; gap: 0; }
  .slideshow__arrow {
    position: absolute;
    top: 50%;
    translate: 0 -50%;
    z-index: 1;
  }
  .slideshow__arrow--prev { inset-inline-start: 0.75rem; }
  .slideshow__arrow--next { inset-inline-end: 0.75rem; }
}

/* Dots: visual 10–12px inside a padded ≥44×44 button. */
.slideshow__footer { display: flex; justify-content: center; margin-top: 1rem; }
.slideshow__dot {
  inline-size: 44px;
  block-size: 44px;
  display: grid;
  place-items: center;
  padding: 0;
  border: none;
  background: none;
  cursor: pointer;
}
.slideshow__dot::before {
  content: "";
  inline-size: 8px;
  block-size: 8px;
  border-radius: 50%;
  background-color: var(--taubenblau);           /* 6.5:1 — AA non-text */
  transition: scale 200ms ease;
}
.slideshow__dot[aria-current="true"]::before {
  inline-size: 12px;
  block-size: 12px;
  background-color: var(--bluetenhonig);         /* decorative centre … */
  border: 2px solid var(--taubenblau);           /* … border carries contrast */
  scale: 1.15;
}
```

Fallback discipline: the translucent hover uses `color-mix()` — declare the solid `var(--taubenblau)` first (done above) so engines without `color-mix()` keep a compliant solid button.

### 3.3 Focus visibility (never `outline: none`)

```css
.slideshow__track:focus-visible,
.slideshow__arrow:focus-visible,
.slideshow__dot:focus-visible {
  outline: 3px solid var(--taubenblau);   /* 6.5:1 on cream */
  outline-offset: 2px;
}
@media (min-width: 1024px) {
  /* arrows sit ON photos there — switch to cream over the imagery */
  .slideshow__arrow:focus-visible { outline-color: var(--schurwolle); }
}
```

### 3.4 Reduced motion (CSS side)

```css
@media (prefers-reduced-motion: reduce) {
  .slideshow *,
  .slideshow::before,
  .slideshow::after {
    transition: none !important;
    animation: none !important;
  }
  .slideshow__track { scroll-behavior: auto; }
}
```

JS enforces the same independently (`behavior: "auto"`, autoplay never starts) — either layer failing alone is harmless.

### 3.5 Layer 2 — `@supports`-gated polish (additive sugar only)

```css
/* Entrance micro-animation (~90% support; silently absent elsewhere) */
@supports (transition-behavior: allow-discrete) and (top: calc(1px * sign(1))) {
  .slideshow__arrow,
  .slideshow__dot {
    opacity: 1;
    translate: 0 0;
    transition: opacity 400ms ease, translate 400ms ease, background-color 150ms ease;
  }
  @starting-style {
    .slideshow[data-ready] .slideshow__arrow,
    .slideshow[data-ready] .slideshow__dot {
      opacity: 0;
      translate: 0 0.5rem;
    }
  }
}

/* Reading-progress hairline under the frame — decorative only (aria-hidden),
   scroll-driven so it is inert under reduced motion (no autonomous animation).
   Chromium + Safari 26+; hidden everywhere else. */
@supports (animation-timeline: scroll()) {
  .slideshow { timeline-scope: --ss-timeline; }
  .slideshow__progress { display: block; }
  .slideshow__track { scroll-timeline: --ss-timeline x; }
  .slideshow__progress::after {
    content: "";
    display: block;
    block-size: 2px;
    background-color: var(--bluetenhonig);
    transform-origin: left;
    animation: ss-progress linear both;
    animation-timeline: --ss-timeline;
  }
  @keyframes ss-progress { from { scale: 0 1; } to { scale: 1 1; } }
}
/* Base state outside @supports: */
.slideshow__progress { display: none; }
```

The progress element in markup: `<div class="slideshow__progress" aria-hidden="true"></div>` between stage and footer.

### 3.6 Layer 3 — Phase-3 native takeover (COMMENTED OUT, do not ship)

Keep the `feature/slideshow` pseudo-element rules **verbatim** in one commented block at the end of the `<style>` section, prefixed:

```css
/* ── Phase 3 (blocked until Baseline Newly Available): native CSS carousel ──
@supports selector(::scroll-button(*)) and selector(::scroll-marker) {
  .slideshow__arrow, .slideshow__dots { display: none; }
  … ::scroll-button(left/right), ::scroll-marker, :target-current rules
    copied verbatim from feature/slideshow commit 16acbdf, token-fixed …
}
Trigger: stable Safari AND unflagged Firefox ship both pseudo-elements.
Then: delete DOM-control branches in the script (§4), re-run checklist. ~0.5 d. */
```

Nothing in Layer-1 markup changes when this activates — the scroller and slides are deliberately designed so Phase 3 is subtraction, not surgery.

---

## 4. Script spec

One inline `<script>` in Slideshow.astro (Astro bundles/hoists as `type="module"`, deduplicated per page). Budget: **≤ ~65 readable lines / ≤ 1.5 KB min+gzip**, zero dependencies, no framework hydration. Init loop `[data-slideshow]` keeps every query scoped to the component root — multi-instance safe, SSR-safe (no top-level `window` access outside init).

TS-ish sketch (final code may reformat but must preserve these semantics):

```ts
const REDUCED = matchMedia("(prefers-reduced-motion: reduce)");
const SETTLE_MS = 140;

for (const root of document.querySelectorAll<HTMLElement>("[data-slideshow]")) init(root);

function init(root: HTMLElement) {
  const track = root.querySelector<HTMLElement>(".slideshow__track")!;
  const slides = [...track.children] as HTMLElement[];
  const prev = root.querySelector<HTMLButtonElement>('[data-dir="-1"]')!;
  const next = root.querySelector<HTMLButtonElement>('[data-dir="1"]')!;
  const dots = [...root.querySelectorAll<HTMLButtonElement>(".slideshow__dot")];
  const status = root.querySelector<HTMLElement>("[data-slideshow-status]")!;
  const n = slides.length;
  const interval = Number(root.dataset.interval ?? 5000);
  const s = { i: 0, stopped: false, paused: false, gesture: false, inView: true, timer: 0 };

  const canRun = () => !s.stopped && !s.paused && !s.gesture && s.inView && !document.hidden && !REDUCED.matches;
  const arm = () => { clearTimeout(s.timer); if (canRun()) s.timer = window.setTimeout(tick, interval); };
  const stop = () => { s.stopped = true; clearTimeout(s.timer); };          // permanent

  function goTo(i: number, announce = false) {
    const nextI = Math.max(0, Math.min(n - 1, i));
    if (nextI === s.i && announce) return;                 // boundary no-op: silent
    s.i = nextI;
    track.scrollTo({ left: slides[s.i].offsetLeft - track.offsetLeft,
                     behavior: REDUCED.matches ? "auto" : "smooth" });
    sync(announce);
  }
  function jumpTo(i: number) {                             // autoplay cut: instant, silent
    s.i = i;
    track.scrollTo({ left: slides[i].offsetLeft - track.offsetLeft, behavior: "auto" });
    sync(false);
  }
  function sync(announce: boolean) {
    prev.disabled = s.i === 0;                             // bounded ends
    next.disabled = s.i === n - 1;
    dots.forEach((d, k) => k === s.i ? d.setAttribute("aria-current", "true")
                                     : d.removeAttribute("aria-current"));
    if (announce) status.textContent = `Bild ${s.i + 1} von ${n}`;
  }
  const indexFromScroll = () =>
    Math.max(0, Math.min(n - 1, Math.round(track.scrollLeft / track.clientWidth)));

  // gesture tracking (touch drag / trackpad): pause while active;
  // a SETTLED displacement > ⅓ slide width counts as deliberate ⇒ permanent stop
  let settleTimer = 0, downScroll = 0;
  const settleGesture = () => { s.gesture = false;
    Math.abs(track.scrollLeft - downScroll) > track.clientWidth / 3 ? stop() : sync(false), arm(); };

  root.addEventListener("click", (e) => {                  // delegated: arrows + dots
    const btn = (e.target as HTMLElement).closest<HTMLButtonElement>("[data-dir], .slideshow__dot");
    if (!btn || btn.disabled) return;
    stop();
    if (btn.dataset.dir) goTo(s.i + Number(btn.dataset.dir), true);
    else goTo(dots.indexOf(btn), true);
  });
  track.addEventListener("keydown", (e) => {               // ← ↑ → ↓ Home End
    const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[e.key];
    if (step === undefined && e.key !== "Home" && e.key !== "End") return;
    e.preventDefault(); stop();
    if (e.key === "Home") goTo(0, true);
    else if (e.key === "End") goTo(n - 1, true);
    else goTo(s.i + step, true);
  });
  track.addEventListener("pointerdown", (e) => { s.gesture = true; downScroll = track.scrollLeft;
    clearTimeout(settleTimer); clearTimeout(s.timer); });  // never move under a finger
  track.addEventListener("pointerup", () => {
    settleTimer = window.setTimeout(settleGesture, SETTLE_MS); });  // wait for momentum
  track.addEventListener("pointercancel", settleGesture);
  track.addEventListener("wheel", () => { s.gesture = true;                // Shift+wheel etc:
    clearTimeout(settleTimer); settleTimer = window.setTimeout(settleGesture, SETTLE_MS); },
    { passive: true });

  const settledScroll = () => { if (!s.gesture) { s.i = indexFromScroll(); sync(false); } };
  if ("onscrollend" in HTMLElement.prototype)
    track.addEventListener("scrollend", settledScroll);    // Safari ≤26.1 fallback:
  else track.addEventListener("scroll", (() => { let t = 0; return () => {
    clearTimeout(t); t = window.setTimeout(settledScroll, 120); }; })(), { passive: true });

  const setPaused = (p: boolean) => { s.paused = p; p ? clearTimeout(s.timer) : arm(); };
  root.addEventListener("pointerenter", () => setPaused(true));            // hover
  root.addEventListener("pointerleave", () => setPaused(false));
  root.addEventListener("focusin", () => setPaused(true));                 // keyboard focus
  root.addEventListener("focusout", (e) => { if (!root.contains(e.relatedTarget as Node)) setPaused(false); });

  new IntersectionObserver(([en]) => { s.inView = en.isIntersecting;       // off-screen pause
    en.isIntersecting ? arm() : clearTimeout(s.timer); }, { threshold: 0.5 }).observe(root);
  document.addEventListener("visibilitychange", () =>                      // hidden tab pause
    document.hidden ? clearTimeout(s.timer) : arm());
  REDUCED.addEventListener("change", () =>                                 // live toggle
    REDUCED.matches ? clearTimeout(s.timer) : arm());
  addEventListener("resize", () => requestAnimationFrame(() => {           // re-sync index
    s.i = indexFromScroll(); sync(false); }), { passive: true });

  function tick() {
    if (!canRun()) return;
    s.i === n - 1 ? jumpTo(0) : goTo(s.i + 1);             // the “cut” at the end
    arm();
  }

  for (const b of [prev, next, ...dots]) b.disabled = false;  // enable SSR-disabled controls
  root.setAttribute("data-ready", "");                        // hides scrollbar, gates entrance
  sync(false);
  if (!REDUCED.matches) arm();
}
```

Behavioural contracts (exact, no interpretation needed):

- **Boundary behaviour**: manual nav is strictly bounded. `prev` gets `disabled` at index 0, `next` at last index (mirrors future `::scroll-button():disabled`). Arrow-key past an end is a clamped no-op that stays silent (no announcement for unchanged index).
- **Autoplay wrap**: on the tick at the last slide, autoplay performs an **instant** `behavior:"auto"` reset to slide 1 (silent, no smooth reverse sweep) and re-arms — the JS equivalent of the prototype's `tostart` teleport.
- **Permanent stop** (`stopped`, never resumes, timer cleared): any click/tap on an arrow or dot; any keyboard navigation; a completed deliberate drag/trackpad gesture with displacement > ⅓ of the track width. Temporary pauses (hover, focus, off-screen, hidden tab, mid-gesture, wheel) merely clear the pending timer and re-arm via `arm()` when lifted.
- **Announcements**: only user-initiated moves write `„Bild X von N“` to the live region; autoplay and programmatic cuts stay silent.
- **Cleanup**: timers die on `stopped`; listeners/observer live with the component (page-lifetime, MPA — no SPA teardown concerns).

---

## 5. Zero-JS baseline statement

With scripts disabled the slideshow remains fully usable content: a native horizontal scroller (`overflow-x: auto`) with momentum swiping on touch, a **visible scrollbar** as affordance (`scrollbar-width: none` applies only under `[data-ready]`, which JS sets), `scroll-snap` giving the same one-photo-per-flick feel, every image present with its own German alt text inside semantic list markup, and zero CLS (aspect-ratio reservation is pure CSS). Arrows/dots render visibly `disabled` — honest dead controls rather than fake affordances. No autoplay runs. Nothing on the page requires JS to be read or browsed.

---

## 6. Page integration

### 6.1 `src/website/src/pages/produkte.astro`

Replace line 15 and line 35:

```astro
--- …imports unchanged…
const images = [
  { src: Strickgabel,   alt: "Strickgabel für Zauberwolle aus Alpakawolle" },      // placeholder alt
  { src: Zauberwolle,   alt: "Zauberwolle aus weicher Alpakawolle" },
  { src: WolleAmadeus,  alt: "Handgesponnene Schurwolle Amadeus" },
  { src: Karten,        alt: "Handgefertigte Karten mit Alpakamotiven" },
  { src: Polster,       alt: "Polster mit Alpakafüllung" },
  { src: Wollpellets,   alt: "Wollpellets als naturreiner Dünger" },
];
---
…
<section class="section">
  <Slideshow images={images} label="Produktfotos" />
</section>
```

Resolved build-time values: `--ss-ratio-w:1709 --ss-ratio-h:2392`; desktop card ≈ 366 px wide; emitted `sizes="(min-width: 768px) 432px, 100vw"`.

### 6.2 `src/website/src/pages/alpaka-wanderungen.astro`

Replace line 16 and line 51:

```astro
const images = [
  { src: Wanderung1, alt: "Alpakas auf der Wanderung durch die Innauen" },          // placeholder alt
  { src: Wanderung2, alt: "Wandergruppe mit Alpakas auf dem Feldweg" },
  { src: Wanderung3, alt: "Blick über die Innauen bei der Alpaka-Wanderung" },
  { src: Wanderung4, alt: "Alpaka beim gemütlichen Spaziergang" },
];
---
…
<Slideshow images={images} label="Eindrücke von den Alpaka-Wanderungen" />
```

Resolved build-time values: `--ss-ratio-w:900 --ss-ratio-h:1600`; desktop card = 288 px wide; emitted `sizes="(min-width: 768px) 336px, 100vw"`.

### 6.3 Image delivery attributes (set inside the component, both pages)

- `widths={[400, 800, 1200].filter((w) => w <= img.src.width)}` — Astro generates AVIF/WebP-capable `srcset` (site default WebP) without overshooting small sources (e.g. wanderung4 is 900 px wide).
- `sizes` computed in frontmatter as `(min-width: 768px) ${Math.ceil((512 * ratioW / ratioH) * 1.15 / 16) * 16}px, 100vw` (32rem cap × safety factor, rounded up to a 16 px grid) — matches the actually-rendered card width instead of the concept's placeholder `(min-width: 768px) 768px, 100vw`.
- Slide 1: `loading="eager"` + `fetchpriority="high"`; remaining slides `loading="lazy"`; all `decoding="async"`. Both usages sit below the hero, so LCP is untouched.

---

## 7. Accessibility checklist

- [ ] `role="region"` + `aria-roledescription="Karussell"` + German `aria-label` from `label` prop (default „Fotogalerie“)
- [ ] Prev/next: `aria-label="Vorheriges Bild"` / `"Nächstes Bild"` + `aria-controls` pointing at unique track id
- [ ] Dots: `role="group"` wrapper labelled „Bildauswahl“, per-dot `aria-label="Zu Bild N"`, active dot `aria-current="true"`
- [ ] Live region `aria-live="polite"` announces `„Bild X von N“` after user-initiated navigation only
- [ ] Keyboard on scroller (`tabindex="0"`): `→`/`↓` next, `←`/`↑` prev, `Home` first, `End` last, all with `preventDefault`; Tab flows naturally track → arrows → dots
- [ ] Contrast: cream-on-taubenblau 6.5:1; taubenblau dots on cream 6.5:1; gold only as accent (3.5:1 graphics); himmelblau never used raw for indicators
- [ ] Focus: `:focus-visible` 3px `--taubenblau` ring offset 2px on cream; `--schurwolle` ring when arrows overlay photos (≥1024px); `outline: none` appears nowhere
- [ ] Touch targets: dots 44×44 px effective, arrows 56 px circles
- [ ] Reduced motion honoured twice (CSS kill-switch + JS: no autoplay start, `behavior:"auto"` jumps), including live `change` events
- [ ] Screen readers without JS browse the gallery as a plain image list — live region is convenience, not the only path

## 8. Performance checklist

- [ ] CLS = 0 on `/produkte` and `/alpaka-wanderungen`: aspect-ratio reservation + intrinsic `<Image>` dimensions, no JS-measured heights
- [ ] Script ≤ ~1.5 KB min+gzip, single deduplicated module per page, zero dependencies
- [ ] First slide eager (+`fetchpriority="high"`), rest lazy; hero LCP unaffected (component sits below the fold)
- [ ] Autoplay pauses off-screen (`IntersectionObserver` threshold 0.5) and on hidden tabs — battery/data respect
- [ ] Only passive scroll/wheel/resize listeners; single `setTimeout` chain, no `setInterval` pile-ups
- [ ] Multi-instance safe: scoped queries, random uid ids, closed-over per-instance state

## 9. Verification steps

Automated (repo standard — no test harness exists for the website beyond this):

```bash
cd src/website && pnpm install && pnpm run build   # runs astro check — must pass clean
```

Manual test matrix (attach screenshots/clip to the PR per repo guidelines):

| # | Scenario | Expected |
|---|---|---|
| 1 | iPhone Safari (iOS 17/18 and 26 if available): swipe through all slides | One photo per flick (`scroll-snap-stop`), rubber-band at ends, page doesn't chain-scroll horizontally |
| 2 | Same, mid-drag | Carousel never auto-advances under the finger; after release >⅓-slide drag, autoplay stops permanently |
| 3 | Desktop keyboard: Tab to track, `→ ← ↓ ↑ Home End` | Navigation works, live region announces, arrows disable at ends, autoplay permanently stopped afterwards |
| 4 | Hover / focus into controls | Autoplay pauses; resumes after leave (if not permanently stopped) |
| 5 | Emulate `prefers-reduced-motion: reduce` (DevTools) | No autoplay at all; jumps instant; no entrance transitions; progress hairline still tracks scroll (scroll-driven) |
| 6 | DevTools → disable JavaScript | Scroller swipeable with visible scrollbar, all images reachable, disabled controls, zero CLS |
| 7 | Background the tab / scroll component out of view for > interval | No advancement occurs (check via dots) |
| 8 | Autoplay reaches last slide untouched | Instant cut back to slide 1, continues forward glide, no announcement |
| 9 | Cross-browser pass: Chrome, Firefox, Safari desktop, Android Chrome, Samsung Internet | Identical visuals (only entrance/hairline sugar may be absent) |
| 10 | Lighthouse mobile run on both pages | CLS 0, no LCP regression vs `main` |
| 11 | Render two `<Slideshow />` instances on a scratch route | Independent state, unique ids, no double-driving (regression guard for `main`'s bug) |

## 10. Effort breakdown & file-change list

| Task | Estimate |
|---|---|
| Component rewrite: markup + layered CSS (§2–§3) | 0.5 d |
| Controller script incl. guards/fallbacks (§4) | 0.5 d |
| Page migrations + responsive/QA pass (§6, matrix items 1–10) | 0.5 d |
| PR material (screenshots/clip), fixes from review | 0.5 d |
| **Total** | **≈ 2 dev-days** |
| Phase-3 native swap (later, when Baseline Newly Available) | ≈ 0.5 d |

| File | Change | Approx. LOC delta |
|---|---|---|
| `src/website/src/components/Slideshow.astro` | Full rewrite (73 → ~340 lines) | **+270 / −73** |
| `src/website/src/pages/produkte.astro` | `images` array → objects with alts; add `label` prop | +12 / −1 |
| `src/website/src/pages/alpaka-wanderungen.astro` | same | +10 / −1 |
| `src/website/src/styles/global.css` | **untouched** (tokens sufficient; `.sr-only` stays component-scoped) | 0 |

No API, dashboard, or infrastructure files change; `requests.http` samples unaffected.

## 11. Risks

| Risk | Mitigation |
|---|---|
| Mixed-orientation sets make the frame tall on phones (portrait 9/16 sources) | Width-clamp invariant caps height at 32rem everywhere while keeping exact ratio; letterboxing on the cream plate preserves the current aesthetic — verify item 1 on small devices |
| `overscroll-behavior` gaps on older iOS 16/17 | Progressive containment; worst case is today's status quo (page chains scroll) |
| `scrollend` absent (Safari ≤ 26.1, ≈ 5 %) | Debounced passive `scroll` fallback, feature-detected, one branch |
| Samsung Internet fleet on old Blink | Base layer is engine-agnostic; every `@supports` gate is additive sugar |
| Autoplay annoyance regression vs prototype | Permanent-stop rule + full pause matrix (§4); owner sign-off on 5 s dwell (D1) |
| Placeholder alts ship accidentally-final | Content task ticketed for owner (D3); alts are trivially editable in the two page files |
| Scoped-style pitfall: styling `<Image>` output | Solved by passing `class="slideshow__img"` (pattern already proven in `Navbar.astro:26`, `Hero.astro:8`) — do not rely on descendant selectors crossing the component boundary |
| Spec drift of Overflow-5 pseudos before Phase 3 | Quarantined commented block references the frozen commit; adoption re-reviewed then |

## 12. Deviations from the concept doc

1. **Aspect prop type generalized**: `"auto" | \`${number} / ${number}\`` instead of the drafted union `"4/3"|…` — a backward-compatible superset needed because actual ratios (1709/2392, 900/1600) aren't in the union.
2. **Concept's 4/3 default abandoned**: measured sources are mixed-orientation (§Context table); `"auto"` resolves to the tallest child at build time, plus a width-clamp invariant on the frame that the concept didn't specify (prevents a ~140vw-tall mobile box for portrait-dominant sets while keeping CLS at exactly 0).
3. **Scrollbar hiding gated on `[data-ready]`** instead of unconditional (concept §10.1 sketch): resolves the conflict with the concept's own Layer-0 requirement ("scrollbar visible as affordance").
4. **Controls ship SSR-`disabled` and are enabled by JS**: follows concept §4's Layer-0 diagram ("controls render disabled"); §4.1's looser prose ("buttons simply do nothing") is superseded.
5. **`sizes` computed per resolved ratio** instead of the concept's fixed `(min-width: 768px) 768px, 100vw` placeholder — the latter would badly oversize srcset candidates given the clamped card widths.
6. **Arrow size conflict resolved to 56 px** (concept says 48 px in §6.1 and 56 px in §8; 56 satisfies both the visual design and ≥44 px targets).
7. **View Transitions dropped entirely** from v1 (concept listed them as optional wrap-cut polish guarded by feature detection) — the instant cut already reads correctly.
8. **Image format**: stay on Astro's default WebP pipeline like the rest of the site; a site-wide AVIF switch is out of scope here despite the concept's "AVIF/WebP for free" phrasing.
9. **Playwright smoke tests not part of this plan**: `package.json` has Playwright scripts but no config/tests exist on disk; verification is `pnpm run build` (astro check) + the manual matrix above, matching AGENTS.md. Adding a smoke suite would require bootstrapping the harness first (separate task).
