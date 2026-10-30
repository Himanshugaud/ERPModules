# ERP Modules Backend

TypeScript backend built with Azure Functions v4 and the Node.js programming model.

The production ERP implementation also contains a layered .NET Azure Functions API in [backend/ERP.sln](backend/ERP.sln) and a React frontend in [frontend/package.json](frontend/package.json). The TypeScript setup below describes the separate root diagnostic service.

## Security Audit and Regression Tests

See the [first-pass audit and remediation checklist](docs/audit-2026-10-18.md) and [performance verification report](docs/performance-2026-10-18.md). The full audit is not complete; remaining security findings and blocked SQL Server tests are explicitly recorded.

```powershell
dotnet test backend/ERP.sln --filter "Category!=SqlServer"
dotnet test backend/ERP.sln --filter "Category=SqlServer"
npm test
npm --prefix frontend test
```

The SQL category uses only a generated disposable LocalDB database, never application connection settings. It requires a working `(localdb)\MSSQLLocalDB` instance. Do not run the existing database smoke/seed scripts against production as regression tests.

Root Node tests mock database access and verify diagnostic responses/logs; frontend Node tests cover requirement conversion/document actions using render stubs, plus synthetic static files and a temporary loopback server. Neither invokes configured databases or existing application servers. Diagnostic success responses intentionally no longer include database/login metadata; failures use a generic error and invocation trace ID.

Converted requirements retain a **Documents** action on the Requirements page. The requirement team can view/download existing files and upload additional files directly into the linked project's **Requirement Attachments** folder without editing or reconverting the requirement. Existing permissions apply (`document.create` plus `project.create` to upload, `document.read` to view); the 25 MB per-file limit and blocked executable/script types are unchanged. Browser upload/reopen and mobile-layout checks used intercepted attachment requests, with browser data restored afterward; they did not create staging files or prove a new live-storage round trip.

App Service deployment artifacts must include [frontend/server-app.js](frontend/server-app.js) alongside [frontend/server.js](frontend/server.js). The entrypoint still starts unconditionally; the helper allows isolated HTTP tests without starting the deployed application.

The [production-readiness workflow](.github/workflows/production-readiness.yml) defines backend analyzer/build/test gates, real LocalDB concurrency tests, frontend/legacy builds, frontend typechecking, dependency audits, and a checksum-verified, fully redacted Git-history secret scan. Actions are pinned to full commit SHAs; checkout does not persist credentials. It performs no deployment. Its YAML has been validated locally; remote execution and required branch checks still need setup/verification. Remaining High advisories intentionally block the dependency gates. See the dated follow-up in the audit report for current results and unresolved release requirements.

The session-hardening update requires users to sign in again: tokens issued before the credential-version claim was introduced are rejected by upgraded instances. Drain/replace old instances during an approved rollout. Persistent logout/revocation and distributed login throttling remain unfinished; do not treat the local regression suite or workflow definition as production approval.

## Staging SQL for Local Development and Tests

The shared non-production database is `erpmodulessqldb-staging` on `erpmodulesqlserver.database.windows.net`. All six schema scripts are initialized (69 tables). Both ignored local SQL configurations now use the contained user `erp_local_staging`, with DML/EXECUTE access through `erp_staging_runtime` on the four ERP schemas, without database ownership, table creation, or user-management privileges. Credentials stay in ignored settings and must not be committed or pasted into chat.

```powershell
# Preview only; applying requires an authorized Azure CLI SQL administrator identity.
./db/scripts/initialize-staging.ps1
# Explicitly reapply staging schema and configure both local SQL targets.
./db/scripts/initialize-staging.ps1 -Apply -ConfigureLocal
# Cloud integration tests are explicit, never part of default npm test.
node --test db/scripts/staging-sql.test.cjs
```

The initializer rejects other database names. The four staging tests verify schema, runtime permissions, FK rejection/rollback, and concurrent status predicates; they clean up their synthetic records. They do not replace the three .NET LocalDB tests or full ERP service workflow tests. Never redirect the existing LocalDB fixture to staging: it creates/deletes databases.

After updating settings, rebuild and restart the .NET host. An explicit output root avoids accidentally starting the separate root diagnostic app:

```powershell
dotnet build backend/src/ERP.Api/ERP.Api.csproj
func start --script-root "backend/src/ERP.Api/bin/Debug/net9.0" --dotnet-isolated --port 7072 --cors http://localhost:5173 --cors-credentials
```

Local API checks returned anonymous `401` and Development-header reads `200`. The authorized production-reference copy populated 1 organization, 9 users, 642 items and their department/role/permission/category/UOM dependencies. The item API shows 634 non-deleted items; the other 8 copied rows remain soft-deleted. That reference-only phase excluded projects, stock, orders, attachments, production password hashes and external identity links. A subsequent user-authorized reset configured local-only passwords for all nine staging users with distinct salts using the application hasher. Normal login and signed-token `/me` returned `200` for the requested account without development headers. The local testing password is not recorded here; production passwords and the normal password policy were unchanged.

The private local attachment container is `erp-attachments-local` in storage account `erpmodulesattach`. The ignored local API setting `ERP_AttachmentsContainer` now selects it, and the API was rebuilt/restarted. A temporary blob passed upload/download SHA-256 verification and was deleted. Production container `erp-attachments` and deployed settings were unchanged. This is a separate container in the same account, not a new account or a revocation of existing account-wide access.

The one-time [reference-copy script](db/scripts/copy-local-reference-data.cjs) reads a consistent production snapshot into memory, excludes credential/identity-link columns, and writes only staging in one transaction. It refuses to overwrite populated staging reference tables and remaps role permissions by code. Preview with:

```powershell
node db/scripts/copy-local-reference-data.cjs
# Only for an empty, explicitly approved staging reference-data target:
node db/scripts/copy-local-reference-data.cjs --apply
```

The later client/warehouse/opening-stock request populated **2 clients, 3 warehouses and 11 stock balances**, preserving production quantities and average costs from a consistent snapshot. One linked project and its required status/priority were copied to preserve a warehouse foreign key; no tasks/orders/attachments were copied. All stock was posted in **one atomic staging transaction**, with **9 new ADJUSTMENT movements/lines across 2 warehouse-specific adjustment documents**, reason `OPENING_BALANCE`, and one shared import correlation. Two zero-balance rows were retained without inventing zero-quantity movements. The model requires one adjustment header per warehouse, so one atomic transaction does not mean one document across multiple warehouses. Production movement history was not copied.

```powershell
# Preview the separate, one-time opening inventory mode.
node db/scripts/copy-local-reference-data.cjs --inventory-opening
# Only for empty staging client/warehouse/stock and linked-project lookup targets.
node db/scripts/copy-local-reference-data.cjs --inventory-opening --apply
```

This mode refuses existing destination import data, preserves users/passwords/items, uses exact SQL decimal conversion for stock, and checks snapshot/ledger/cost/document reconciliation before commit. SQL regression tests and signed-token client/warehouse/stock/adjustment/movement API reads passed after import. It is an administrative opening-balance seed, not a replay of production workflow events or a full service-concurrency/load test.

After those initial imports, restore the complete workflow and sheet catalogs before using requirement conversion or Plan Workspace:

```powershell
node db/scripts/copy-local-reference-data.cjs --planning-catalog
node db/scripts/copy-local-reference-data.cjs --planning-catalog --apply
```

This inserts missing records only, preserving existing local data and refusing conflicting lookup identities. Staging now has all 8 project statuses, 4 priorities and the 3 production sheet types with 18 legacy sections/76 mappings. Category-backed sheet buttons use the already-copied categories/items. Restart the local API after reference-data changes to clear server caches and refresh the browser to clear frontend caches. Requirement conversion explicitly requires `PLANNING`; it no longer silently substitutes the first status. A browser regression verified conversion starts in Planning and all three direct-add sheet editors open on desktop, with their buttons fitting mobile width; its synthetic project/draft were removed afterward. Production was not modified.

Staging now contains copied production profile/master data: keep access restricted and never commit data exports or credentials. No production records were modified. The Basic-tier database and bounded functional checks are not evidence of production capacity or full audit completion.

## Procurement Workflow

Purchase orders can be drafted independently on the Purchase Orders page or from
missing material-request quantities. Drafts have no supplier, project link, or
prices and can be edited before submission. The material-request reference is
retained for traceability. After approval, **Mark ordered** requires a supplier and
positive unit prices for every line. Receipts for ordered or partly received POs
are entered on **Stock** and remain pending until approved; approval posts stock.
Material requests can be marked **Ready to Ship** only against available stock at
a selected source warehouse. Vehicle and driver details belong to dispatch in
Transport, not the ready-to-ship action.

New PO supports **Upload Excel**, **Download template**, and **Download Excel**
for draft items and quantities. Imports accept `Item Code` (or an unambiguous
`Item Name`) and `Qty`/`Quantity` columns. Repeated items are combined; invalid or
unknown rows reject the import without replacing the current draft. Blank
template quantities are ignored. Prices and supplier details remain deferred
until ordering.

Before deploying this workflow to an existing database, apply
[db/scripts/defer-po-supplier.sql](db/scripts/defer-po-supplier.sql) to allow
supplier-free drafts. This script does not change existing orders or stock.
Deploy the backend and frontend together: `POST /purchase-orders/{id}/mark-ordered`
now requires a JSON body containing `supplierId`, `lines` (`lineId`, `unitPrice`,
`taxRatePercent`), and optionally `rowVersion`, `orderDate`, `expectedDate`, and
`currencyCode`.

Latest PO verification: 22 focused backend tests and 53 frontend tests passed,
including real workbook round trips. Frontend typechecking/build and mocked
desktop/mobile browser checks passed. Browser checks did not write database
records. The supplier-nullability migration is now applied to **staging only**;
a staging draft header/line insert with no supplier or prices passed and was
rolled back without leaving test records. Production was not changed or
deployed. Apply the migration separately before a production rollout. Full .NET
SQL integration and live end-to-end workflow tests remain unverified.

## Prerequisites

- Node.js 18 or newer
- Azure Functions Core Tools 4
- Azure CLI (for deployment)
- Azurite when adding functions that require Azure Storage locally

## Local development

```powershell
npm install
npm start
```

The Functions host starts at `http://localhost:7071` by default.

## Endpoints

### Health check

```http
GET /api/health
```

Example response:

```json
{
  "status": "ok",
  "timestamp": "2026-08-28T00:00:00.000Z"
}
```

## Scripts

- `npm run build` compiles TypeScript into `dist`.
- `npm run watch` recompiles when source files change.
- `npm start` builds and starts the local Functions host.
- `npm run clean` removes compiled output.

## Configuration

Local settings belong in `local.settings.json`, which is excluded from source control. Add matching application settings to the Azure Function App before deployment.

## Deploy to Azure

After signing in with `az login` and creating an Azure Function App, publish with:

```powershell
func azure functionapp publish <FUNCTION_APP_NAME>
```