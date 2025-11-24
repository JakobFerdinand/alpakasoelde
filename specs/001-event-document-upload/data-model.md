# Data Model – Event Document Upload

## Entity: Event
| Field | Type | Description | Notes |
| --- | --- | --- | --- |
| `EventId` | string (GUID) | Primary identifier shared across dashboard + storage | Client can pre-generate during create flow to allow early uploads |
| `Title` | string | Human-readable event name | existing |
| `OwnerUserId` | string | Organizer responsible for attachments | used for permissions |
| `Status` | enum (`Draft`, `Active`, `Locked`, `Archived`) | Drives upload/disable logic | uploads disabled when `Locked` or higher |
| `PermissionSet` | object | Roles (creator, editors, viewers) reused for attachment auth | assumed existing |

**Relationships**: `Event` (1) — (N) `EventDocument`; `Event` (1) — (N) `DocumentAuditEntry`.  
**Validation**: `EventId` immutable post-create; transitions to `Locked` trigger a cascade that prevents new `EventDocument` rows from entering `UploadStatus = Queued/Uploading`.

## Entity: EventDocument
| Field | Type | Description | Validation / Business Rules |
| --- | --- | --- | --- |
| `EventId` | string | Partition key aligning with parent event | required |
| `DocumentId` | string GUID | Row key + blob name suffix | assigned per queued file |
| `OriginalName` | string | Filename displayed in UI | read-only to preserve context |
| `BlobName` | string | `<EventId>/<DocumentId>_<slug>` used for blob paths | ensures uniqueness |
| `BlobUri` | string | Storage endpoint recorded after upload finalize | validated to container scope |
| `ContentType` | enum (`application/pdf`, `image/jpeg`, `image/png`, `text/csv`) | Derived from file header sniffing + accept list | reject others |
| `SizeBytes` | int | File size recorded at queue time | must be ≤ 26,214,400 bytes |
| `Checksum` | string (SHA256) | Provided by client after upload for tamper detection | optional until finalize |
| `UploaderUserId` | string | Actor that queued the file | audited |
| `UploadTimestamp` | datetime | When finalize call succeeds | set by server |
| `UploadStatus` | enum (`Queued`, `Uploading`, `Uploaded`, `Failed`, `Archived`) | Drives progress + cleanup | `Queued/Uploading` TTL 15 min |
| `ScanStatus` | enum (`Pending`, `Clean`, `Quarantined`, `Failed`) | Controls download availability per FR-007 | downloads allowed only when Clean |
| `ScanCompletedAt` | datetime? | When scanner finishes | null until finish |
| `Description` | string | Optional user note for reviewers | ≤ 280 chars |
| `ArchivePurgeAt` | datetime | When archived blob scheduled for purge (90 days after removal) | null for active docs |

**State Transitions**:
1. `Queued` → `Uploading` when SAS token is issued and client begins transfer.  
2. `Uploading` → `Uploaded` once blob `PutBlockList` completes and finalize metadata API succeeds.  
3. `Uploaded` → `Archived` when user removes doc; function moves blob to Archive tier and stamps `ArchivePurgeAt = now + 90d`.  
4. Any state → `Failed` if transfer aborts or scan fails; cleanup job deletes blob + metadata.  
5. `ScanStatus` evolves separately: `Pending` → `Clean` after scanner success, or `Pending` → `Quarantined`/`Failed` for detections/errors.

## Entity: DocumentAuditEntry
| Field | Type | Description | Notes |
| --- | --- | --- | --- |
| `EventId` | string | Partition key | aligns with Event |
| `DocumentId` | string | Row identifier | join to EventDocument |
| `AuditId` | string (GUID) | Uniquely identifies each action record | enables dedupe |
| `Action` | enum (`UploadRequested`, `UploadCompleted`, `UploadFailed`, `DownloadRequested`, `DownloadBlocked`, `Removed`, `Restored`, `ScanPassed`, `ScanFailed`) | Satisfies FR-010 | values map to UI timeline |
| `ActorUserId` | string | Who performed the action | required |
| `Channel` | enum (`UI`, `API`, `System`) | Distinguishes manual vs automated | `System` for cleanup/scan |
| `Timestamp` | datetime | When the action occurred | server clock |
| `Result` | string | Additional context (e.g., reason for block, virus signature) | optional |

**Relationships**: Many audit entries per document; viewer timeline queries by `EventId` and sorts descending by `Timestamp`.  
**Validation**: `Action` transitions correlate with `EventDocument` state machine (e.g., `Removed` only allowed when `UploadStatus = Uploaded`).

## Entity: DocumentUploadSession
| Field | Type | Description | Notes |
| --- | --- | --- | --- |
| `SessionId` | string GUID | Temporary ID for files queued before event create commits | stored in-memory on client and echoed to backend |
| `ProspectiveEventId` | string GUID | Either server-provided or generated client-side | becomes real EventId on save |
| `ExpiresAt` | datetime | 15-minute TTL for pending uploads | cleanup job prunes expired sessions |
| `Files` | list of `{DocumentId, OriginalName, SizeBytes, ContentType}` | Helps front-end reconcile uploads after event saves | not persisted server-side beyond Table entries |

**Usage**: When users create a new event, the UI generates `ProspectiveEventId` and includes it in both the event payload and per-file SAS requests. Once the `POST /events` call commits, the same ID is primary key, so no extra migrations are required.

## Derived Views
- **Documents Panel View**: Aggregates `EventDocument` + latest `DocumentAuditEntry` per document to display status, uploader, timestamps, and scan outcome. Supports filtering by `ContentType` and `UploaderUserId` leveraging Table Storage queries + client-side filtering for ≤20 results.
- **Timeline View**: Composes `DocumentAuditEntry` items with relative timestamps for the UI timeline referenced in User Story 2 acceptance criteria.
- **Download Eligibility**: Evaluated at runtime by combining `Event.Status != Locked` OR `Action = Download` allowed for viewers plus `ScanStatus = Clean` and `PermissionSet` check.
