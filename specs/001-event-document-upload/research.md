# Research – Event Document Upload

## Decision 1: Use per-file SAS uploads with optimistic client queue
- **Rationale**: Azure Static Web Apps cannot proxy large payloads efficiently, so issuing short-lived SAS tokens from an Azure Function lets the browser upload directly to Blob Storage using the Block Blob REST API, honoring the 25 MB cap and enabling native progress events plus chunk retries. Limiting tokens to 5 per event and scoping them to `event-documents/{eventId}/<guid>_<original>` prevents cross-event leakage while keeping dependencies minimal.  
- **Alternatives considered**: (a) Streaming file bytes through the Functions API—rejected because the Functions plan has a 230 MB memory limit and incurs double bandwidth; (b) Re-using Storage account keys client-side—rejected for security exposure.

## Decision 2: Persist document metadata and status in Azure Table Storage
- **Rationale**: Table Storage already underpins other dashboard entities, supports inexpensive queries by PartitionKey (EventId) + RowKey (DocumentId), and can store filterable metadata (type, size, uploader, scan status) without provisioning Cosmos DB. This satisfies FR-003/FR-009 while aligning with the Minimal Dependencies principle.  
- **Alternatives considered**: (a) Embedding metadata inside the Events table—rejected because each event can own up to 20 docs leading to sparse updates and concurrency conflicts; (b) Adding SQL/Blob index—rejected because no SQL tier exists today.

## Decision 3: Model audit entries as append-only Table records plus Application Insights traces
- **Rationale**: Writing lightweight audit rows (PartitionKey=EventId, RowKey=DocumentId+Timestamp) captures upload/download/remove/quarantine actions with actors and outcomes, fulfilling FR-010 while remaining queryable for compliance exports. Mirroring the same data to Application Insights custom events keeps dashboards simple without duplicating storage primitives.  
- **Alternatives considered**: (a) Durable Functions workflow history—rejected for added runtime complexity; (b) Azure Monitor logs only—rejected because analysts require tenant-scoped exports via Table queries.

## Decision 4: Leverage existing malware scanning queue triggered by Blob Created events
- **Rationale**: Subscribing the storage account to Event Grid and enqueueing `DocumentScanRequested` messages lets the dedicated malware scanner (already referenced in assumptions) process each blob asynchronously. The document record holds `ScanStatus` + `ScanResultDetails`, and download SAS issuance checks this flag to block quarantined files, satisfying FR-007 without delaying event submission.  
- **Alternatives considered**: (a) Synchronous inline scanning—rejected because it would exceed the 30 s upload SLA; (b) Relying purely on Storage Defender—rejected since it lacks per-document notification hooks for the dashboard UI.

## Decision 5: Implement orphan cleanup via scheduled Azure Function driven by Table filters
- **Rationale**: A 5-minute timer-triggered Function can query `EventDocuments` where `UploadStatus = Pending` and `CreatedAt < UtcNow - 15m`, delete their blobs, and mark rows as failed, satisfying FR-008. The same job can demote archived blobs by ensuring removal requests set `AccessTier = Archive` and schedule purge after 90 days.  
- **Alternatives considered**: (a) Client-side retry watchdog only—rejected because browsers cannot guarantee execution when tabs close; (b) Azure Data Factory pipeline—rejected for overhead relative to table queries.
