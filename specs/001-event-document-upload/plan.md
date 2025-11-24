# Implementation Plan: Event Document Upload

**Branch**: `001-event-document-upload` | **Date**: 2025-11-24 | **Spec**: [/specs/001-event-document-upload/spec.md](./spec.md)

## Summary

Implement secure, low-friction document uploads for the Astro-based dashboard so event organizers can attach up to five compliant files per submission and maintain them throughout the event lifecycle. The dashboard will use a client-side queue with validation, request per-file SAS tokens from a dedicated Azure Function, upload directly to the `event-documents` container with GUID-suffixed names, and then finalize metadata links on save. Additional Functions will expose list/download/remove operations, enforce permissions, trigger malware scans via storage events, archive removed blobs for 90 days, and reconcile orphaned uploads on a 15-minute job.

## Technical Context

**Language/Version**: Astro 5.14 (TypeScript 5) for the dashboard UI; C# 14 on .NET 10.0 isolated Azure Functions for backend slices  
**Primary Dependencies**: Astro core runtime, Azure Storage Blobs SDK 12.26, Azure Data Tables SDK 12.11, Azure Functions Worker 2.51, HttpMultipartParser for multi-part bodies  
**Storage**: Azure Blob Storage `event-documents` container for binaries plus Azure Table Storage tables (`EventDocuments`, `EventDocumentAudit`) for metadata and audit events  
**Testing**: Manual dashboard end-to-end flows plus `requests.http` coverage for new Functions; no automated harness currently defined  
**Target Platform**: Azure Static Web Apps (Linux) with integrated Azure Functions backend; local dev via `npm run dev` + `dotnet run`  
**Project Type**: Full-stack dashboard (Astro frontend + Azure Functions vertical slices)  
**Performance Goals**: ≤30 s upload completion for ≤25 MB files, ≤3 s download start for ≤10 MB files, progress UI refreshing within 500 ms ticks  
**Constraints**: Max 5 files per submission, 25 MB/file, allowed types PDF/JPG/PNG/CSV, downloads gated until malware scan reports Clean, offline retries limited to 15 minutes, duplicate filenames resolved via GUID suffix while OriginalName preserved  
**Scale/Scope**: Events module serving hundreds of events with ≤20 docs/event, expecting <5 simultaneous uploads per organizer but thousands of blobs overall

## Constitution Check

### Pre-Design Gate

- **Minimal Dependencies** – PASS: Reuses existing Azure SDK packages already referenced by `dashboard-api`; frontend relies solely on Astro + built-ins, so no extra libraries are introduced.  
- **Static-First Architecture** – PASS: Astro pages remain statically rendered; uploads depend on progressive enhancement and call serverless APIs only for persistence.  
- **Vertical Slice Organization** – PASS: All new Functions will live under `src/dashboard-api/features/events/documents/*` with co-located models and handlers.  
- **Infrastructure as Code** – PASS: Any storage/container changes will be expressed through the existing Bicep modules beneath `infrastructure/`.  
- **Type Safety & Modern C#** – PASS: Functions will continue using C# 12 features (records, pattern matching, target-typed `new`), keeping parity with the `dashboard-api` conventions.

### Post-Design Gate

- **Minimal Dependencies** – PASS: Design artifacts keep the footprint to Azure SDKs already sourced in `dashboard-api`; UI enhancements use native `<input type="file" multiple>` plus Astro components without introducing upload libraries.  
- **Static-First Architecture** – PASS: The design routes all heavy work (SAS issuance, metadata persistence, cleanup) through Functions while the dashboard stays statically generated; only dynamic islands handle progress state.  
- **Vertical Slice Organization** – PASS: Contracts + data model map to a dedicated `documents` slice under the existing `events` feature, keeping shared records in `shared/entities`.  
- **Infrastructure as Code** – PASS: Research + quickstart explicitly call for updating the existing storage Bicep module for container lifecycle + Event Grid wiring, so no portal-only drift.  
- **Type Safety & Modern C#** – PASS: Data model + contracts prescribe record types and enums that align with C# 12 expression-bodied Functions, ensuring the project continues to leverage modern language features.

## Project Structure

### Documentation (this feature)

```text
specs/001-event-document-upload/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── event-documents.openapi.yaml
```

### Source Code (repository root)

```text
src/
├── dashboard/
│   ├── src/components/
│   ├── src/pages/
│   ├── src/styles/
│   └── staticwebapp.config.json
├── dashboard-api/
│   ├── features/events/
│   │   └── documents/              # new vertical slice with upload/list/remove Functions
│   ├── features/messages/
│   └── shared/
└── website*/                       # legacy marketing surfaces (untouched by this feature)

infrastructure/
└── storage/
    └── event-documents.bicep      # container + lifecycle config updates
```

**Structure Decision**: The existing Astro dashboard plus Azure Functions backend already provide the right separation, so the feature lives inside `src/dashboard` (UI) and `src/dashboard-api/features/events/documents` (Functions) with matching infrastructure definitions; no extra projects are required.

## Complexity Tracking

_No constitution deviations identified; table remains empty._
