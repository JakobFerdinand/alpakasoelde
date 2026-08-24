# Design Concept 3 — Smart Gallery Experience

**Status:** Concept (no code yet) · **Date:** 2026-08-24 · **Scope:** `src/website/src/components/Slideshow.astro` and its two call sites (`produkte.astro`, `alpaka-wanderungen.astro`)
**Supersedes:** main-branch crossfade slideshow; evaluates (and rejects as base) the CSS-only carousel prototype on `feature/slideshow`

---

## 1. Motivation

The current `Slideshow.astro` is a fixed-height box (24rem/32rem) with all slides absolutely stacked and a blind 4-second `setInterval` crossfade. It fails both of its real jobs:

1. **Products (`/produkte`)** — six shop items (Strickgabel, Zauberwolle, Wolle Amadeus, Grußkarten, Polster, Wollpellets) that visitors may want to *inspect* before visiting the Hofladen. There is no way to swipe, no captions, no way to look closer, and every slide shares one generic alt text ("Alpaka auf der Alpakasölde Farm"), which is wrong for product photos and invisible to screen-reader shopping.
2. **Experiences (`/alpaka-wanderungen`)** — four hike photos whose job is *atmosphere* between info sections. A rigid fixed-height box with hard cuts every 4 s (running even when off-screen or in a background tab) is neither calm nor performant.

The owner's `feature/slideshow` prototype moved to a scroll-snap carousel with native CSS carousels (`::scroll-button()`, `::scroll-marker()`, anchor positioning). The direction is right; the implementation is not shippable: as of August 2026 those pseudo-elements are **Chromium-only**, while most of this site's traffic is mobile Safari/Firefox. The prototype also has no zoom, no per-product semantics, no deep links.

This concept defines **one flexible component with two variants** — `product` (inspection-oriented) and `story` (atmosphere-oriented) — built on a scroll-snap baseline that works everywhere, progressively enhanced with ~3 KB of inline vanilla JS. No framework islands on the marketing site (repo rule).

---

## 2. Goals / Non-goals

### Goals

- G1 — One typed component API serving both contexts via `variant="product" | "story"`; pages pass semantic slide data instead of bare image URLs.
- G2 — Product mode: large inspectable image, thumbnail navigation, fraction counter, German per-product captions, optional badge slot, lightbox with pinch-zoom (Phase 2).
- G3 — Story mode: calm auto-playing, swipeable gallery with caption overlay; no zoom chrome.
- G4 — Excellent mobile-first behavior: native touch swipe via CSS scroll-snap; all touch targets ≥ 44 × 44 px; full keyboard support.
- G5 — Zero-JS baseline: with scripts disabled the galleries remain fully browsable (native scrolling); JS only adds state sync, autoplay control, lightbox, deep links.
- G6 — Performance: CLS-free aspect-ratio reservation, eager first image / lazy rest, Astro `<Image />` responsive formats, LCP ≤ 2.5 s p75, INP < 200 ms p75.
- G7 — WCAG 2.1 AA: APG carousel pattern, German `aria-label`s, modal lightbox via `<dialog>`, `prefers-reduced-motion`, AA-contrast controls over photos.
- G8 — Lightweight analytics hooks (`slide_change`, `lightbox_open`) compatible with the self-hosted pageview beacon in `Layout.astro`.

### Non-goals

- No client-side framework islands, no Swiper/Glider/embla libraries on `src/website`.
- No e-commerce checkout — badges/price hints are display-only; contact flow stays as-is.
- No video support in this iteration (images only).
- No adoption of Chromium-only CSS carousels (`::scroll-button()`/`::scroll-marker()`) as the *primary* mechanism; revisit behind `@supports` in Phase 3 at the earliest.
- No restructuring of the two pages beyond swapping the `<Slideshow …>` invocation.

---

## 3. Component API

### 3.1 Typed props draft

```ts
export type GalleryVariant = 'product' | 'story';

export interface GallerySlide {
  src: ImageMetadata;            // static import from ../images/…
  alt: string;                   // REQUIRED, specific German text
  caption?: string;              // short line under/over the image
  description?: string;          // 1–2 sentences, product variant only
  badge?: string;                // e.g. "Neu", "Bestseller"; product variant only
}

export interface Props {
  slides: GallerySlide[];
  variant?: GalleryVariant;                    // default: 'product'
  id?: string;                                 // stable id → hash deep links (#produkte-3)
  autoplay?: boolean;                          // story default true, product false
  intervalMs?: number;                         // default 6000 (calmer than today's 4000)
  loop?: boolean | 'wrap-buttons';             // see §5.4, default 'wrap-buttons'
  showThumbnails?: boolean;                    // product default true ≥ md, else dots+fraction
  lightbox?: boolean;                          // product true, story false (Phase 2)
}
```

Pages keep passing statically imported images; the page owns content (repo pattern: copy lives in `.astro` fragments), the component owns behavior.

### 3.2 Variant matrix

| Aspect | `variant="product"` | `variant="story"` |
| --- | --- | --- |
| Purpose | Inspect items, drive Hofladen visits | Convey atmosphere |
| Layout | Square-ish stage (1/1 mobile, 4/3 ≥ md) + caption block below | Full-width landscape stage (16/10 mobile, 21/9 ≥ lg) |
| Fit | `object-fit: contain` over dominant-color backdrop (products must not be cropped) | `object-fit: cover` (immersion beats completeness) |
| Navigation | Thumbnails (≥md) / dots + „3 von 6" (mobile) + prev/next buttons | Dots only, subtle; swipe-first |
| Autoplay | Off | On, IntersectionObserver-gated, stops after first interaction |
| Zoom/Lightbox | Yes (Phase 2) | No |
| Badges/captions | Caption + optional description + badge under the stage | Optional overlay caption with scrim, bottom-left |
| Looping | Bounded, buttons wrap (`wrap-buttons`) | Buttons wrap; swipe stays bounded |

---

## 4. UX specs per mode

### 4.1 Product mode (`/produkte`)

- **Stage:** large square area on mobile (100 vw − padding), 4:3 up to ~900 px wide on desktop, centered in the page column. Background = blurred/dominant-color fill derived from the photo (build-time) so `contain` letterboxing looks intentional, not broken. Fallback until Phase 3: flat `--schurwolle`.
- **Caption block below stage:** product name (e.g. „Strickgabel aus Holz"), optional one-liner („Für knotenfreies Stricken ohne Zählen."), optional badge chip (`--bluetenhonig` background, `--taubenblau` text, e.g. „Neu"). Captions swap with the active slide; they are real DOM text (SEO + no-JS visibility for the *first* slide).
- **Counter:** „3 von 6" top-right of the stage in a pill; doubles as the screen-reader status (`aria-live="polite"`).
- **Inspect affordance:** magnifier icon button „Bild vergrößern" (Phase 2) opening the lightbox; the whole stage is also tappable.
- **Alt texts** become product-specific, e.g.:
  - `alt="Strickgabel aus Holz mit begonnener Wollkordel"`
  - `alt="Knäuel Zauberwolle in kräftigen Buntfarben"`
  - `alt="Handgesponnene Wolle Amadeus in Naturtönen"`
  - `alt="Handgefertigte Grußkarten mit Alpakamotiv"`
  - `alt="Kuschelweiches Polster gefüllt mit Alpakawolle"`
  - `alt="Wollpellets als natürlicher Dünger aus Schurwolle"`

### 4.2 Story mode (`/alpaka-wanderungen`)

- **Stage:** edge-to-edge within the container, 16/10 on phones, cinematic 21/9 on desktop; `cover`.
- **Autoplay:** 6 s interval, crossfade-free (native snap slide, smooth). Rules:
  - starts only when ≥ 50 % visible (IntersectionObserver),
  - pauses on hover, focus-within, any pointerdown/swipe, `visibilitychange` hidden,
  - **stops permanently** after the user interacts manually (WCAG 2.2.2 best practice) — pause/play button „Diashow pausieren"/„Diashow abspielen" remains available,
  - disabled entirely under `prefers-reduced-motion: reduce`.
- **Caption overlay:** bottom-left, e.g. „Alpakawanderung durch die Innauen", white `--schurwolle` text on a gradient scrim (`--taubenblau` → transparent) for guaranteed AA contrast regardless of the photo beneath.
- Example alt texts: `alt="Alpakas auf dem Wanderweg durch die Innauen bei Ering"`, `alt="Teilnehmerin führt ein Alpaka am Halfter"`.

---

## 5. Navigation & accessibility spec

### 5.1 Indicator choice — argued trade-offs

| Option | Pros | Cons | Verdict |
| --- | --- | --- | --- |
| **Dots** | Tiny, universal, familiar | Poor tap targets if small; no preview of what you jump to; unusable > ~8 slides | Story mode (≤ 6 slides) + product mode on mobile |
| **Fraction „3/6"** | Orientation at a glance; cheap; great SR status | Not directly operable | Paired with dots everywhere |
| **Thumbnails** | Shopping mental model ("I want the yellow skein"); direct access; invites exploration | Costs layout height; needs lazy thumbs; tap targets need ≥ 44 px rows | Product mode ≥ 768 px; horizontally scrollable strip, active thumb ringed in `--bluetenhonig` |

Product mobile therefore gets **dots + fraction**, not tiny thumbnails — thumbnails return as a horizontal strip once there is room.

### 5.2 Controls

- Prev/next circular buttons 48 × 48 px (≥ 44 px required), `--schurwolle` icon on `--taubenblau` at 85 % opacity pill/scrim → ≈ 6.9:1 contrast (AA ✓ even over bright photos, because the pill carries its own background). Focus ring 2 px offset `--bluetenhonig`.
- Labels: „Vorheriges Bild", „Nächstes Bild", „Zu Bild 3 springen" (dots/thumbs get `aria-label="Zu Bild X: {Produktname}"`).
- Buttons are real `<button>`s rendered server-side; without JS they still scroll the snap container via `scrollIntoView`-less anchors? — No: without JS we hide buttons/dots entirely (`html:not(.js)` gate set by a 1-line inline script in `Layout`); the baseline interaction is native swipe/scroll. This avoids dead controls.

### 5.3 Keyboard

On the gallery region (roving tabindex: the scrollport has `tabindex="0"`, controls are tabbable):

- `←`/`→`: previous/next slide
- `Home`/`End`: first/last slide
- `Enter`/`Space` on stage (product): open lightbox; `Escape` closes it
- Inside lightbox `<dialog>`: `Tab` cycles natively (focus trap built into `showModal()`), `Escape` closes natively, focus returns to invoker automatically

### 5.4 Swipe, trackpad, loop vs bounded

- Interaction base = CSS `scroll-snap-type: x mandatory` on an `overflow-x:auto` track, `scroll-snap-stop: always`, `scroll-behavior: smooth` (auto under reduced motion). Native momentum scrolling comes free on iOS/Android; trackpad horizontal scroll works on desktop.
- **Loop decision:** infinite looping needs either DOM cloning (breaks "current index is truth", confuses SR users) or Chromium-only CSS animation tricks. Pragmatic call: **bounded swipe + wrapping buttons** (`loop: 'wrap-buttons'`) — pressing „Nächstes Bild" on the last slide scrolls to the first. Autoplay in story mode wraps the same way. This keeps the DOM linear and honest.

### 5.5 Lightbox dialog semantics (Phase 2)

- `<dialog>` opened via `showModal()` → implicit `role="dialog"`, `aria-modal="true"`, inert background, top layer (no z-index wars).
- Content: close button „Schließen" (48 px), prev/next, counter, caption; image in a pinch-zoomable viewport (`overflow:auto`, `img { width: max(100%, 200%) }` with `touch-action: pan-x pan-y pinch-zoom` → native pinch-zoom on iOS/Android without gesture JS; double-click toggles 1×/2× on desktop).
- `aria-label` on the dialog: „Bildansicht: Strickgabel aus Holz".
- iOS caveat: use `100dvh` (not `vh`) for the dialog surface; test Safari rubber-banding inside the zoom viewport.

### 5.6 Reduced motion

`@media (prefers-reduced-motion: reduce)`: no autoplay, no smooth scrolling (instant jumps), no opacity/transform transitions on caption swaps; lightbox opens/closes without animation.

---

## 6. State & interaction model

- **Single source of truth:** `currentIndex` lives in the tiny script instance per gallery. Scroll position is the *input*: `scrollend` (Baseline since Dec 2025) recomputes `index = Math.round(scrollLeft / slideWidth)`; programmatic changes are the output: `slides[index].scrollTo({behavior:'smooth'})` guarded by an internal flag so the component never fights itself.
  Fallback for Safari < 26.2 (~11 %): debounced `scroll` listener (120 ms) computing the same index — 8 lines.
- All UI (dots, thumbs, counter, caption swap) derives from `currentIndex`; no parallel state anywhere.
- **Deep links:** product galleries accept `#produkte-3` (id prefix + 1-based index). Behavior:
  - on load: if hash matches, scroll the track instantly (`behavior:'instant'`, `preventScroll` on surrounding focus) *after* layout, before paint where possible;
  - on change: `history.replaceState(null, '', '#produkte-4')` — replaceState, **not** pushState: swiping through six products must not create six history entries (back button would feel broken);
  - `hashchange` (user pastes/shares a link while on the page) navigates the gallery;
  - caveat documented: browser scroll restoration restores *page* position on back/forward; our index restore keys off the hash only, so back/forward to the same page with different hashes triggers `hashchange` → intentional slide change, everything else leaves the user where they were.
- Story galleries don't write hashes (no sharing value, avoids churn).

---

## 7. Loading strategy & performance plan

### 7.1 Image delivery

- Astro `<Image />` everywhere (repo mandate):
  - slide 1: `loading="eager"` `fetchpriority="high"` `decoding="sync"` — this is the LCP candidate on both pages;
  - slides 2+: `loading="lazy"` `decoding="async"`;
  - `widths={ [480, 768, 1080, 1440] }` + `sizes`: product `(min-width: 768px) 60vw, calc(100vw - 3rem)`; story `(min-width: 1024px) 80rem, 100vw`;
  - `format={['avif','webp']}` (sharp defaults already emit these; make it explicit);
  - lightbox loads a separate 1920–2400 px rendition **only on open** (never bloat initial payload);
  - thumbnails reuse the same source with `widths={[96,160]}` and their own `sizes` (browser cache hit, near-zero cost).
- **CLS:** every stage reserves space via `aspect-ratio` + `width/height` attributes on `<img>`; the track height is therefore known before images decode. Target CLS contribution: 0.
- **Placeholders:** Phase 1 ships a flat token background; Phase 3 option adds build-time dominant color (one sharp pass, inlined as `style="--ph:#c8b49a"`) or 20 px blur-up via `getImage().src` data URI. Chosen late because it touches the build pipeline.
- **Hover-intent preload (desktop only):** `matchMedia('(hover:hover)')` + `pointerenter` on next/prev → `new Image().src = neighbor.src` so button navigation feels instant.

### 7.2 Budgets & targets

| Metric | Target (p75 mobile) | Rationale |
| --- | --- | --- |
| Component JS | ≤ 3 KB gzipped, one hoisted module for N instances | Repo bans islands on website; script attaches per `[data-gallery]` root |
| LCP | ≤ 2.5 s | First slide eager/high priority, AVIF/WebP, reserved box |
| CLS | ≤ 0.02 overall page | aspect-ratio reservation |
| INP | < 200 ms | Handlers are O(1); scroll work deferred to `scrollend`; no timers during idle |
| Extra requests | 0 blocking beyond current page budget | Lazy slides fetch on approach only |

### 7.3 Zero-JS baseline

With JS disabled: scroll-snap swipe/scroll works, first caption visible, images load per native lazy loading, alt texts intact. Hidden-by-default controls stay hidden (no dead UI). This is strictly better than today's component, which shows nothing but slide 1 forever.

---

## 8. Browser support check (verified 2026-08-24)

| Technique | Status Aug 2026 | Use in concept | Fallback |
| --- | --- | --- | --- |
| CSS scroll-snap | Baseline widely available (all engines for years) | **Foundation** | none needed |
| `scrollend` event | **Baseline Newly Available Dec 2025**: Chrome/Edge 114+, Firefox 109+, **Safari/iOS 26.2+**; ~88.5 % global | Index sync | debounced `scroll` (~12 % older devices, incl. iOS < 26.2 which matters most here) |
| Same-document View Transitions (`startViewTransition`) | Baseline Newly Available Oct 2025: Chrome/Edge 111+, Safari 18+, Firefox 121+ (types missing in FF initially) | Phase 3 polish only | plain swap, silently fine |
| Cross-document `@view-transition` | Chrome/Edge 126+, Safari 18.2+, Samsung 27+; **Firefox still flag-only** mid-2026 | not used | — |
| Popover API | Baseline (widely available Apr 2025), all engines | considered for lightbox, **not used** — we need modality/focus trap → `<dialog>` | — |
| `<dialog>` + `showModal()` | Baseline widely available (Chrome 37+/Safari 15.4+/Firefox 98+) | **Lightbox foundation** | none needed |
| CSS Anchor Positioning | **Baseline 2026**: Chrome/Edge 125+, Safari 26+, Firefox 147+ (~90 % global; iOS gated on OS update) | optional Phase 3 niceties only | static CSS positioning |
| `::scroll-button()` / `::scroll-marker()` | **Still Chromium-only** (Chrome/Edge); Firefox & Safari "in progress" per Feb 2026 sources | rejected as primary mechanism; possible `@supports (scroll-marker-group: after)` enhancement later | our own buttons/dots |
| `:has()` | Baseline widely available (since Dec 2023) | CSS state hooks (e.g. `[data-state]` alternatives) | attribute selectors anyway |
| `touch-action: pinch-zoom` | Universal | lightbox zoom viewport | double-tap toggle |

Conclusion: the concept is **mobile-Safari-safe by construction** — every load-bearing technique is either universally available or has a cheap fallback; bleeding-edge features appear only as silent progressive enhancement.

---

## 9. Technical sketch

### 9.1 Structure

```
src/website/src/components/
  Slideshow.astro            ← thin dispatcher: validates props, picks variant template
  gallery/
    GalleryProduct.astro     ← stage + captions + thumbs/dots + lightbox markup
    GalleryStory.astro       ← stage + overlay + dots + play/pause
    gallery.css?             ← (optional) shared tokens; default: scoped styles per component
    gallery.ts               ← one initGallery(root) function, bundled+hoisted by Astro
```

### 9.2 Pseudo-markup (product variant)

```astro
<section
  class="gallery gallery--product"
  data-gallery data-variant="product" data-loop="wrap-buttons"
  aria-roledescription="Bildergalerie" aria-label="Produktfotos aus dem Hofladen"
>
  <div class="gallery__stage-wrap">
    <button class="gallery__nav gallery__nav--prev" aria-label="Vorheriges Bild">‹</button>

    <ul class="gallery__track" tabindex="0" aria-label="Bilder durchblättern">
      {slides.map((s, i) => (
        <li class="gallery__slide" role="group"
            aria-roledescription="Folie" aria-label={`${i + 1} von ${slides.length}`}>
          <Image src={s.src} alt={s.alt} widths={…} sizes={…}
                 loading={i === 0 ? 'eager' : 'lazy'}
                 fetchpriority={i === 0 ? 'high' : undefined} />
        </li>
      ))}
    </ul>

    <button class="gallery__nav gallery__nav--next" aria-label="Nächstes Bild">›</button>
    <p class="gallery__counter"><span aria-live="polite">3 von 6</span></p>
  </div>

  <div class="gallery__meta">
    <h3 class="gallery__title">Strickgabel aus Holz <span class="badge">Neu</span></h3>
    <p class="gallery__desc">Für knotenfreies Stricken ohne Zählen.</p>
  </div>

  <div class="gallery__thumbs">
    {slides.map((s, i) => (
      <button aria-label={`Zu Bild ${i + 1}: ${s.alt}`} aria-current={…}>
        <Image src={s.src} alt="" width={96} height={96} loading="lazy" />
      </button>
    ))}
  </div>

  <!-- Phase 2 -->
  <dialog class="gallery__lightbox" aria-label={`Bildansicht: ${slides[0].alt}`}> … </dialog>
</section>
```

Caption/title/badge blocks exist per slide but only the active one is displayed (`[data-active]` + CSS), keeping markup static and SEO-visible.

### 9.3 Script outline (~3 KB)

```ts
for (const root of document.querySelectorAll('[data-gallery]')) {
  const track = root.querySelector('.gallery__track');
  let index = 0, suppress = false;

  const goTo = (i, behavior = 'smooth') => {
    index = clamp(i); suppress = true;
    track.scrollTo({ left: index * track.firstElementChild.offsetWidth, behavior });
    render();                       // dots, thumbs, counter, caption, hash (replaceState)
  };

  track.addEventListener('scrollend', () => {   // fallback: debounce('scroll')
    if (suppress) return (suppress = false);
    goTo(Math.round(track.scrollLeft / slideWidth()), undefined);
  });
  // buttons, keydown (←/→/Home/End), thumb clicks, autoplay controller,
  // IO visibility gate, visibilitychange, reduced-motion check,
  // lightbox wiring (Phase 2), analytics dispatch (below), hash bootstrapping
}
```

Astro processes `<script>` into a single deduped module even when the component renders multiple times per page — one download, N initializations.

### 9.4 Analytics hooks (describe-only)

Extend the existing beacon pattern (`navigator.sendBeacon('/api/pageview', …)` in `Layout.astro`). The gallery only *dispatches DOM events*; a small listener (added alongside the pageview script) forwards them:

| Event | Payload | Fired when |
| --- | --- | --- |
| `slide_change` | `{ path, galleryId, variant, index, trigger: 'swipe'|'button'|'thumb'|'key'|'autoplay' }` | committed index change (post-`scrollend`) |
| `lightbox_open` / `lightbox_close` | `{ path, galleryId, index }` | dialog open/close |
| `gallery_autoplay_stop` | `{ path, galleryId }` | user interrupts autoplay |

Server side this maps to a future `events` column/partition on the existing `pageviews` table (dashboard-api) — out of scope here; the events are designed so the payload fits the current anonymous, session-id-based scheme with no new PII.

---

## 10. Rollout phases, effort, risks

### Phase 1 — Mobile-solid baseline (≈ 1–1.5 days)

Rewrite `Slideshow.astro` into variant architecture (dispatcher + two templates + shared script): snap track, aspect-ratio reservation, eager/lazy split with `widths/sizes`, product captions/badges, dots+fraction / thumbnails, buttons ≥ 44 px, keyboard, roving focus, reduced-motion, autoplay rules, `js`-gate for controls, German alt-text overhaul on both pages. Ship behind nothing — replaces the old component outright.

### Phase 2 — Inspection & sharing (≈ 1 day)

`<dialog>` lightbox with pinch-zoom viewport, separate high-res rendition on open, focus semantics, deep-link hashes (`#produkte-n`) with `replaceState`, hover-intent preloading, analytics event dispatch.

### Phase 3 — Polish (≈ 0.5–1 day)

Dominant-color placeholders (or LQIP), View Transition morph stage→lightbox where supported, optional `@supports` upgrade path to native `::scroll-marker` dots on Chromium, QA passes (iOS Safari 16–26, Android Chrome, Firefox desktop/mobile, VoiceOver/TalkBack/NVDA), Lighthouse/CWV verification against §7.2 targets.

**Total estimate: 2.5–3.5 focused days.**

### Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Mixed source aspect ratios break uniform stages | `contain` + colored backdrop (product) / `cover` (story); audit source images in Phase 1 |
| Older iOS (< 26.2) lacks `scrollend` | Debounced scroll fallback, feature-detected |
| Hash deep links fight browser scroll restoration | Instant-scroll on bootstrap, `replaceState` only, documented caveats (§6) |
| Autoplay annoyance/a11y complaints | Strict pause rules, permanent stop on interaction, play/pause button, reduced-motion off |
| `feature/slideshow` branch divergence | Close the branch after landing Phase 1; salvage its autoplay-keyframe idea only as a Phase 3 `@supports` experiment |
| Lightbox quirks on iOS Safari (`dvh`, rubber-band) | Dedicated device QA in Phase 2; zoom viewport isolates gestures |
| Sharp build-time cost from many renditions | Fixed width ladder (4 sizes), reused metadata via Astro's cached images |

---

## References

- Current component: `src/website/src/components/Slideshow.astro`
- Prototype under evaluation: `git show feature/slideshow:src/website/src/components/Slideshow.astro`
- Call sites: `src/website/src/pages/produkte.astro`, `src/website/src/pages/alpaka-wanderungen.astro`
- Design tokens: `src/website/src/styles/global.css`
- Beacon pattern to extend: `src/website/src/layouts/Layout.astro` (`sendBeacon('/api/pageview', …)`)
- Support data verified 2026-08-24 via caniuse/MDN/vendor notes: `scrollend` (Safari 26.2, Dec 2025 → Baseline Newly Available), same-doc View Transitions (Baseline Oct 2025), cross-doc VT (Firefox flag-gated), Popover API (Baseline widely available Apr 2025), Anchor Positioning (Baseline 2026, Firefox 147/Safari 26), `::scroll-button()/::scroll-marker()` (Chromium-only, others in progress)
