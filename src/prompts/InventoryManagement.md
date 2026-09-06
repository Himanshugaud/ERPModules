# Inventory Management Module — Feature & Build Plan

Target business: **Manufacturing + Construction company** that stocks both **raw materials**
(cement, steel, aggregates, chemicals, timber, components) and **finished / manufactured
products** (fabricated units, precast items, assembled goods), consumed on **projects / sites**
and produced via **work orders**.

This module plugs into the existing modular-monolith ERP (schemas `core`, `project`, `shared`)
and follows the same conventions already used by Project Management:

- Multi-tenant: every table has `OrganizationId`; queries filtered by `ITenantContext`.
- GUID PKs (`DEFAULT NEWSEQUENTIALID()`), UTC audit columns, soft-delete where appropriate.
- Vertical slices across `ERP.Domain` / `ERP.Application` / `ERP.Infrastructure` / `ERP.Api`.
- `/api/v1` routes, standard `{ data, pagination }` / `{ error }` envelope, permission-based authz.
- **New SQL schema: `inventory`** (keeps module boundaries clean; no changes to existing tables).
- Ledger-based stock: on-hand is **derived from an immutable movement ledger**, never edited directly.

---

## 1. Why these features are critical (business drivers)

| Driver | What it demands from the module |
|---|---|
| Two stock natures (raw vs finished) | Item typing, separate valuation, BOM linking raw → finished |
| Manufacturing | Bill of Materials, Work/Production Orders, WIP, scrap/by-product |
| Construction | Site/project-based issue, material indents, per-project consumption & wastage |
| Cost control | Valuation (Weighted Avg / FIFO / Standard), stock aging, ABC analysis |
| Compliance / quality | Batch/lot + expiry (cement, chemicals, paint), serial tracking (equipment), QC hold |
| Avoiding stockouts / overstock | Reorder levels, safety stock, low-stock & expiry alerts, reorder suggestions |
| Auditability | Immutable stock ledger + `shared.AuditLogs`, physical counts, adjustments with reason |
| Procurement readiness | Suppliers, item-supplier prices/lead time, PO → GRN flow (bridges future Procurement module) |

---

## 2. Data model (new `inventory` schema)

### 2.1 Master data
- **inventory.ItemCategories** — hierarchical grouping (ParentCategoryId), e.g. Cement > OPC/PPC.
- **inventory.UnitsOfMeasure** — Code, Name, BaseUnit flag (KG, TON, BAG, M3, NOS, LTR).
- **inventory.UomConversions** — FromUomId, ToUomId, Factor (1 TON = 1000 KG; 1 BAG = 50 KG).
- **inventory.Items** — the catalog. Key columns:
  - `ItemType` (RAW_MATERIAL, FINISHED_GOOD, SEMI_FINISHED / WIP, CONSUMABLE, SPARE_PART, TOOL_EQUIPMENT)
  - `CategoryId`, `BaseUomId`, `Sku`/`Code` (unique per org), `Barcode`
  - `TrackBatches` (bit), `TrackSerials` (bit), `TrackExpiry` (bit)
  - `ValuationMethod` (WEIGHTED_AVG default | FIFO | STANDARD), `StandardCost`
  - `ReorderLevel`, `SafetyStock`, `MinStock`, `MaxStock`, `ReorderQty`
  - `IsPurchasable`, `IsManufactured`, `IsSellable`, `IsActive`, soft-delete cols
- **inventory.Warehouses** — plant stores + construction site stores. `WarehouseType`
  (MAIN_STORE, SITE_STORE, PRODUCTION_STORE, TRANSIT, SCRAP), optional `ProjectId`
  (links a site store to a `project.Projects` row), address.
- **inventory.StorageBins** — optional bin/rack locations within a warehouse.
- **inventory.Suppliers** — vendors (lightweight now; graduates to Procurement module later).
- **inventory.ItemSuppliers** — preferred supplier, `LeadTimeDays`, `LastPrice`, `SupplierSku`.

### 2.2 Stock & lots
- **inventory.StockLevels** — cached balance per (ItemId, WarehouseId, optional BinId, optional BatchId):
  `QtyOnHand`, `QtyReserved`, `QtyAvailable` (computed), `QtyInTransit`, `AvgUnitCost`, `RowVersion`.
  Rebuildable from the ledger; kept for fast reads.
- **inventory.Batches** — BatchNo/LotNo, `ManufactureDate`, `ExpiryDate`, `SupplierId`, QC status.
- **inventory.SerialNumbers** — for serialized equipment/tools (SerialNo, current WarehouseId, status).

### 2.3 Transactions (movement ledger + documents)
- **inventory.StockMovements** — the **immutable ledger** (append-only). One row per line impact:
  `ItemId, WarehouseId, BinId, BatchId, SerialId, MovementType, Direction (IN/OUT),
  Qty, UnitCost, TotalCost, RefDocType, RefDocId, RefDocLineId, ProjectId, OccurredAt`.
  MovementTypes: RECEIPT, ISSUE, TRANSFER_IN, TRANSFER_OUT, ADJUSTMENT, PRODUCTION_IN,
  CONSUMPTION, RETURN_IN, RETURN_OUT, SCRAP, OPENING_BALANCE.
- **inventory.PurchaseOrders / PurchaseOrderLines** — raise POs to suppliers (status DRAFT→APPROVED→RECEIVED→CLOSED).
- **inventory.GoodsReceipts / GoodsReceiptLines (GRN)** — receive against PO or direct; creates RECEIPT movements + batches.
- **inventory.MaterialIssues / MaterialIssueLines** — issue to production or to a project/site; creates ISSUE/CONSUMPTION movements.
- **inventory.MaterialRequisitions / Lines (Indent)** — site/production request for material (approval workflow → issue).
- **inventory.StockTransfers / Lines** — warehouse↔warehouse / store↔site (TRANSFER_OUT then TRANSFER_IN, with in-transit).
- **inventory.StockAdjustments / Lines** — physical-count corrections, damage, wastage; requires `ReasonCode`.
- **inventory.Returns / Lines** — return to supplier (RETURN_OUT) or return from site/production (RETURN_IN).

### 2.4 Manufacturing
- **inventory.BillsOfMaterials / BomLines** — a finished/semi item's recipe: component ItemId, Qty per unit, scrap %, optional operation.
- **inventory.WorkOrders / WorkOrderComponents** — production order for an output item & qty; reserves & consumes raw materials, yields PRODUCTION_IN of finished goods, tracks WIP, scrap and by-products; optional `ProjectId`.

### 2.5 Counting
- **inventory.CycleCounts / CycleCountLines** — scheduled/physical counts; variance posts an ADJUSTMENT movement.

> Reuse existing shared tables: `shared.Documents` (attach GRN/invoice scans via Blob),
> `shared.Comments`, `shared.Notifications` (low-stock/expiry alerts),
> `shared.AuditLogs`, `shared.OutboxMessages` (async events).

---

## 3. Domain events (via `shared.OutboxMessages` → Service Bus)
- `StockReceived`, `StockIssued`, `StockTransferred`, `StockAdjusted`
- `ReorderLevelBreached`, `SafetyStockBreached`, `BatchNearingExpiry`, `BatchExpired`
- `WorkOrderReleased`, `WorkOrderCompleted`, `MaterialRequisitionApproved`
- Consumers: notifications, reorder-suggestion generation, project-cost roll-up, dashboards.

---

## 4. API surface (`/api/v1`, permission-gated)

**Master data**
- `GET/POST /items`, `GET/PUT/DELETE /items/{id}`, `GET /items/{id}/stock` (levels across warehouses)
- `GET/POST /item-categories`, `GET/POST /units-of-measure`, `GET/POST /uom-conversions`
- `GET/POST /warehouses`, `GET/PUT/DELETE /warehouses/{id}`, `GET/POST /warehouses/{id}/bins`
- `GET/POST /suppliers`, `GET/PUT/DELETE /suppliers/{id}`, `GET/POST /items/{id}/suppliers`

**Stock & lookups**
- `GET /stock` (filter item/warehouse/batch, on-hand/available), `GET /stock/valuation`
- `GET /stock/movements` (ledger, filter by item/warehouse/project/date/type)
- `GET /items/{id}/batches`, `GET /items/{id}/serials`
- `GET /stock/low` (below reorder/safety), `GET /stock/expiring?days=30`

**Transactions**
- `POST /purchase-orders`, `GET /purchase-orders`, `GET/PUT /purchase-orders/{id}`, `POST /purchase-orders/{id}/approve`
- `POST /goods-receipts` (against PO or direct), `GET /goods-receipts`, `GET /goods-receipts/{id}`
- `POST /material-requisitions`, `POST /material-requisitions/{id}/approve`, `GET .../{id}`
- `POST /material-issues` (to project/work-order), `GET /material-issues`, `GET .../{id}`
- `POST /stock-transfers`, `POST /stock-transfers/{id}/receive`, `GET .../{id}`
- `POST /stock-adjustments`, `GET /stock-adjustments`
- `POST /returns` (supplier/site), `GET /returns`

**Manufacturing**
- `GET/POST /boms`, `GET/PUT/DELETE /boms/{id}`
- `POST /work-orders`, `POST /work-orders/{id}/release`, `POST /work-orders/{id}/consume`,
  `POST /work-orders/{id}/complete`, `GET /work-orders`, `GET .../{id}`

**Counting & reports**
- `POST /cycle-counts`, `POST /cycle-counts/{id}/post`
- `GET /reports/stock-summary`, `/reports/stock-aging`, `/reports/abc-analysis`,
  `/reports/consumption?projectId=`, `/reports/inventory-valuation`

**Project integration**
- `GET /projects/{projectId}/materials` (consumption + cost roll-up for construction sites)

All write endpoints validate org ownership, use FluentValidation, run inside `IUnitOfWork`
(concurrency → 409), write to `shared.AuditLogs`, and emit outbox events.

---

## 5. Permissions (new, extend `core.Permissions`)
```
item.read/create/update/delete
warehouse.read/create/update/delete
stock.read
stock.adjust
supplier.read/create/update/delete
purchaseorder.read/create/update/approve
goodsreceipt.read/create
requisition.read/create/approve
issue.read/create
transfer.read/create/receive
return.read/create
bom.read/create/update/delete
workorder.read/create/release/complete
cyclecount.read/create/post
inventory.report.read
```
Seeded into `core.usp_SeedOrganizationDefaults` and mapped to roles
(Store Keeper, Purchase Officer, Production Manager, Site Engineer, Inventory Manager, Admin).

---

## 6. Frontend (Vite + React, mirrors current pages)
- **Inventory nav group**: Items, Stock, Warehouses, Suppliers, Purchase Orders,
  Goods Receipts, Requisitions, Issues, Transfers, Adjustments, BOMs, Work Orders, Reports.
- **Items** — list (type/category/low-stock filters) + create/edit (tracking flags, reorder levels, UOM).
- **Stock** — on-hand grid per item/warehouse, batch/expiry drill-down, low-stock & expiring tabs.
- **GRN / Issue / Transfer / Adjustment** — line-item document forms with warehouse + batch pickers.
- **Work Orders** — output item + BOM explosion, reserve/consume/complete steps, WIP view.
- **Project → Materials** tab on ProjectDetail: requisitions, issues, and material cost for the site.
- **Dashboard widgets**: total stock value, low-stock count, expiring soon, pending POs/GRNs.

---

## 7. Delivery phases (incremental vertical slices)

| Phase | Scope | Outcome |
|---|---|---|
| **0. Schema & scaffolding** | `inventory` schema, EF configs, permissions seed, lookups (UOM, categories) | Foundations, no behaviour change |
| **1. Master data** | Items, Categories, UOM + conversions, Warehouses, Bins, Suppliers, Item-Suppliers | Catalog manageable end-to-end |
| **2. Opening stock & ledger** | StockMovements ledger, StockLevels, opening balances, `GET /stock` + valuation | Real-time on-hand & valuation |
| **3. Inbound** | Purchase Orders, GRN, Batches/Serials, RECEIPT movements | Receiving with lots/expiry |
| **4. Outbound** | Requisitions, Material Issues (to project/WO), Transfers (in-transit), Returns | Issue/transfer to sites & production |
| **5. Manufacturing** | BOM, Work Orders, consumption, PRODUCTION_IN, scrap/WIP | Raw → finished conversion |
| **6. Adjustments & counting** | Stock adjustments (reason codes), Cycle counts, variance posting | Physical accuracy & audit |
| **7. Alerts & reports** | Reorder/safety/expiry events + notifications, stock aging, ABC, consumption, valuation reports | Decision support |
| **8. Project integration** | Per-project material consumption & cost roll-up on ProjectDetail | Construction cost visibility |
| **9. Frontend & polish** | Inventory UI screens, dashboard widgets, role wiring | Usable module |

---

## 8. Design guardrails
- **Never edit stock directly** — every change is a ledger movement; `StockLevels` is a rebuildable projection.
- **Atomic postings** — a GRN/issue/transfer writes document + all movements + level updates in one `IUnitOfWork` transaction; `RowVersion` guards concurrent stock updates (→ 409).
- **No negative stock** unless org setting `AllowNegativeStock` is on; validate availability on issue.
- **Batch/serial enforced** by item flags; expiry blocks issuing expired lots (configurable).
- **Costing** centralised in a `IStockValuationService` (Weighted-Avg default; FIFO/Standard pluggable).
- **Tenant isolation** on every query; `ProjectId` on movements ties consumption to construction sites.
- **No changes to existing `core`/`project`/`shared` tables** — only additive `inventory` schema + permission rows.
```
```

Ready to start with **Phase 0 (schema + scaffolding)** on your confirmation.
