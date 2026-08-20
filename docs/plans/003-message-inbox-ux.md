# Message Inbox UX Plan

## Goal

Improve the message inbox in `src/dashboard/src/pages/messages.astro`: surface the
AI spam classification that is already stored on each row, add column sorting and
status filtering, and polish the table UX end to end. Built on top of the idea in
`docs/ideas/message-insights-dashboard.md`.

## Decisions

- **Spam surfacing:** `IsSpam` is already written by `SendMessage.cs`
  (website-api) into the `messages` table but was dropped by
  `GetMessages.cs` when mapping to `DashboardMessage`. Add the flag to the
  dashboard entity + DTO and render a "Spam" badge.
- **Sorting/filtering:** client-side. The inbox is small, the whole list is
  already fetched and rendered in the browser, and keeping it client-side avoids
  query-parameter plumbing in the Functions. Default order stays newest-first.
- **Overview stats:** a compact "X Nachrichten · Y Spam" summary derived from the
  loaded list — no extra endpoint.
- **Long messages:** clamped to two lines with a "Mehr/Weniger" toggle so rows
  stay compact.
- **Security fix:** rows are now built with `textContent` instead of the previous
  `innerHTML` interpolation, closing an XSS vector where public contact-form
  content could inject markup into the dashboard.

## Milestones (tracked)

- [x] Create git branch `feat/message-inbox-ux`
- [x] Write the plan (`docs/plans/003-message-inbox-ux.md`)
- [x] Backend: add `IsSpam` to `dashboard-api/shared/entities/MessageEntity.cs`
  and to `DashboardMessage`/handler in `features/messages/GetMessages.cs`
- [x] Frontend: rework `src/dashboard/src/pages/messages.astro` (row markers,
      sortable headers, Alle/Gesendet/Spam filter, summary line, expandable
      messages, visible loading/empty/error states)
- [x] Verify: `dotnet build` (both APIs), `pnpm run build` + `astro check`
- [ ] Deploy on `main` merge

## 1. Backend (`src/dashboard-api`)

- `shared/entities/MessageEntity.cs`: add `public bool IsSpam { get; set; }`
  (parallel to the website-api entity; existing rows deserialise to `false`).
- `features/messages/GetMessages.cs`: extend
  `DashboardMessage(..., bool IsSpam)` and map `m.IsSpam` in the handler.
- `requests.http` already covers `GET /api/messages`; no new endpoint needed.

## 2. Frontend (`src/dashboard/src/pages/messages.astro`)

State-driven rework: keep the fetched `Message[]` in memory plus
`filter`/`sortKey`/`sortDir`; a single `renderTable()` recomputes the visible,
sorted rows. Delete mutates the array and re-renders.

- **Row markers** instead of a Status column: a narrow leading column shows
  lucide icons at the top-left of matching rows — `ShieldAlert` (backstein) for
  messages classified as spam and `MailX` (red) for messages older than six
  months (same 30-day×6 threshold as `get-old-message-count`). Rows flagged as
  old also get a light red background.
- **Sortable headers** for `Name`, `Nachricht`, `Email`, `Telefon`, `Zeitpunkt`
  as `<button>` inside `<th>` with `aria-sort`; lucide
  `ArrowUp`/`ArrowDown`/`ArrowUpDown` icons cloned from `<template>` (same
  pattern as `EventList.astro`). Default: `Zeitpunkt` descending; first click on
  other columns sorts ascending.
- **Filter toggle**: segmented `Alle / Gesendet / Spam` with `aria-pressed`;
  per-filter empty text; `colspan="7"`.
- **Summary line**: `X Nachrichten · Y Spam` (or `kein Spam`), `aria-live`.
- **Message cell**: two-line clamp with `Mehr/Weniger` toggle (button only
  appears when the text overflows).
- **States**: loading row, per-filter empty rows, and a visible error row when
  the fetch fails (previously only `console.error`).
- Kept: sticky header, scoped `<style>`, WCAG AA contrast, responsive
  min-width + horizontal scroll, existing delete flow.

## 3. Verification

- `dotnet build src/dashboard-api/dashboard-api.csproj`
- `dotnet build src/website-api/website-api.csproj`
- `cd src/dashboard && pnpm run build` and `pnpm run check`
- Manual: fetch `/api/messages`, confirm `IsSpam` in the JSON; exercise
  sort/filter/badge/expand/delete in the browser.

## Known limitations / notes

- Old messages (stored before the spam filter shipped) carry no `IsSpam` column
  and show no spam marker; the age marker still applies based on `Timestamp`.
- No unit test project exists; verification relies on builds, `astro check`, and
  the `requests.http` samples.
