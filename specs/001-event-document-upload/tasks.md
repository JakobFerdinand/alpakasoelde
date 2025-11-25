# Tasks: Event Document Upload

**Input**: Design documents from `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/`
**Prerequisites**: `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/plan.md`, `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/spec.md`, `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/data-model.md`, `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/research.md`, `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/contracts/`, `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md`

---

## 1. Phase 1: Setup (Infrastructure & Docs)
**Purpose**: Prepare local tooling, documentation, and request samples so developers can exercise the new slice end-to-end.
**Workstreams**: Infrastructure, QA/Docs

- [ ] T001 Update `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md` with the storage prerequisites (Azurite container, `DOCUMENT_SCAN_QUEUE`) and the dual-terminal command matrix for `npm run dev` + `dotnet run`; Acceptance: following the doc boots both servers without missing env variables.
- [ ] T002 Add Document Upload configuration keys (blob connection, `DocumentScanQueue`, `ProspectiveEventId` hint) to `/home/jakob/private/alpakasoelde/src/dashboard-api/local.settings.json` and `/home/jakob/private/alpakasoelde/src/dashboard-api/shared/EnvironmentVariables.cs`; Acceptance: `dotnet run` throws a clear error if any key is absent and loads successfully once populated.
- [ ] T003 [P] Append REST samples for upload token, finalize, list, download-link, and remove endpoints to `/home/jakob/private/alpakasoelde/src/dashboard-api/requests.http`; Acceptance: each request returns the response code described in contracts once the APIs exist.

---

## 2. Phase 2: Foundational (Shared Backend Plumbing)
**Purpose**: Stand up shared infrastructure, entities, and services required by all stories before UI or endpoint work begins.
**Workstreams**: Infrastructure, Backend API

- [ ] T004 Author `/home/jakob/private/alpakasoelde/infrastructure/storage/event-documents.bicep` to provision the `event-documents` container (hot + archive tiers), `EventDocuments` and `EventDocumentAudit` tables, and an Event Grid subscription that forwards blob-created events to the malware scanning queue; Acceptance: `az deployment sub what-if` shows the new resources plus 90-day archive retention settings only.
- [ ] T005 [P] Reference the new storage module from `/home/jakob/private/alpakasoelde/infrastructure/table-storage.bicep` (or the root deployment) and thread parameters/secrets used by CI; Acceptance: `bicep build /home/jakob/private/alpakasoelde/infrastructure/table-storage.bicep` succeeds and pipelines receive the module outputs.
- [ ] T006 [P] Create `/home/jakob/private/alpakasoelde/src/dashboard-api/shared/entities/EventDocumentEntity.cs`, `DocumentAuditEntry.cs`, and `DocumentUploadSessionEntity.cs` capturing every field from `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/data-model.md` (UploadStatus, ScanStatus, ArchivePurgeAt, TTL, etc.); Acceptance: `dotnet build` exposes these records to other slices without namespace warnings.
- [ ] T007 Update `/home/jakob/private/alpakasoelde/src/dashboard-api/Program.cs` (optionally via a new `features/events/documents/DocumentsModule.cs`) to register blob/table clients, repositories, and validators for the documents slice; Acceptance: the Functions host resolves `IEventDocumentStore`, `IDocumentAuditStore`, and `IDocumentUploadSessionStore` without DI errors.
- [ ] T008 Implement repositories in `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/Stores/` that encapsulate blob naming (`{eventId}/{documentId}_{slug}`), size/type enforcement, and audit writes; Acceptance: invoking the stores in an integration harness creates Table rows keyed by `EventId`/`DocumentId`.
- [ ] T009 [P] Add `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/Services/DocumentUploadSessionService.cs` to persist prospective Event IDs with a 15-minute TTL and enforce max-5 queue rules server-side; Acceptance: attempting to mint a 6th token in the same session returns HTTP 400 and expired rows disappear automatically.

---

## 3. Phase 3: User Story 1 – Attach invoices during event creation (Priority: P1) 🎯 MVP
**Goal**: Allow event creators to queue up to five compliant files before saving an event, upload them directly to blob storage, and persist metadata atomically.
**Independent Test**: Start a new event, add ≤5 valid files, submit, and verify they appear against the event with correct metadata and blob references.
**Workstreams**: Frontend, Backend API, QA/Docs

- [ ] T010 [P] [US1] Implement ProspectiveEventId + `DocumentUploadSession` utilities in `/home/jakob/private/alpakasoelde/src/dashboard/src/lib/documents/session-store.ts` to mint GUIDs, mirror queued files in `localStorage`, and replay pending uploads after reconnect; Acceptance: refreshing mid-upload keeps ≤5 queued files without duplicates.
- [ ] T011 [P] [US1] Create `/home/jakob/private/alpakasoelde/src/dashboard/src/lib/documents/file-validation.ts` to enforce MIME sniffing, size (≤25 MB), and total count validations before contacting the API; Acceptance: selecting a 30 MB file surfaces an inline error and no network request fires.
- [ ] T012 [P] [US1] Build `/home/jakob/private/alpakasoelde/src/dashboard/src/components/events/DocumentsPanel.astro` that renders queued files with size/type/remove controls, shows progress/errors, and exposes retry buttons for storage outages; Acceptance: users cannot queue more than five files and duplicate filenames display unique suffix hints.
- [ ] T013 [US1] Create or extend `/home/jakob/private/alpakasoelde/src/dashboard/src/pages/events/new.astro` so the event creation form initializes a `DocumentUploadSession`, blocks submit until all uploads reach Uploaded, and surfaces size/type violations inline; Acceptance: manual QA can create an event with five files and see each listed on the confirmation screen.
- [ ] T014 [P] [US1] Add `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/RequestUploadTokensFunction.cs` to validate permissions, enforce Locked-state blocks, and issue per-file SAS URLs targeting `event-documents/{eventId}/{documentId}_{slug}` for ≤15 minutes; Acceptance: `POST /events/{eventId}/documents/uploads` returns 200 with tokens for valid payloads and 423 when the event is locked.
- [ ] T015 [P] [US1] Implement `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/FinalizeEventDocumentsFunction.cs` that verifies blob existence, stamps checksums/descriptions, sets `UploadStatus=Uploaded`, and records `UploadCompleted` audit entries atomically; Acceptance: duplicate filenames persist by appending GUID suffixes while `OriginalName` remains unchanged.
- [ ] T016 [US1] Create `/home/jakob/private/alpakasoelde/src/dashboard/src/lib/documents/upload-client.ts` to stream blobs via SAS (with chunk retries/backoff), catch connectivity loss, and queue finalize calls so saving waits for storage success; Acceptance: simulating offline mode causes the client to pause uploads and resume without losing form state.
- [ ] T017 [US1] Extend `/home/jakob/private/alpakasoelde/src/dashboard/src/styles/global.css` with document panel tokens (progress bars, badge colors, error states) using root design variables; Acceptance: `npm run build` finishes without unused-style warnings and components meet contrast requirements.
- [ ] T018 [US1] Document the “Attach invoices during event creation” manual flow inside `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md` (steps for attaching, removing, and saving); Acceptance: QA can follow the checklist to satisfy acceptance scenarios 1–3 from `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/spec.md`.

**Parallel Execution Examples (US1)**
- Run T010 and T011 simultaneously because they touch separate lib files (session vs validation).
- Develop T014 (SAS issuance) and T015 (finalize) in parallel with T012 (UI) since backend HTTP stubs are contract-driven.

---

## 4. Phase 4: User Story 2 – Manage attachments for an existing event (Priority: P2)
**Goal**: Let editors add new documents or remove existing ones after an event is created, complete with filtering and audit visibility.
**Independent Test**: Open an existing event, add a file, remove another, and confirm metadata updates instantly without touching the base event record.
**Workstreams**: Frontend, Backend API, QA/Docs

- [ ] T019 [P] [US2] Build `/home/jakob/private/alpakasoelde/src/dashboard/src/components/events/DocumentManager.astro` that lists attachments with filters (type/uploader), shows scan/upload badges, and exposes remove actions gated by permission; Acceptance: toggling filters updates the list client-side without a full reload.
- [ ] T020 [US2] Extend `/home/jakob/private/alpakasoelde/src/dashboard/src/pages/events/[id].astro` to mount `DocumentManager`, allow new uploads when status < Locked, and disable inputs otherwise while leaving downloads enabled; Acceptance: loading a Locked event hides upload controls yet still shows document metadata.
- [ ] T021 [P] [US2] Implement `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/ListEventDocumentsFunction.cs` that queries Table Storage with optional `contentType`/`uploader` filters and returns uploader info plus latest audit summary; Acceptance: `GET /events/{eventId}/documents` responds within 200 ms locally and respects filters.
- [ ] T022 [P] [US2] Create `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/RemoveEventDocumentFunction.cs` to verify editor permissions, move the blob to Archive tier, stamp `ArchivePurgeAt = UtcNow + 90d`, and append `Removed` + `DownloadBlocked` audits; Acceptance: DELETE returns 202 and the blob’s tier shows Archive in Storage Explorer.
- [ ] T023 [P] [US2] Add `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/DocumentAuditTimelineFunction.cs` (and a small client hook) to fetch audit entries ordered desc so the UI timeline displays who performed each action; Acceptance: `GET /events/{eventId}/documents/{documentId}/audit` returns Upload/Remove events with actor IDs.
- [ ] T024 [US2] Expand `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md` with a “Manage attachments post-create” section describing add/remove/archive verification plus audit checks; Acceptance: QA can follow it to confirm archive scheduling and audit timeline visibility.

**Parallel Execution Examples (US2)**
- Develop T019 (UI) and T021 (list endpoint) concurrently since UI can mock API responses using `/home/jakob/private/alpakasoelde/src/dashboard-api/requests.http`.
- Run T022 (remove function) in parallel with T023 (audit endpoint) because they touch different API handlers and tables.

---

## 5. Phase 5: User Story 3 – Review and download attachments (Priority: P3)
**Goal**: Enable viewers to see attachment metadata and download files once scans succeed while gracefully blocking unauthorized or quarantined content.
**Independent Test**: Log in as a read-only reviewer, open an event, download each clean file (<10 MB), and confirm access is denied when permissions or scan status fail.
**Workstreams**: Frontend, Backend API, QA/Docs

- [ ] T025 [P] [US3] Implement `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/RequestDocumentDownloadLinkFunction.cs` that enforces view permissions, checks `ScanStatus=Clean`, issues short-lived read SAS URLs, and logs `DownloadRequested`/`DownloadBlocked` audits; Acceptance: POST `/events/{eventId}/documents/{documentId}/download-link` returns 200 only when scan status is Clean and 403 otherwise.
- [ ] T026 [P] [US3] Add `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/ScanResultHandlerFunction.cs` to process malware scan Event Grid messages, update `ScanStatus` + `ScanCompletedAt`, and write `ScanPassed`/`ScanFailed` audit entries; Acceptance: replaying a sample Event Grid payload flips the corresponding document status in Table Storage.
- [ ] T027 [P] [US3] Build `/home/jakob/private/alpakasoelde/src/dashboard/src/components/events/DocumentReviewPanel.astro` that renders a read-only grid with scan badges, blocked download messaging, and retry prompts for quarantined files; Acceptance: viewers see disabled download buttons until `scanStatus` becomes Clean.
- [ ] T028 [US3] Enhance `/home/jakob/private/alpakasoelde/src/dashboard/src/pages/events/[id].astro` to branch between editor and viewer modes, call the download-link endpoint, and stream files with progress feedback; Acceptance: reviewers download ≤10 MB files in under 3 seconds while unauthorized users receive the permission error banner.
- [ ] T029 [US3] Document the reviewer download/permission scenarios (including blocked/quarantined states) inside `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md`; Acceptance: QA can follow the checklist to observe both success and access-denied flows.

**Parallel Execution Examples (US3)**
- Build T025 (download-link API) alongside T026 (scan handler) because they operate on different triggers but share schemas defined in Foundational tasks.
- Implement T027 (review panel) while T028 (page integration) wires the API call, enabling designers and API devs to work concurrently.

---

## 6. Final Phase: Polish & Cross-Cutting Concerns
**Purpose**: Harden reliability, compliance, and documentation that spans all stories (cleanup jobs, archive policies, telemetry).
**Workstreams**: Backend API, Infrastructure, QA/Docs

- [ ] T030 Implement `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/CleanupExpiredUploadsFunction.cs` (timer-trigger) that deletes blobs stuck in `UploadStatus=Queued/Uploading` beyond 15 minutes, marks them `Failed`, and emits `UploadFailed` audits; Acceptance: running locally removes seeded stale rows and frees orphaned blobs.
- [ ] T031 [P] Build `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/ArchivePurgeReconcilerFunction.cs` to find rows with `ArchivePurgeAt < UtcNow`, purge archived blobs, and stamp `Removed` + `ArchivePurgeAt=null`; Acceptance: executing the function deletes only archive-tier blobs and leaves active documents untouched.
- [ ] T032 [P] Add structured logging + Application Insights custom events across `/home/jakob/private/alpakasoelde/src/dashboard-api/features/events/documents/*` so each upload/download/remove action carries correlation IDs matching DocumentAudit entries; Acceptance: running `dotnet run` emits trace logs with documentId, actorId, and action labels.
- [ ] T033 [P] Update `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/plan.md` and `/home/jakob/private/alpakasoelde/src/dashboard/README.md` with architecture notes covering SAS flows, malware scan coordination, archive policies, and cleanup job schedules; Acceptance: both docs summarize queue names, retention windows, and operational runbooks without TODOs.

---

## Dependencies & Execution Order
- **Phase 1 → Phase 2**: Environment prep must precede the `/home/jakob/private/alpakasoelde/infrastructure/*` Bicep work.
- **Phase 2 → Phases 3–5**: Shared entities, repositories, and Azure resources are prerequisites for all user stories.
- **User Story Ordering**: US1 (P1) must complete before US2 (P2) because metadata creation is required before post-create management; US3 (P3) depends on documents existing plus scan signals from US2 cleanup flows.
- **Polish**: Cleanup jobs, archive reconciler, telemetry, and docs run last because they require stable APIs and schemas.

```
Setup → Foundational → US1 (P1) → US2 (P2) → US3 (P3) → Polish
```

## Parallel Execution Examples (Summary)
- **US1**: (T010 ↔ T011), (T012 ↔ T014 ↔ T015).
- **US2**: (T019 ↔ T021), (T022 ↔ T023).
- **US3**: (T025 ↔ T026), (T027 ↔ T028).
- **Cross-Phase**: All tasks marked `[P]` can proceed concurrently when they touch distinct files or triggers.

## Implementation Strategy
1. **MVP (US1 only)**: Complete Phases 1–3, validate the creation flow via `/home/jakob/private/alpakasoelde/specs/001-event-document-upload/quickstart.md`, and deploy once uploads + metadata finalize successfully.
2. **Incremental Delivery**: After MVP, ship Phase 4 (US2) to unlock post-create management, then Phase 5 (US3) for reviewer downloads.
3. **Reliability Hardening**: Run Phase 6 tasks to ensure orphan cleanup, archive purge, malware scan reconciliation, and documentation before general release.
4. **Parallel Team Play**: While one developer finalizes US1, others can stage infrastructure (T004–T005) or begin US2 UI work with mocked APIs using `/home/jakob/private/alpakasoelde/src/dashboard-api/requests.http` samples.
