# Glossary

- **Veranstaltung** — a dated one-off event on the farm (e.g. Alpaka Yoga), announced on the homepage and written as one markdown file in the website's content folder (see #536). Not a Dashboard event, which is a per-alpaka log entry in the `events` table.
- **Bevorstehend (upcoming)** — a Veranstaltung whose `end` (or, without `end`, the end of its start day in Europe/Vienna) is still in the future. Only upcoming ones appear on the homepage.
- **Vergangen (past)** — an ended Veranstaltung; its page stays online with a notice and `noindex`, and it is left out of the sitemap.
