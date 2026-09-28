# Procurement Management Module — Feature & Build Plan

Extends the existing modular-monolith ERP with a **Procurement** slice that plugs into
**Planning** (`project.Projects`, sites = `inventory.Warehouses` of type `SITE_STORE`) and the
already-built **Inventory** module (`inventory` schema, ledger-based stock).

## Core flow
```
Planning → Material Requirement → Procurement Review → Purchase Order → Inventory (Purchase Receipt)
                                                                       → Site Transfer → Material Issue
```

No new SQL schema — everything lives in the existing `inventory` schema (additive columns/tables
only, per repo convention of never touching existing tables' meaning, only adding).

## 1. Material Requirement (new)
Table `inventory.MaterialRequirements` (+ `MaterialRequirementLines`).
- Fields: OrganizationId, ReqNumber, ProjectId, WarehouseId (requesting site), DepartmentId,
  Priority (`LOW|MEDIUM|HIGH|URGENT`), RequiredDate, Status, Requested/Approved/Rejected By+At,
  RejectionReason, PurchaseOrderId (set once converted), Notes.
- Lifecycle: `SUBMITTED → APPROVED → CONVERTED` or `SUBMITTED → REJECTED` (simple — no draft step;
  Planning submits directly, Procurement reviews).
- Actions: Create (Planning), Approve/Reject (Procurement), Convert to PO (creates a `DRAFT`
  Purchase Order from the requirement's lines + chosen supplier/prices).

## 2. Purchase Order (new domain layer — SQL table already existed, unused)
`inventory.PurchaseOrders` / `PurchaseOrderLines` gain: ProjectId, WarehouseId (delivery site),
MaterialRequirementId, SubTotal, TaxAmount, Submitted/Approved/Rejected By+At, ClosedAt;
lines gain TaxRatePercent, TaxAmount, MaterialRequirementLineId.
- Lifecycle: `DRAFT → PENDING_APPROVAL → APPROVED → ORDERED → PARTIALLY_RECEIVED → RECEIVED → CLOSED`
  (+ `REJECTED` / `CANCELLED` branches).
- Purchase Receipt = existing `inventory.GoodsReceipts` posted with `PurchaseOrderId` set: on post,
  each line's `QtyReceived` is incremented and the PO status is recomputed
  (`PARTIALLY_RECEIVED` / `RECEIVED`); the existing ledger already turns this into a
  `PURCHASE_RECEIPT` stock movement + `StockLevels` update — inventory qty is never edited by hand.

## 3. Site Transfer — lifecycle upgrade
`inventory.StockTransfers` currently posts stock immediately on create. Upgraded to a staged
lifecycle per spec: `REQUESTED → APPROVED → DISPATCHED → RECEIVED` (+ `CANCELLED`).
- Create → `REQUESTED`, no stock movement yet.
- Approve → `APPROVED`.
- Dispatch → deducts stock from `FromWarehouseId` (`TRANSFER_OUT` movement), captures unit cost on
  the line, → `DISPATCHED`.
- Receive → adds stock to `ToWarehouseId` at the captured cost (`TRANSFER_IN` movement) → `RECEIVED`.

## 4. Material Issue
Already implemented (`inventory.MaterialIssues`, `issue.create`) — issues against Project/Work
Order, posts a ledger movement (renamed `MATERIAL_ISSUE`), reduces available stock. No change
needed beyond the ledger movement-type rename below.

## 5. Inventory ledger — movement type naming
`inventory.StockMovements` (already immutable/append-only) movement types aligned to the spec:
`PURCHASE_RECEIPT` (was `RECEIPT`), `TRANSFER_OUT`, `TRANSFER_IN`, `MATERIAL_ISSUE` (was `ISSUE`),
`MATERIAL_RETURN` (new, reserved for a future Returns slice), `ADJUSTMENT`. Manufacturing-only
types (`PRODUCTION_IN`, `CONSUMPTION`, `SCRAP`, `RETURN_IN/OUT`, `OPENING_BALANCE`) unchanged.
Stock is still 100% derived from this ledger — `StockLevels` remains a rebuildable projection,
never directly editable via API.

## 6. Permissions (new, module `inventory`, auto-granted to SUPER_ADMIN/ADMIN by the existing
`inventory.usp_SeedOrganizationInventoryDefaults` proc since it grants by `Module = 'inventory'`):
```
materialrequirement.read / .create / .approve (covers approve+reject) / .convert
purchaseorder.read / .create / .update (covers submit + mark-ordered) / .approve (covers approve+reject) / .close
transfer.approve / .dispatch / .receive   (transfer.read/.create already existed)
```

## 7. API surface (`/api/v1`)
- `POST/GET /material-requirements`, `GET /material-requirements/{id}`,
  `POST /material-requirements/{id}/approve|reject|convert-to-po`
- `POST/GET /purchase-orders`, `GET/PUT /purchase-orders/{id}`,
  `POST /purchase-orders/{id}/submit|approve|reject|mark-ordered|close`
- `POST /stock-transfers/{id}/approve|dispatch|receive` (added to existing endpoints)
- Purchase Receipts = existing `POST/GET /goods-receipts` (now PO-aware)

## 8. Key screens (frontend — next phase, not built in this pass)
Procurement: Material Requirements, Purchase Orders, Suppliers (exists), Purchase Receipts
(exists as Goods Receipts UI), Procurement Dashboard.
Inventory: Stock Overview (exists), Stock by Location (exists), Transfer Requests (upgrade
existing Transfers UI to the staged actions), Material Issues (exists), Inventory Ledger (exists
as movements tab), Purchase Receipts (exists).

## 9. Delivery phases
| Phase | Scope | Status |
|---|---|---|
| 1 | Schema additions + Domain/Application/Infra/Api for Material Requirement + Purchase Order + PO-aware receipts + staged Stock Transfer | **this pass** |
| 2 | Procurement Dashboard (KPIs: open requirements, POs by status, pending receipts) | next |
| 3 | Frontend screens (Material Requirements, Purchase Orders, Transfer Requests actions) | next |
| 4 | Material Return workflow (`MATERIAL_RETURN` ledger type) | next |
