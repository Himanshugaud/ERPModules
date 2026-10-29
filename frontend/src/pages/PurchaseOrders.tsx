import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { api, ApiError, type PurchaseOrder, type Supplier, type InventoryItem, type Warehouse, type InventoryDocument } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate } from "../lib/ui";
import { downloadPurchaseOrderXlsx, parsePurchaseOrderXlsx } from "../lib/sheetExcel";

const STATUS_TABS: { value: string; label: string }[] = [
  { value: "", label: "All" },
  { value: "DRAFT", label: "Draft" },
  { value: "PENDING_APPROVAL", label: "Pending approval" },
  { value: "APPROVED", label: "Approved" },
  { value: "ORDERED", label: "Ordered" },
  { value: "PARTIALLY_RECEIVED", label: "Partly received" },
  { value: "RECEIVED", label: "Received" },
  { value: "CLOSED", label: "Closed" }
];

const RECEIPTS_TAB = "RECEIPTS";
const PRICED_PO_STATUSES = new Set(["ORDERED", "PARTIALLY_RECEIVED", "RECEIVED", "CLOSED"]);

const badge = (s: string) =>
  s === "APPROVED" || s === "RECEIVED" || s === "CLOSED" ? "green"
    : s === "PENDING_APPROVAL" || s === "PARTIALLY_RECEIVED" ? "amber"
    : s === "ORDERED" ? "blue"
    : s === "REJECTED" || s === "CANCELLED" ? "red" : "gray";

export default function PurchaseOrders() {
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const canCreate = perms.includes("purchaseorder.create");
  const canUpdate = perms.includes("purchaseorder.update");
  const canApprove = perms.includes("purchaseorder.approve");
  const canClose = perms.includes("purchaseorder.close");
  const canReceive = perms.includes("goodsreceipt.create");

  const [orders, setOrders] = useState<PurchaseOrder[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [viewing, setViewing] = useState<PurchaseOrder | null>(null);
  const [editing, setEditing] = useState<PurchaseOrder | "new" | null>(null);
  const [ordering, setOrdering] = useState<PurchaseOrder | null>(null);
  const [rejecting, setRejecting] = useState<PurchaseOrder | null>(null);
  const [receipts, setReceipts] = useState<InventoryDocument[]>([]);

  const supplierMap = Object.fromEntries(suppliers.map((s) => [s.id, s]));
  const itemMap = Object.fromEntries(items.map((i) => [i.id, i]));

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const [pos, grns] = await Promise.all([
        api.purchaseOrders({ status: status === RECEIPTS_TAB ? undefined : status || undefined, pageSize: 100 }),
        api.goodsReceipts({ status: "PENDING_APPROVAL", pageSize: 100 }).catch(() => ({ data: [] as InventoryDocument[] }))
      ]);
      setOrders(pos.data);
      setReceipts(grns.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load purchase orders.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.suppliers({ pageSize: 200 }).then((r) => setSuppliers(r.data)).catch(() => setSuppliers([]));
    api.allItems().then((r) => setItems(r.data)).catch(() => setItems([]));
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
  }, []);
  useEffect(() => { load(); }, [status]);

  async function act(po: PurchaseOrder, fn: () => Promise<unknown>, failure: string) {
    setBusyId(po.id);
    setError(null);
    try { await fn(); await load(); }
    catch (err) { setError(err instanceof ApiError ? err.message : failure); }
    finally { setBusyId(null); }
  }

  async function actReceipt(g: InventoryDocument, fn: () => Promise<unknown>, failure: string) {
    setBusyId(g.id);
    setError(null);
    try { await fn(); await load(); }
    catch (err) { setError(err instanceof ApiError ? err.message : failure); }
    finally { setBusyId(null); }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Purchase Orders</h1>
          <div className="page-sub">Raise orders for materials that are not in stock, track them to receipt, then arrange the shipment to site.</div>
        </div>
        <div className="head-actions">
          {canCreate && <button className="btn primary" onClick={() => setEditing("new")}>+ New PO</button>}
          <Link className="btn" to="/inventory/requests">Material Requests →</Link>
        </div>
      </div>

      <div className="tabs" style={{ marginBottom: 14 }}>
        {STATUS_TABS.map((t) => (
          <span key={t.value} className={`tab ${status === t.value ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setStatus(t.value)}>{t.label}</span>
        ))}
        <span className={`tab ${status === RECEIPTS_TAB ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setStatus(RECEIPTS_TAB)}>
          Receipts to approve{receipts.length > 0 && <span className="badge amber" style={{ marginLeft: 6 }}>{receipts.length}</span>}
        </span>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      {status === RECEIPTS_TAB ? (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Receipt #</th><th>Purchase order</th><th>Received into</th><th>Materials received</th><th>Value</th><th></th></tr></thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={6}><div className="empty">Loading…</div></td></tr>
              ) : receipts.length === 0 ? (
                <tr><td colSpan={6}><div className="empty">No receipts waiting for approval. Stock is updated once a receipt is approved.</div></td></tr>
              ) : receipts.map((g) => (
                <tr key={g.id}>
                  <td>{g.number}</td>
                  <td>{g.reference ?? "—"}</td>
                  <td>{warehouses.find((w) => w.id === g.warehouseId)?.name ?? "—"}</td>
                  <td>{(g.lines ?? []).map((l) => `${itemMap[l.itemId]?.name ?? l.itemId} ×${l.qty}`).join(", ")}</td>
                  <td>{g.totalValue.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                  <td>
                    {canApprove ? (
                      <div className="row-actions">
                        <button className="btn btn-sm primary" disabled={busyId === g.id} onClick={() => actReceipt(g, () => api.approveGoodsReceipt(g.id), "Unable to approve the receipt.")}>Approve &amp; add to stock</button>
                        <button className="btn btn-sm danger" disabled={busyId === g.id} onClick={() => { if (window.confirm(`Reject receipt ${g.number}? Stock will not be updated.`)) actReceipt(g, () => api.rejectGoodsReceipt(g.id), "Unable to reject the receipt."); }}>Reject</button>
                      </div>
                    ) : <span className="muted">Awaiting approval</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
      <div className="table-wrap">
        <table>
          <thead><tr><th>PO #</th><th>Supplier</th><th>Expected</th><th>Total</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => <tr key={i}>{[...Array(6)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: 90 }} /></td>)}</tr>)
            ) : orders.length === 0 ? (
              <tr><td colSpan={6}><div className="empty">No purchase orders here.</div></td></tr>
            ) : orders.map((po) => (
              <tr key={po.id} className="clickable" onClick={() => setViewing(po)}>
                <td>{po.poNumber}</td>
                <td>{supplierMap[po.supplierId ?? ""]?.name ?? "Not assigned"}</td>
                <td>{formatDate(po.expectedDate)}</td>
                <td>{PRICED_PO_STATUSES.has(po.status) ? `${po.currencyCode ?? ""} ${po.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })}` : "Not priced"}</td>
                <td><span className={`badge ${badge(po.status)}`}>{po.status.replace(/_/g, " ")}</span></td>
                <td onClick={(e) => e.stopPropagation()}>
                  <div className="row-actions">
                    {po.status === "DRAFT" && canUpdate && <>
                      <button className="btn btn-sm" onClick={() => setEditing(po)}>Edit draft</button>
                      <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => act(po, () => api.submitPurchaseOrder(po.id), "Unable to submit the purchase order.")}>Submit</button>
                    </>}
                    {po.status === "PENDING_APPROVAL" && canApprove && (
                      <>
                        <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => act(po, () => api.approvePurchaseOrder(po.id), "Unable to approve the purchase order.")}>Approve</button>
                        <button className="btn btn-sm danger" onClick={() => setRejecting(po)}>Reject</button>
                      </>
                    )}
                    {po.status === "APPROVED" && canUpdate && <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => setOrdering(po)}>Mark ordered</button>}
                    {(po.status === "ORDERED" || po.status === "PARTIALLY_RECEIVED") && canReceive && <Link className="btn btn-sm primary" to={`/inventory/stock?receivePo=${po.id}`}>Receive at Stock</Link>}
                    {po.status === "RECEIVED" && canClose && <button className="btn btn-sm" disabled={busyId === po.id} onClick={() => act(po, () => api.closePurchaseOrder(po.id), "Unable to close the purchase order.")}>Close</button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      )}

      {viewing && (
        <div className="modal-overlay" onClick={() => setViewing(null)}>
          <div className="modal procurement-modal" style={{ width: "min(760px, calc(100vw - 40px))" }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <div>
                <h3>{viewing.poNumber}</h3>
                <div className="muted" style={{ fontSize: 13 }}>{supplierMap[viewing.supplierId ?? ""]?.name ?? "Not assigned"}</div>
              </div>
              <span className={`badge ${badge(viewing.status)}`}>{viewing.status.replace(/_/g, " ")}</span>
            </div>
            <div className="modal-body">
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Item</th><th>Qty</th><th>Received</th><th>Unit price</th><th>Tax %</th><th>Total</th></tr></thead>
                  <tbody>
                    {viewing.lines.map((l) => (
                      <tr key={l.id}>
                        <td><div className="cell-title">{itemMap[l.itemId]?.name ?? l.itemId}</div><div className="cell-code">{itemMap[l.itemId]?.code ?? ""}</div></td>
                        <td>{l.qty}</td><td>{l.qtyReceived}</td><td>{PRICED_PO_STATUSES.has(viewing.status) ? l.unitPrice : "—"}</td><td>{PRICED_PO_STATUSES.has(viewing.status) ? l.taxRatePercent : "—"}</td><td>{PRICED_PO_STATUSES.has(viewing.status) ? l.lineTotal.toLocaleString(undefined, { maximumFractionDigits: 2 }) : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {PRICED_PO_STATUSES.has(viewing.status) && <div style={{ textAlign: "right", marginTop: 10 }}>Sub-total {viewing.subTotal.toLocaleString()} · Tax {viewing.taxAmount.toLocaleString()} · <strong>Total {viewing.totalAmount.toLocaleString()}</strong></div>}
              {(viewing.status === "RECEIVED" || viewing.status === "CLOSED") && (
                <div className="draft-note" style={{ marginTop: 12 }}><span className="dot" />Goods are in the warehouse. Open the material request and arrange the shipment so the Transport team can send it to site.</div>
              )}
            </div>
            <div className="modal-foot">
              <button className="btn" onClick={() => setViewing(null)}>Close</button>
              {viewing.status === "DRAFT" && canUpdate && <button className="btn primary" onClick={() => { setEditing(viewing); setViewing(null); }}>Edit draft</button>}
              {viewing.status === "APPROVED" && canUpdate && <button className="btn primary" onClick={() => { setOrdering(viewing); setViewing(null); }}>Mark ordered</button>}
            </div>
          </div>
        </div>
      )}

      {editing && <DraftPoModal po={editing === "new" ? null : editing} warehouses={warehouses} items={items} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await load(); }} />}
      {ordering && <OrderPoModal po={ordering} suppliers={suppliers} items={items} onClose={() => setOrdering(null)} onSaved={async () => { setOrdering(null); await load(); }} />}

      {rejecting && (
        <RejectPoModal po={rejecting} onClose={() => setRejecting(null)} onDone={async () => { setRejecting(null); await load(); }} />
      )}
    </>
  );
}

function DraftPoModal(props: { po: PurchaseOrder | null; warehouses: Warehouse[]; items: InventoryItem[]; onClose: () => void; onSaved: () => void }) {
  const [warehouseId, setWarehouseId] = useState(props.po?.warehouseId ?? "");
  const [expectedDate, setExpectedDate] = useState(props.po?.expectedDate ?? "");
  const [notes, setNotes] = useState(props.po?.notes ?? "");
  const [rows, setRows] = useState<{ id?: string; itemId: string; qty: string; uomId?: string; linked?: boolean }[]>(() =>
    props.po?.lines.map(line => ({ id: line.id, itemId: line.itemId, qty: String(line.qty), uomId: line.uomId, linked: !!line.materialRequirementLineId })) ?? [{ itemId: "", qty: "1" }]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [excelBusy, setExcelBusy] = useState(false);
  const excelInput = useRef<HTMLInputElement>(null);

  async function importExcel(file?: File) {
    if (!file) return;
    setError(null);
    setExcelBusy(true);
    try {
      const imported = await parsePurchaseOrderXlsx(file, props.items);
      setRows(imported.map(line => ({ itemId: line.itemId, qty: String(line.qty), uomId: props.items.find(item => item.id === line.itemId)?.baseUomId })));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to read the Excel file.");
    } finally {
      setExcelBusy(false);
      if (excelInput.current) excelInput.current.value = "";
    }
  }

  async function exportExcel(template: boolean) {
    setError(null);
    setExcelBusy(true);
    try {
      const lines = template ? [] : rows.filter(row => row.itemId).map(row => ({ itemId: row.itemId, qty: Number(row.qty) }));
      if (!template && (!lines.length || lines.some(line => !Number.isFinite(line.qty) || line.qty <= 0))) throw new Error("Add an item and positive quantity before downloading the draft.");
      await downloadPurchaseOrderXlsx(lines, props.items, props.po?.poNumber ?? "Purchase Order");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to download the Excel file.");
    } finally { setExcelBusy(false); }
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!rows.length || rows.some(row => !row.itemId || !(Number(row.qty) > 0))) { setError("Add an item and positive quantity on every line."); return; }
    setSaving(true);
    try {
      const payload = { warehouseId: warehouseId || undefined, expectedDate: expectedDate || undefined, notes: notes.trim() || undefined,
        rowVersion: props.po?.rowVersion, lines: rows.map(row => ({ id: row.id, itemId: row.itemId, qty: Number(row.qty), uomId: row.uomId })) };
      if (props.po) await api.updatePurchaseOrder(props.po.id, payload);
      else await api.createPurchaseOrder(payload);
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save the purchase order draft.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal procurement-modal" style={{ width: "min(760px, calc(100vw - 40px))" }} onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>{props.po ? `Edit draft · ${props.po.poNumber}` : "New purchase order"}</h3><button type="button" className="icon-btn" aria-label="Close" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Receiving warehouse</label><select value={warehouseId} onChange={e => setWarehouseId(e.target.value)}><option value="">Not selected</option>{props.warehouses.map(warehouse => <option key={warehouse.id} value={warehouse.id}>{warehouse.name}</option>)}</select></div>
            <div className="field"><label>Expected delivery</label><input type="date" value={expectedDate} onChange={e => setExpectedDate(e.target.value)} /></div>
          </div>
          <div className="field"><label>Notes</label><input value={notes} onChange={e => setNotes(e.target.value)} maxLength={1000} /></div>
          <div className="head-actions" style={{ flexWrap: "wrap", marginBottom: 10 }}>
            {!props.po && <>
              <input ref={excelInput} type="file" accept=".xlsx" hidden aria-label="Upload PO Excel" onChange={event => importExcel(event.target.files?.[0])} />
              <button type="button" className="btn btn-sm" disabled={saving || excelBusy || !props.items.length} onClick={() => excelInput.current?.click()}>⭱ Upload Excel</button>
            </>}
            <button type="button" className="btn btn-sm" disabled={saving || excelBusy || !props.items.length} onClick={() => exportExcel(true)}>⭳ Download template</button>
            <button type="button" className="btn btn-sm" disabled={saving || excelBusy} onClick={() => exportExcel(false)}>⭳ Download Excel</button>
          </div>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Item</th><th>Quantity</th><th></th></tr></thead>
              <tbody>
                {rows.map((row, index) => <tr key={row.id ?? index}>
                  <td><select aria-label={`Item ${index + 1}`} required value={row.itemId} disabled={row.linked} onChange={e => setRows(current => current.map((entry, position) => position === index ? { ...entry, itemId: e.target.value, uomId: props.items.find(item => item.id === e.target.value)?.baseUomId } : entry))} style={{ maxWidth: 400 }}>
                    <option value="">Select item</option>{props.items.map(item => <option key={item.id} value={item.id}>{item.code} - {item.name}</option>)}
                  </select></td>
                  <td><input aria-label={`Quantity ${index + 1}`} type="number" min="0.0001" step="0.0001" required value={row.qty} style={{ width: 100 }} onChange={e => setRows(current => current.map((entry, position) => position === index ? { ...entry, qty: e.target.value } : entry))} /></td>
                  <td><button type="button" className="icon-btn" aria-label={`Remove item ${index + 1}`} title="Remove item" onClick={() => setRows(current => current.filter((_, position) => position !== index))}>×</button></td>
                </tr>)}
              </tbody>
            </table>
          </div>
          <button type="button" className="btn btn-sm" style={{ marginTop: 10 }} onClick={() => setRows(current => [...current, { itemId: "", qty: "1" }])}>+ Add item</button>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving || excelBusy}>{saving ? <span className="spinner" /> : "Save draft"}</button>
        </div>
      </form>
    </div>
  );
}

function OrderPoModal(props: { po: PurchaseOrder; suppliers: Supplier[]; items: InventoryItem[]; onClose: () => void; onSaved: () => void }) {
  const [supplierId, setSupplierId] = useState(props.po.supplierId ?? "");
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10));
  const [expectedDate, setExpectedDate] = useState(props.po.expectedDate ?? "");
  const [currencyCode, setCurrencyCode] = useState(props.po.currencyCode ?? "INR");
  const [rows, setRows] = useState(() => props.po.lines.map(line => ({ lineId: line.id, itemId: line.itemId, qty: line.qty, unitPrice: "", taxRatePercent: "0" })));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const selectedSupplier = props.suppliers.find(supplier => supplier.id === supplierId);
  const total = rows.reduce((sum, row) => sum + row.qty * Number(row.unitPrice || 0) * (1 + Number(row.taxRatePercent || 0) / 100), 0);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!supplierId || rows.some(row => !(Number(row.unitPrice) > 0))) { setError("Select a supplier and enter a positive price for each item."); return; }
    setSaving(true);
    setError(null);
    try {
      await api.markPurchaseOrderOrdered(props.po.id, { supplierId, orderDate, expectedDate: expectedDate || undefined, currencyCode, rowVersion: props.po.rowVersion,
        lines: rows.map(row => ({ lineId: row.lineId, unitPrice: Number(row.unitPrice), taxRatePercent: Number(row.taxRatePercent || 0) })) });
      props.onSaved();
    } catch (err) { setError(err instanceof ApiError ? err.message : "Unable to mark the purchase order as ordered."); }
    finally { setSaving(false); }
  }

  return <div className="modal-overlay" onClick={props.onClose}>
    <form className="modal procurement-modal" style={{ width: "min(820px, calc(100vw - 40px))" }} onClick={event => event.stopPropagation()} onSubmit={submit}>
      <div className="modal-head"><h3>Mark ordered · {props.po.poNumber}</h3><button type="button" className="icon-btn" aria-label="Close" onClick={props.onClose}>✕</button></div>
      <div className="modal-body">
        {error && <div className="form-error">{error}</div>}
        <div className="field"><label>Supplier *</label><select aria-label="Supplier" required value={supplierId} onChange={event => setSupplierId(event.target.value)}><option value="">Select supplier</option>{props.suppliers.map(supplier => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}</select></div>
        {selectedSupplier && <div className="muted" style={{ marginBottom: 12 }}>{[selectedSupplier.email, selectedSupplier.phone, selectedSupplier.address].filter(Boolean).join(" · ")}</div>}
        <div className="row2">
          <div className="field"><label>Order date *</label><input type="date" required value={orderDate} onChange={event => setOrderDate(event.target.value)} /></div>
          <div className="field"><label>Expected delivery</label><input type="date" value={expectedDate} onChange={event => setExpectedDate(event.target.value)} /></div>
        </div>
        <div className="field"><label>Currency *</label><input required minLength={3} maxLength={3} value={currencyCode} onChange={event => setCurrencyCode(event.target.value.toUpperCase())} /></div>
        <div className="table-wrap"><table><thead><tr><th>Item</th><th>Qty</th><th>Unit price *</th><th>Tax %</th></tr></thead><tbody>
          {rows.map((row, index) => <tr key={row.lineId}><td>{props.items.find(item => item.id === row.itemId)?.name ?? row.itemId}</td><td>{row.qty}</td>
            <td><input aria-label={`Unit price ${index + 1}`} type="number" required min="0.0001" step="0.0001" value={row.unitPrice} style={{ width: 110 }} onChange={event => setRows(current => current.map((entry, position) => position === index ? { ...entry, unitPrice: event.target.value } : entry))} /></td>
            <td><input aria-label={`Tax rate ${index + 1}`} type="number" min={0} max={100} step="0.0001" value={row.taxRatePercent} style={{ width: 90 }} onChange={event => setRows(current => current.map((entry, position) => position === index ? { ...entry, taxRatePercent: event.target.value } : entry))} /></td>
          </tr>)}
        </tbody></table></div>
        <div style={{ textAlign: "right", marginTop: 12 }}><strong>Total {currencyCode} {total.toLocaleString(undefined, { maximumFractionDigits: 2 })}</strong></div>
      </div>
      <div className="modal-foot"><button type="button" className="btn" onClick={props.onClose}>Cancel</button><button className="btn primary" disabled={saving}>{saving ? "Saving..." : "Mark ordered"}</button></div>
    </form>
  </div>;
}

function RejectPoModal(props: { po: PurchaseOrder; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!reason.trim()) { setError("A reason is required."); return; }
    setSaving(true);
    try { await api.rejectPurchaseOrder(props.po.id, reason.trim()); props.onDone(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Unable to reject."); setSaving(false); }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>Reject {props.po.poNumber}</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field"><label>Reason *</label><input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} required /></div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn danger" disabled={saving}>{saving ? <span className="spinner" /> : "Reject"}</button>
        </div>
      </form>
    </div>
  );
}
