# Feature Specification: Event Document Upload

**Feature Branch**: `001-event-document-upload`  
**Created**: 2025-11-24  
**Status**: Draft  
**Input**: User description: "In the dashboard project, when an event is created the user must be able to upload files (e.g., invoices) that are associated with the event. The selected uploaded files should be stored in the \"event-documents\" blob container. Provide a full feature specification aligned with existing project conventions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Attach invoices during event creation (Priority: P1)

As an event organizer, I can attach multiple supporting documents while creating a new event so the finance team has immediate context for approvals.

**Why this priority**: Events cannot be reviewed or reimbursed without the supporting invoices, so this is the minimum viable experience.

**Independent Test**: Start a new event, add up to five compliant files, submit, and verify they appear against the event with correct metadata and storage references.

**Acceptance Scenarios**:

1. **Given** I have the permission to create events and the form is open, **When** I select one or more PDF/JPG/PNG files each ≤25 MB and save the event, **Then** the event is created, each file shows an "Uploaded" status, and the attachments are persisted in the event-documents container tied to the new event ID.
2. **Given** I try to attach a file that exceeds 25 MB, **When** I select it, **Then** the upload is blocked, the UI displays the size rule, and no attempt is made to store the file.
3. **Given** I added the wrong file before submitting the event, **When** I remove it from the pending list, **Then** the file is deleted from the queue and will not be uploaded when I submit the event.

---

### User Story 2 - Manage attachments for an existing event (Priority: P2)

As an event owner, I can add or remove documents after the event is created so that late-arriving paperwork is still captured in one place.

**Why this priority**: Events frequently require additional receipts after creation; without the ability to update attachments, process work would stall.

**Independent Test**: Open an existing event, add a new file, remove another, and confirm all changes reflect instantly without affecting the base event data.

**Acceptance Scenarios**:

1. **Given** an event already exists and I have edit permissions, **When** I upload an additional document, **Then** it is validated with the same rules as P1, stored in event-documents, and the timeline shows who added it and when.
2. **Given** a document is no longer relevant, **When** I choose “Remove” and confirm, **Then** the association is revoked, the blob is moved into an archive tier for 90 days before purge, and an audit note records the action.

---

### User Story 3 - Review and download attachments (Priority: P3)

As any collaborator with view rights, I can see and download event documents to verify details or forward them to external approvers.

**Why this priority**: The value of uploading documents is realized only when reviewers can reliably retrieve them.

**Independent Test**: Log in as a read-only reviewer, open an event, download each file, and confirm access is denied when permissions are insufficient.

**Acceptance Scenarios**:

1. **Given** I have view access to an event, **When** I open the documents panel, **Then** I can see filename, type, size, uploader, and timestamp plus download each file in under 3 seconds for files under 10 MB.
2. **Given** I lack the necessary permission, **When** I try to download an attachment, **Then** the system blocks the action and informs me I need elevated access.

---

### Edge Cases

- User loses connectivity mid-upload; partial blobs must be cleaned up and the UI should prompt to retry.
- Duplicate filenames are uploaded for the same event; the system must append a short GUID-based suffix to the stored blob name (while preserving the original display name) so nothing is overwritten silently.
- An event transitions to a locked/archived status; uploads must be disabled while downloads remain accessible per retention policy.
- The event-documents container is temporarily unavailable; the dashboard should queue retries and inform the user that saving is blocked until storage responds.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The event creation flow MUST allow selecting and queuing up to five files per submission, supporting PDF, JPG, PNG, and CSV formats capped at 25 MB each.
- **FR-002**: Prior to saving, the UI MUST display each queued file with size, type, and a remove action so organizers can correct mistakes without leaving the form.
- **FR-003**: On save, all queued files MUST be uploaded atomically to the `event-documents` blob container and linked to the event via immutable identifiers and metadata (event ID, uploader, timestamps, file size/type), storing each blob under a GUID-suffixed filename to avoid collisions while exposing the original name in metadata.
- **FR-004**: Users with event edit permissions MUST be able to add or remove attachments from existing events using the same validation, and removals MUST move the blob into a 90-day archive tier (with eventual purge) while capturing an audit entry.
- **FR-005**: Downloads MUST be available to any user with view access to the event, while users without access MUST receive an authorization error without exposing file metadata beyond filename.
- **FR-006**: The system MUST surface upload progress and error states (size limit, unsupported type, storage outage) inline, allowing users to retry individual files without re-entering event data.
- **FR-007**: Each uploaded document MUST undergo an asynchronous virus/malware scan; event submission should complete immediately, but downloads remain blocked until the scan passes, and failures MUST notify the uploader plus mark the file as quarantined.
- **FR-008**: The dashboard MUST prevent orphaned blobs by cleaning up failed uploads and by reconciling pending uploads that do not complete within 15 minutes.
- **FR-009**: Document metadata MUST be searchable/filterable within the event (e.g., filter by type or uploader) so reviewers can quickly find the correct file when multiple attachments exist.
- **FR-010**: All attachment actions MUST be logged with actor, timestamp, action (upload/download/remove), and outcome to support compliance reviews.

### Key Entities *(include if feature involves data)*

- **Event**: Represents a scheduled activity with attributes such as Event ID, title, date range, owner, status (Draft, Active, Locked), and permission set that governs who may manage attachments.
- **Event Document**: Represents a single uploaded file, storing Event ID, blob URI, filename, file type, size, checksum, uploader, upload timestamp, scan status, and an optional description to explain the document’s purpose.
- **Document Audit Entry**: Captures every interaction with an attachment, including Event ID, Document ID, actor, action (upload/download/remove/quarantine), channel (UI/API), timestamp, and result to satisfy compliance traceability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of events that require documentation have all mandatory files attached within the same creation session without support intervention.
- **SC-002**: 99% of uploads that pass validation finish in under 30 seconds for files ≤25 MB, providing progress feedback throughout.
- **SC-003**: 0 orphaned files remain in the event-documents container after daily reconciliation jobs, ensuring every blob is tied to an Event ID.
- **SC-004**: 90% of reviewers report that they can locate and download the needed document in under 3 clicks during user acceptance testing.

## Assumptions

- Allowed file types (PDF, JPG, PNG, CSV) and 25 MB size limit align with current finance compliance standards; changes will be handled in future revisions.
- Existing role-based access controls already distinguish between event creators/editors and viewers; this feature reuses those roles without modification.
- The `event-documents` blob container, retention policies, and malware scanning services exist and are monitored by platform operations.
- Events rarely require more than 20 documents; pagination or lazy loading can be deferred until metrics show higher usage.
