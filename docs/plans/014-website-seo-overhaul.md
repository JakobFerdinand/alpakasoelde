# Website SEO Overhaul Plan

## Goal

Make `src/website` competitive for the local searches it should already win („Alpakawanderung Oberösterreich", „Alpakahof Innviertel", „Alpakaerlebnis Mining") by fixing the four things that measurably cost rankings and clicks today: every page shipping the same meta description, social previews that render blank because `og:image` is an SVG, ~1 MB of unoptimized hero JPEG sitting on the LCP path of four pages, and a homepage whose title and H1 spend their keyword budget on the word „Willkommen". Structured data grows from a stub into a complete `LocalBusiness`, and the crawl surface gets tidied. Purely a website change — no API, no infra, no storage, no privacy-policy implications (nothing new is collected or sent anywhere).

## Current state

- **Metadata is one hardcoded block for the whole site.** `src/components/Head.astro` takes only `title` and `robots`; the description (`:33`), `og:description` (`:44`) and `twitter:description` (`:51`) are literal strings, so all pages ship „Kleiner Alpakahof in Frauenstein, Oberösterreich. Alpakaerlebnisse und Wanderungen." There is no `description` prop to thread anything through.
- **`og:image` is a vector logo.** `Head.astro:47,53` import `AS_Symbolik_Schwarz.svg`, which is byte-identical to `favicon.svg`, so the build dedupes both to `/_astro/favicon.Dy8MDFby.svg`. Facebook, WhatsApp, LinkedIn and X do not rasterize SVG — every shared link is a blank card. `twitter:card` is `summary_large_image` (`:50`) while the asset is a small square mark.
- **Heroes bypass the image pipeline.** `Hero.astro:7` and `ServiceHero.astro:18` inject the raw ESM import into a CSS custom property (`--hero-bg-image: url(${image.src})`), so Sharp never touches them: no resize, no webp/avif, no `srcset`, no preload. `dist/_astro` holds 9 raw JPEGs against 16 optimized webp — `impression_7.jpg` **1019 KB** (the `/produkte` hero), `hero.jpg` **599 KB**, `impression_117.jpg` 679 KB, `impression_72.jpg` 514 KB. Each is the LCP element of its page. `astro.config.mjs` sets no `image` block, so `image.layout` is unset and `image.responsiveStyles` is off.
- **Homepage wastes its ranking signals.** `index.astro:14` passes `title="Willkommen"` → `<title>Willkommen | Alpakasölde</title>`; the only H1 is screen-reader-only „Alpakasölde" (`Hero.astro:9`). The suffix ` | Alpakasölde` is hardcoded in `Head.astro:31`, so a brand-carrying title would read „… | Alpakasölde | Alpakasölde".
- **Structured data is a stub.** `Head.astro:12-26` emits `LocalBusiness` with only `name`, `description`, `image` (the SVG), `addressCountry`/`addressRegion`, `areaServed`, `serviceType` and `priceRange`. The data it omits is already in the repo: `impressum.astro:9-14` has „Frauenstein 12, 4962 Mining", `+43 699 81375946`, `kontakt@alpakasoelde.at`; `Contact.astro:47` embeds the OSM marker `48.28570595443409, 13.167816996574402`. `serviceType` is not a valid `LocalBusiness` property (it belongs to `Service`). The block repeats on every page with no `@id`.
- **Crawl surface.** `robots.txt` allows everything and points at `sitemap-index.xml`; `astro.config.mjs` filters `/403/` and `/nachricht-gesendet/` out of the sitemap, but neither they nor `404` carry `noindex` — verified in `dist`, all three ship `content="index, follow"`. `Head.astro:7` *has* a `robots` prop and no page or layout ever passes it. Sitemap entries have no `lastmod`.
- **Internal linking is thin.** `Navbar.astro:34-37` links only `/` plus four homepage anchors, so `/produkte`, `/alpaka-wanderungen` and `/wollverarbeitung` receive internal links exclusively from the three cards in `Services.astro`. `/terrapreta` was orphaned entirely (sitemap-only, zero inbound links) and **has been deleted in this change** as obsolete; `WorkshopLayout.astro` stays for the `_yoga.astro` draft.
- **Headings.** `impressum`, `datenschutzerklaerung`, `404`, `403` and `nachricht-gesendet` have no H1 and open at `<h2>`. `produkte.astro:20,28` renders H1 „Produkte" immediately followed by H2 „Produkte"; `wollverarbeitung.astro:13,21` does the same with „Wollverarbeitungs-Workshop" / „Wollverarbeitung". `alpaka-wanderungen.astro:21,31` jumps H1 → H3.
- **Image alt text.** Good on content images, but `Slideshow.astro:16` hardcodes „Alpaka auf der Alpakasölde Farm" for all six *product* photos on `/produkte` (Strickgabel, Wollpellets, Karten, Polster …) — wrong text and forfeited image-search traffic.
- **What is already correct and must not regress:** `site: 'https://alpakasoelde.at'`, self-referencing canonicals (`Head.astro:36`) — checked live, `/produkte`, `/produkte/` and `www.` all canonicalize to `https://alpakasoelde.at/produkte/`, which is what absorbs the duplicate URL forms; `lang="de"`; the skip link; `is:inline set:html={JSON.stringify(...)}` on the JSON-LD (per `AGENTS.md`, anything else escapes the payload into invalid structured data).
- **Tests that constrain this work:** `test/head.test.ts` renders `Head` through the Astro Container API and asserts the JSON-LD parses, `@type === 'LocalBusiness'`, `image` starts with `site`, plus the canonical and title strings. `e2e/home.spec.ts:6-7` asserts `toHaveTitle('Willkommen | Alpakasölde')` and an H1 named exactly „Alpakasölde" — **both break on purpose in section 4** and must be updated in the same commit. Both deploy workflows run `pnpm test` and `pnpm run test:e2e`, so a stale assertion blocks the deploy.

## Decisions

- **`description` becomes a required prop, not an optional one.** `Head.astro`, `Layout.astro` and `WorkshopLayout.astro` all declare it non-optional so `pnpm run check` fails on a page that forgets it. A default would silently reintroduce exactly the duplicate-description problem this plan exists to fix.
- **One `description` feeds all three tags** (meta, `og:`, `twitter:`). Per-tag overrides are speculative complexity for a nine-page site.
- **Titles get an explicit escape hatch.** `Head.astro` keeps appending ` | Alpakasölde` but gains a `titleTemplate: 'full' | 'suffix'` style opt-out — implemented as an optional `brandSuffix = true` prop — so the homepage can ship a self-contained title without the doubled brand.
- **The OG image is a committed static file, not a generated one.** `public/og-default.jpg` at 1200×630, referenced as `new URL('/og-default.jpg', Astro.site)`. Astro's `@astrojs/og-image`-style runtime generation would need an SSR adapter; this site is static on SWA. Per-page override via an optional `ogImage` prop for later, defaulting to the shared file. `twitter:card` stays `summary_large_image` because the asset now matches it.
- **Heroes move to `<Image>`, not to an optimized CSS `url()`.** Astro 7's `layout="full-width"` + `priority` + `fit="cover"` + `position="center"` emits the `srcset`, the `sizes`, the eager `fetchpriority="high"` and the intrinsic dimensions in one go; `getImage()` in the CSS path would still leave the LCP image undiscoverable by the preload scanner. This means enabling `image.responsiveStyles: true` in `astro.config.mjs` (needed for the layout styles to be injected) and restructuring `.hero` into a positioned container with an absolutely-placed image behind the content — the dark `linear-gradient` overlay becomes an `::after` layer instead of part of `background-image`.
- **`priority` on exactly one image per page** — the hero. Everything else keeps Astro's default lazy loading.
- **Structured data is emitted once, from the homepage.** `Head.astro` gains a `structuredData` prop; `index.astro` passes the full `LocalBusiness`, other pages pass nothing. A stable `@id` of `https://alpakasoelde.at/#business` is set so future `Event`/`Offer` nodes can reference it via `location`. Repeating an identical business node on nine pages gives Google nothing and multiplies the maintenance cost of one address change.
- **The `LocalBusiness` address uses the real postal address** — `streetAddress: 'Frauenstein 12'`, `postalCode: '4962'`, `addressLocality: 'Mining'` — and keeps „Frauenstein" as the hamlet in `description`/copy only. The current schema says „Frauenstein, Oberösterreich" with no locality, which does not match any postal record. **This must be reconciled against the Google Business Profile before shipping** (section 5) — a schema/GBP mismatch is worse for local ranking than the incomplete schema is today.
- **`serviceType` is replaced by `hasOfferCatalog`** with an `OfferCatalog` of `Offer`/`Service` items, which is the property Google actually reads. `openingHours` is deliberately **omitted** rather than guessed — the farm runs on appointment, so a fabricated schedule would earn „geschlossen" labels in the local pack. `sameAs` is left as a TODO pending the actual social URLs.
- **`meta keywords` is removed.** Ignored by every major engine since ~2009; keeping it invites the misconception that it does something.
- **`noindex` is applied via the prop that already exists.** `404`, `403` and `nachricht-gesendet` pass `robots="noindex, follow"`. The `astro.config.mjs` `notIndexable` set stays as the second layer — sitemap exclusion and `noindex` solve different halves (discovery vs. indexing).
- **Sub-pages join the navbar.** „Wanderungen", „Wollverarbeitung" and „Produkte" become real nav entries next to the anchors. This is the cheapest available fix for internal link equity and it also removes the trap where a visitor on `/produkte` sees a nav consisting entirely of links back to the homepage.
- **`Event` and `FAQPage` schema are explicitly out of scope** for this plan. `/terrapreta` is gone, `_yoga` is an unrouted draft, and there is no routed page with real dates — `Event` markup without them would be invalid. Noted as follow-up in section 8.
- **No `www` → apex 301.** The correct canonicals already consolidate the duplicate, and adding a redirect means touching `staticwebapp.config.json`, whose `/*` route rule is load-bearing for EasyAuth roles. Out of scope; recorded in section 8.
- **Content depth is out of scope but flagged.** The service pages run three paragraphs each; no metadata fix compensates for that against competitors with prices, duration, directions and FAQs. Section 8.

## Milestones (tracked)

- [x] Delete the obsolete `/terrapreta` page
- [x] Write the plan (`docs/plans/014-website-seo-overhaul.md`)
- [x] 1. Per-page descriptions + `Head.astro` prop surface (`description`, `ogImage`, `brandSuffix`, `structuredData`)
- [x] 2. `public/og-default.jpg` 1200×630 + OG/Twitter tag rework
- [x] 3. Hero images through `<Image>` with `layout="full-width"` + `priority`
- [x] 4. Homepage title/H1 rewrite — **also updates `e2e/home.spec.ts`**
- [x] 5. Full `LocalBusiness` JSON-LD, homepage-only — **GBP reconciliation still outstanding**
- [x] 6. `noindex` on 404/403/nachricht-gesendet; drop `meta keywords`; sitemap `lastmod`
- [x] 7. Navbar sub-page links; heading hierarchy; per-image `Slideshow` alt text
- [x] 8. Verify: `astro check` 0 errors, 12 Vitest, 3 Playwright, clean build
- [ ] Confirm the schema against the Google Business Profile, then deploy and run the post-deploy checks in section 8

## As built — where this diverged from the plan

Implemented on `feat/website-seo-overhaul`, one commit per section.

- **`ImpressionBreak.astro` had the same defect as the heroes** and was converted alongside them. The plan named only `Hero`/`ServiceHero`, but `ImpressionBreak` also built a CSS background from the raw import, and it accounted for six of the nine unoptimized JPEGs. Converting it drops the `role="img"` wrapper and its sr-only caption, since the alt text now sits on a real `img`.
- **`fit`/`position` could not be passed to `<Image>`.** Astro hands `position` straight to Sharp as a crop gravity, which accepts only named values — the percentage offsets these images are framed with (`center 40%`) fail the build with `CouldNotTransformImage`. The framing stays in CSS as `object-position`, which is what it was as `background-position` anyway; `object-fit: cover` in the component stylesheet does the cropping.
- **`dist/_astro` grew from 8.2 MB to 12 MB**, the opposite direction from the plan's expectation, because `layout="full-width"` writes every srcset variant to disk. Delivered bytes are what dropped: the `/produkte` hero now offers 95 KB (640w) through 747 KB (2048w) in place of a single 1019 KB JPEG, so a 390 px phone pulls 155 KB where it used to pull the lot.
- **`#alpakas` had no heading at all**, which the plan's heading survey missed — every sibling section carries an h2 and the navbar links to it as "Alpakas". Added "Unsere Alpakas".
- **`.section h1` had to be added to `global.css`.** Promoting the legal/error/confirmation pages from h2 to h1 would otherwise have dropped them out of `.section h2`'s centring and sizing and silently restyled five pages.
- **Two files were touched that the plan did not anticipate:** `src/styles/global.css` (the rule above) and `src/pages/_yoga.astro` (the draft needs a `description` like every other page, or `astro check` fails once it is routed).
- **Formatting:** every file touched here already failed `prettier --check` before this branch (confirmed against `HEAD~1`), consistent with the note in `AGENTS.md` that the tree was never reformatted. Running `--write` over them would bury these changes in whole-file reflows, so the new code matches its surrounding style instead.

## 1. Per-page descriptions

`src/components/Head.astro` — widen `Props`:

```ts
interface Props {
  title: string;
  description: string;
  robots?: string;
  ogImage?: ImageMetadata | string;
  brandSuffix?: boolean;
  structuredData?: Record<string, unknown>;
}
```

`title` and `description` lose their defaults. Replace the three literal description strings (`:33`, `:44`, `:51`) with `{description}`. Both layouts (`Layout.astro:11-16`, `WorkshopLayout.astro:9-13`) take `description` and forward it verbatim.

Then write one per page, 140–160 characters, each naming a distinct search intent and none repeating another. Draft copy:

| Page | Description |
| --- | --- |
| `index` | „Kleiner Alpakahof in Frauenstein bei Mining, Oberösterreich: Alpakawanderungen, Hofbesuche und Wolle direkt vom Tier. Jetzt Termin anfragen." |
| `alpaka-wanderungen` | „Geführte Alpakawanderung im Innviertel: gemütliche Touren mit unseren Alpakas, inklusive Hofbesuch. Dauer, Preise und Ausflugstipps im Überblick." |
| `wollverarbeitung` | „Wollworkshop am Alpakahof: erlebe Kardieren und Spinnen am Spinnrad und verfolge, was mit der Alpakawolle nach dem Scheren passiert." |
| `produkte` | „Alpakawolle, handgefertigte Accessoires, Pölster mit Alpakafüllung und Wollpellets als Dünger — direkt aus dem Hofladen der Alpakasölde." |
| `impressum` | „Impressum der Alpakasölde: Anbieterkennzeichnung, Kontaktdaten und Verantwortliche für alpakasoelde.at." |
| `datenschutzerklaerung` | „Datenschutzerklärung der Alpakasölde: welche Daten das Kontaktformular verarbeitet, wie die Besuchsstatistik funktioniert und welche Rechte du hast." |
| `nachricht-gesendet` | „Deine Nachricht an die Alpakasölde ist angekommen — wir melden uns so schnell wie möglich zurück." |
| `404` / `403` | Short, page-specific, both `noindex` anyway. |
| `_yoga` | Draft; give it one so it does not fail `check` when it is eventually routed. |

Extend `test/head.test.ts` with a case that a passed `description` reaches all three tags, and a case that two different descriptions produce two different outputs (guards against a future reintroduced default).

## 2. Social preview image

Author `public/og-default.jpg`, 1200×630, JPEG, under ~200 KB: a photo (`hero.jpg` or one of the `impressions/`) with the wordmark legible at Slack/WhatsApp thumbnail size. It goes in `public/` rather than `src/images/` so the URL is stable and hash-free across builds — shares already in the wild keep resolving.

In `Head.astro`, resolve `ogImage` (default `'/og-default.jpg'`) through `new URL(..., Astro.site)` and emit:

```astro
<meta property="og:image" content={ogImageUrl} />
<meta property="og:image:width" content="1200" />
<meta property="og:image:height" content="630" />
<meta property="og:image:alt" content="Alpakas auf der Weide der Alpakasölde" />
<meta property="og:locale" content="de_AT" />
```

`twitter:image` gets the same URL. Drop the `Logo` import at `Head.astro:3` once nothing references it. Verify with the Facebook Sharing Debugger and `https://cards-dev.twitter.com/validator` **after** deploy — the crawlers fetch the live URL, so this cannot be checked locally.

## 3. Hero images (the LCP fix)

The measurable win: ~1 MB off four pages. Enable the responsive-image styles in `astro.config.mjs`:

```js
image: { responsiveStyles: true },
```

Restructure `ServiceHero.astro` — the image becomes real markup behind positioned content:

```astro
<section class={`hero ${className}`}>
  <Image
    src={image}
    alt={imageAlt}
    layout="full-width"
    priority
    fit="cover"
    position="center"
    class="hero__bg"
  />
  <div class="hero-content">
    <h1>{title}</h1>
    <p>{description}</p>
    <slot />
  </div>
</section>
```

- `.hero` gets `position: relative; isolation: isolate;` and keeps its `min-height`/padding.
- `.hero__bg` gets `position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; z-index: -2;`.
- The `rgba(0,0,0,0.35)` overlay currently baked into `background-image` moves to `.hero::after { content: ''; position: absolute; inset: 0; background: rgba(0,0,0,0.35); z-index: -1; }`.
- `image` stays an `ImageMetadata` prop; a new **required** `imageAlt` prop replaces the empty-alt-by-omission that a background image implied. The hero is the page's main visual — it deserves real alt text.

`Hero.astro` gets the same treatment for `hero.jpg` (`:7`). Its `--hero-bg-image` custom property and the `margin-top: calc(-1 * var(--nav-height))` interaction with the transparent navbar both need a visual check at 390 px and 1440 px — that negative margin plus a `position: absolute` child is where this refactor is most likely to go wrong.

Confirm afterwards that `dist/_astro` contains **no** raw `impression_*.jpg` / `hero.jpg` and that the hero `<img>` carries `srcset`, `sizes`, `fetchpriority="high"`, `width` and `height`.

## 4. Homepage title and H1

`index.astro:14`:

```astro
<Layout
  title="Alpakahof & Alpakawanderungen in Frauenstein, Oberösterreich"
  description="…"
  brandSuffix={false}
  …
>
```

`brandSuffix={false}` makes `Head.astro` render the title as-is; „Alpakasölde" is already the site's `og:site_name` and appears in the brand-name query anyway, and the string is at the 60-character budget without a doubled brand.

`Hero.astro:9` — the H1 stays visually hidden (the wordmark image is the intended visual) but gains the keyword: `<h1 class="sr-only">Alpakasölde — Alpakahof in Frauenstein, Oberösterreich</h1>`. Alternative worth considering during implementation: make it visible and drop the wordmark `<Image>`, which would let the H1 double as the LCP text element.

**This breaks `e2e/home.spec.ts:6-7`** (`toHaveTitle('Willkommen | Alpakasölde')`, H1 named `'Alpakasölde'`). Update both assertions in the same commit; prefer a substring/regex matcher for the H1 so future copy tweaks do not break the test again.

## 5. Structured data

`Head.astro` renders `structuredData` only when the prop is present. `index.astro` passes:

```js
const business = {
  "@context": "https://schema.org",
  "@type": "LocalBusiness",
  "@id": "https://alpakasoelde.at/#business",
  name: "Alpakasölde",
  description: "Kleiner Alpakahof in Frauenstein bei Mining, Oberösterreich",
  url: "https://alpakasoelde.at",
  telephone: "+43 699 81375946",
  email: "kontakt@alpakasoelde.at",
  image: new URL("/og-default.jpg", Astro.site).toString(),
  address: {
    "@type": "PostalAddress",
    streetAddress: "Frauenstein 12",
    postalCode: "4962",
    addressLocality: "Mining",
    addressRegion: "Oberösterreich",
    addressCountry: "AT",
  },
  geo: { "@type": "GeoCoordinates", latitude: 48.285706, longitude: 13.167817 },
  areaServed: { "@type": "AdministrativeArea", name: "Oberösterreich" },
  priceRange: "€€",
  hasOfferCatalog: {
    "@type": "OfferCatalog",
    name: "Angebote",
    itemListElement: [
      { "@type": "Offer", itemOffered: { "@type": "Service", name: "Alpakawanderungen", url: "https://alpakasoelde.at/alpaka-wanderungen/" } },
      { "@type": "Offer", itemOffered: { "@type": "Service", name: "Wollverarbeitungs-Workshop", url: "https://alpakasoelde.at/wollverarbeitung/" } },
      { "@type": "Offer", itemOffered: { "@type": "Service", name: "Hofladen & Alpakaprodukte", url: "https://alpakasoelde.at/produkte/" } },
    ],
  },
};
```

Coordinates rounded to 6 decimals (~11 cm — the current 14 decimals in `Contact.astro:47` are false precision). Keep `is:inline set:html={JSON.stringify(...)}` exactly as `AGENTS.md` requires.

**Blocking check before merge:** confirm the Google Business Profile's address matches „Frauenstein 12, 4962 Mining" and that `name`, `telephone` and `url` are character-identical to it. If GBP says something else, GBP is the source of truth and this object follows it — not the reverse. Also decide whether `openingHours` should say `"By appointment"` via `openingHoursSpecification` or stay absent; absent is the safe default.

`test/head.test.ts` needs updating on two counts: the existing JSON-LD test renders `Head` at `/` **without props**, so it will find no JSON-LD once emission is prop-driven — pass a `structuredData` fixture, and add a case asserting that a page *without* the prop emits none.

## 6. Crawl hygiene

- `404.astro`, `403.astro`, `nachricht-gesendet.astro`: pass `robots="noindex, follow"` through the layout to `Head`. `Layout.astro` needs to forward the prop — it currently does not, which is why `Head.astro:7` has been dead code since it was written.
- Delete `Head.astro:34` (`meta keywords`).
- `astro.config.mjs`: add `lastmod: new Date()` to the `sitemap()` options. Build-time timestamps are honest here — a static build genuinely republishes every page.
- Re-verify after build that `dist/404.html`, `dist/403/index.html` and `dist/nachricht-gesendet/index.html` all contain `content="noindex, follow"`, and that `sitemap-0.xml` still lists exactly the six indexable routes (`/`, `/alpaka-wanderungen/`, `/produkte/`, `/wollverarbeitung/`, `/impressum/`, `/datenschutzerklaerung/`) now that `/terrapreta/` is gone.

## 7. Structure and alt text

- `Navbar.astro:34-37`: add „Wanderungen" → `/alpaka-wanderungen`, „Wollverarbeitung" → `/wollverarbeitung`, „Produkte" → `/produkte`. Seven items will not fit the desktop bar at its current sizing — either shorten the anchors („Alpakas", „Über uns", „Kontakt" stay; „Leistungen" can go, since the three service pages now appear directly) or accept a tighter gap. Check the 768 px burger breakpoint; `e2e/navbar.spec.ts` exercises the open/navigate/close cycle and should keep passing, but re-run it.
- Headings: give `impressum`, `datenschutzerklaerung`, `nachricht-gesendet`, `404` and `403` an `<h1>` in place of their opening `<h2>`. On `produkte.astro:28` and `wollverarbeitung.astro:21`, replace the H2 that duplicates the hero H1 with one that describes the section („Aus unserem Hofladen", „Der Workshop im Detail"). On `alpaka-wanderungen.astro:31`, promote „Details" and „Ausflugstipps" from H3 to H2.
- `Slideshow.astro`: change `Props.images` from `any[]` to a `{ src: ImageMetadata; alt: string }[]` and render `alt={image.alt}`. Update both call sites (`produkte.astro:15`, and the `alpaka-wanderungen`/other usages) with real per-photo German alt text — „Strickgabel aus Holz", „Wollpellets als Naturdünger", „Handgefertigte Grußkarten mit Alpakamotiv" and so on. This also removes the `as Props` cast at `Slideshow.astro:8`.

## 8. Verification and explicit non-goals

Run, in order: `pnpm run check` (the only thing that catches template/type errors — `build` will not), `pnpm test`, `pnpm run test:e2e`, `pnpm run build`. Then compare `du -sh dist/_astro` against the 8.2 MB baseline and confirm no raw hero JPEG survives. Format only touched files with `pnpm exec prettier --write <paths>` — never `pnpm run format` (`AGENTS.md`).

Post-deploy, on the live origin: Rich Results Test for the `LocalBusiness`, Facebook Sharing Debugger for `og:image`, PageSpeed Insights on `/` and `/produkte` for the LCP delta, and a Search Console re-submit of the sitemap.

Deliberately **not** in this plan:

- `Event` / `Course` schema — no routed page carries real dates now that `/terrapreta` is deleted; revisit when `_yoga` or a successor workshop page ships.
- `FAQPage` on `alpaka-wanderungen` — the „Details" block is FAQ-shaped and is the obvious next rich-result win, but it wants real Q&A copy first.
- `www` → apex 301 in `staticwebapp.config.json` — canonicals already consolidate it and that file's `/*` rule is load-bearing for EasyAuth.
- Content depth. Three paragraphs per service page is the largest remaining gap against competitors, and no amount of metadata substitutes for prices, duration, what to bring, and directions. This is a copywriting task, not an engineering one, and it deserves its own plan.
