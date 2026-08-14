# Spam marking & message overview

The AI spam filter already stores `IsSpam` on each entry in the `messages` table, but the dashboard never surfaces it: `GetMessages.cs` maps `MessageEntity` to `DashboardMessage` without `IsSpam` (src/dashboard-api/features/messages/GetMessages.cs:33), so `messages.astro` has no way to tell spam from legit mail.

Idea: surface the flag and count the volume.

- **Spam marking:** include `IsSpam` in the `DashboardMessage` record and render it in `messages.astro` (e.g. a "Spam" badge/badge column), so a message classified as spam by the AI filter is visibly marked. Perhaps a spam filter toggle to show/hide spam rows.
- **Message overview:** add a stats view (on `messages.astro` or a small overview page/section) showing how many messages arrived in total, how much of them are spam and how many are legit — supported by an endpoint that returns counts (e.g. `GET /api/messages/counts` or derived from `get-messages` + `get-old-message-count`).

Keep it a concrete few sentences so a plan can be drafted from it.