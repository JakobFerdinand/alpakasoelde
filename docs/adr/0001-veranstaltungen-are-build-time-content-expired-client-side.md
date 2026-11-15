# Veranstaltungen are build-time content, expired client-side

A Veranstaltung is one markdown file in `src/website/src/content/veranstaltungen/`, rendered statically. Because the site only rebuilds on deploy, an event that ends between two deploys would stay listed, so the markup carries each expiry as an ISO timestamp and a small script (`src/scripts/expiry.ts`) removes ended cards, the nav link and an emptied section in the visitor's browser. Ended events leave the generated HTML at the next build, whenever that happens.

## Considered options

- **Scheduled nightly rebuild**: rejected, it costs pipeline runs and maintenance for a farm that adds an event a few times a year.
- **Fetch events from an API at page load**: rejected, the public site is static and the content is hand-written markdown, not table data.

## Consequences

- With JavaScript disabled, an ended event stays listed until the next build. Accepted.
- Past event pages stay online so shared links keep working; they show a notice, are `noindex` and are dropped from the sitemap.
- "Upcoming" is defined once in `src/data/veranstaltung.ts` (`isUpcoming`); the sitemap filter in `astro.config.mjs` reads the event files itself because it runs before the content layer.
