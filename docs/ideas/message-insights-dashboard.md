# Spam marking & message overview

The AI spam filter already stores `IsSpam` on each entry in the `messages` table, but the dashboard never surfaces it: `GetMessages.cs` maps `MessageEntity` to `DashboardMessage` without `IsSpam` (src/dashboard-api/features/messages/GetMessages.cs:33), so `messages.astro` has no way to tell spam from legit mail.

Idea: surface the flag and count the volume.

- **Spam marking:** include `IsSpam` in the `DashboardMessage` record and render it in `messages.astro` (e.g. a "Spam" badge/badge column), so a message classified as spam by the AI filter is visibly marked. Perhaps a spam filter toggle to show/hide spam rows.
- **Message overview:** add a stats view (on `messages.astro` or a small overview page/section) showing how many messages arrived in total, how much of them are spam and how many are legit — supported by an endpoint that returns counts (e.g. `GET /api/messages/counts` or derived from `get-messages` + `get-old-message-count`).

Keep it a concrete few sentences so a plan can be drafted from it.

---

## Status: implemented in `docs/plans/003-message-inbox-ux.md`

Both ideas landed in the dashboard inbox rework (`feat/message-inbox-ux`):

- **Spam marking:** `IsSpam` is surfaced through `DashboardMessage` and rendered
  as a badge; the table also gained an `Alle / Gesendet / Spam` filter and
  sortable columns.
- **Message overview:** the summary line "X Nachrichten · Y Spam" is derived
  from the loaded list, so no separate counts endpoint was needed.
