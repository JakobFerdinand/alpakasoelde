# Quickstart – Event Document Upload Feature

## 1. Prerequisites
- Node.js 20+ and pnpm or npm installed
- .NET SDK 10.0.x (isolated Azure Functions)
- Azure Storage emulator (Azurite) or access to the shared dev storage account with `event-documents` container
- `AZURE_STORAGE_CONNECTION_STRING` and `DOCUMENT_SCAN_QUEUE` values in `src/dashboard-api/local.settings.json`

## 2. Install dependencies
```bash
cd /home/jakob/private/alpakasoelde/src/dashboard && npm install
cd /home/jakob/private/alpakasoelde/src/dashboard-api && dotnet restore
```

## 3. Run local services
```bash
# Terminal 1 – Astro dashboard
cd /home/jakob/private/alpakasoelde/src/dashboard
npm run dev

# Terminal 2 – Azure Functions (documents slice auto-loads)
cd /home/jakob/private/alpakasoelde/src/dashboard-api
DOTNET_ENVIRONMENT=Development dotnet run
```
The dashboard proxies `/api/*` calls to the Functions host via Static Web Apps CLI defaults.

## 4. Seed storage for testing
1. Create (or ensure) the `event-documents` container exists in your dev storage account.
2. Grant yourself Storage Blob Data Contributor (or use Azurite, which skips RBAC).
3. Optional: enqueue a `DocumentScanRequested` message to the scanner queue to test the Clean/Quarantined flow.

## 5. Exercise the flow
- Navigate to `/events/new` (temporary route) and attach up to five PDF/JPG/PNG/CSV files ≤25 MB.
- Use browser dev tools to confirm SAS uploads hit blob storage directly and metadata finalize request contains checksums.
- Remove a document to verify it moves to the Archive tier and the UI timeline records the removal.
- Sign in as a viewer-only account and ensure downloads are blocked until `scanStatus = Clean`.

## 6. Troubleshooting
- **Upload stuck in Pending**: Inspect the `DocumentUploadSession` entry in Table Storage and run the cleanup Function locally (`dotnet run --function EventDocumentReconciler`).
- **Downloads blocked unexpectedly**: Check for lingering `scanStatus = Pending`; manually enqueue a scan completion message while debugging.
- **Blob name collisions**: Document IDs are GUIDs by design; delete stray blobs with older naming if migrating legacy data.
