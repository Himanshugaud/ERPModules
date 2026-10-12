import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { api, ApiError, type PurchaseOrder, type Supplier, type Project, type InventoryItem, type Warehouse, type InventoryDocument } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate } from "../lib/ui";

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

const badge = (s: string) =>
  s === "APPROVED" || s === "RECEIVED" || s === "CLOSED" ? "green"
    : s === "PENDING_APPROVAL" || s === "PARTIALLY_RECEIVED" ? "amber"
    : s === "ORDERED" ? "blue"
    : s === "REJECTED" || s === "CANCELLED" ? "red" : "gray";

export default function PurchaseOrders() {
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const canUpdate = perms.includes("purchaseorder.update");
  const canApprove = perms.includes("purchaseorder.approve");
  const canClose = perms.includes("purchaseorder.close");
  const canReceive = perms.includes("goodsreceipt.create");

  const [orders, setOrders] = useState<PurchaseOrder[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [viewing, setViewing] = useState<PurchaseOrder | null>(null);
  const [receiving, setReceiving] = useState<PurchaseOrder | null>(null);
  const [rejecting, setRejecting] = useState<PurchaseOrder | null>(null);
  const [receipts, setReceipts] = useState<InventoryDocument[]>([]);

  const supplierMap = Object.fromEntries(suppliers.map((s) => [s.id, s]));
  const projectMap = Object.fromEntries(projects.map((p) => [p.id, p]));
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
    api.projects({ pageSize: 200 }).then((r) => setProjects(r.data)).catch(() => setProjects([]));
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
          <thead><tr><th>PO #</th><th>Supplier</th><th>Project</th><th>Expected</th><th>Total</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => <tr key={i}>{[...Array(7)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: 90 }} /></td>)}</tr>)
            ) : orders.length === 0 ? (
              <tr><td colSpan={7}><div className="empty">No purchase orders here. Create one from an approved material request.</div></td></tr>
            ) : orders.map((po) => (
              <tr key={po.id} className="clickable" onClick={() => setViewing(po)}>
                <td>{po.poNumber}</td>
                <td>{supplierMap[po.supplierId]?.name ?? "—"}</td>
                <td>{po.projectId ? (projectMap[po.projectId]?.name ?? "—") : "—"}</td>
                <td>{formatDate(po.expectedDate)}</td>
                <td>{po.currencyCode ?? ""} {po.totalAmount.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                <td><span className={`badge ${badge(po.status)}`}>{po.status.replace(/_/g, " ")}</span></td>
                <td onClick={(e) => e.stopPropagation()}>
                  <div className="row-actions">
                    {po.status === "DRAFT" && canUpdate && <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => act(po, () => api.submitPurchaseOrder(po.id), "Unable to submit the purchase order.")}>Submit</button>}
                    {po.status === "PENDING_APPROVAL" && canApprove && (
                      <>
                        <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => act(po, () => api.approvePurchaseOrder(po.id), "Unable to approve the purchase order.")}>Approve</button>
                        <button className="btn btn-sm danger" onClick={() => setRejecting(po)}>Reject</button>
                      </>
                    )}
                    {po.status === "APPROVED" && canUpdate && <button className="btn btn-sm primary" disabled={busyId === po.id} onClick={() => act(po, () => api.markPurchaseOrderOrdered(po.id), "Unable to mark the order as placed.")}>Mark ordered</button>}
                    {(po.status === "ORDERED" || po.status === "PARTIALLY_RECEIVED") && canReceive && <button className="btn btn-sm primary" onClick={() => setReceiving(po)}>Receive goods</button>}
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
          <div className="modal" style={{ maxWidth: 760 }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <div>
                <h3>{viewing.poNumber}</h3>
                <div className="muted" style={{ fontSize: 13 }}>{supplierMap[viewing.supplierId]?.name ?? "—"}{viewing.projectId ? ` · ${projectMap[viewing.projectId]?.name ?? ""}` : ""}</div>
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
                        <td>{l.qty}</td><td>{l.qtyReceived}</td><td>{l.unitPrice}</td><td>{l.taxRatePercent}</td><td>{l.lineTotal.toLocaleString(undefined, { maximumFractionDigits: 2 })}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div style={{ textAlign: "right", marginTop: 10 }}>Sub-total {viewing.subTotal.toLocaleString()} · Tax {viewing.taxAmount.toLocaleString()} · <strong>Total {viewing.totalAmount.toLocaleString()}</strong></div>
              {(viewing.status === "RECEIVED" || viewing.status === "CLOSED") && (
                <div className="draft-note" style={{ marginTop: 12 }}><span className="dot" />Goods are in the warehouse. Open the material request and arrange the shipment so the Transport team can send it to site.</div>
              )}
            </div>
            <div className="modal-foot"><button className="btn" onClick={() => setViewing(null)}>Close</button></div>
          </div>
        </div>
      )}

      {receiving && (
        <ReceiveModal po={receiving} warehouses={warehouses} items={items} onClose={() => setReceiving(null)} onSaved={async () => { setReceiving(null); setStatus(RECEIPTS_TAB); await load(); }} />
      )}

      {rejecting && (
        <RejectPoModal po={rejecting} onClose={() => setRejecting(null)} onDone={async () => { setRejecting(null); await load(); }} />
      )}
    </>
  );
}

function ReceiveModal(props: { po: PurchaseOrder; warehouses: Warehouse[]; items: InventoryItem[]; onClose: () => void; onSaved: () => void }) {
  const itemMap = Object.fromEntries(props.items.map((i) => [i.id, i]));
  const [warehouseId, setWarehouseId] = useState(props.po.warehouseId ?? props.warehouses.find((w) => w.warehouseType === "MAIN_STORE")?.id ?? props.warehouses[0]?.id ?? "");
  const [rows, setRows] = useState(() =>
    props.po.lines.map((l) => ({ itemId: l.itemId, remaining: Math.max(0, l.qty - l.qtyReceived), qty: String(Math.max(0, l.qty - l.qtyReceived)), unitCost: l.unitPrice }))
  );
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    const lines = rows.filter((r) => Number(r.qty) > 0);
    if (!warehouseId) { setError("Select the warehouse receiving the goods."); return; }
    if (!lines.length) { setError("Enter a received quantity for at least one item."); return; }
    if (lines.some((r) => Number(r.qty) > r.remaining)) { setError("Received quantity cannot exceed the quantity still pending on the order."); return; }
    setSaving(true);
    try {
      await api.createGoodsReceipt({
        warehouseId,
        poReference: props.po.poNumber,
        purchaseOrderId: props.po.id,
        supplierId: props.po.supplierId,
        lines: lines.map((r) => ({ itemId: r.itemId, qty: Number(r.qty), unitCost: r.unitCost }))
      });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to post the goods receipt.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" style={{ maxWidth: 680 }} onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>Receive goods · {props.po.poNumber}</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="muted" style={{ fontSize: 12, marginBottom: 10 }}>Stock is updated only after this receipt is approved.</div>
          <div className="field">
            <label>Receiving warehouse *</label>
            <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
              <option value="">Select warehouse…</option>
              {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
            </select>
          </div>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Item</th><th>Pending</th><th>Receive qty</th></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><div className="cell-title">{itemMap[r.itemId]?.name ?? r.itemId}</div><div className="cell-code">{itemMap[r.itemId]?.code ?? ""}</div></td>
                    <td>{r.remaining}</td>
                    <td><input type="number" min={0} max={r.remaining} step="0.01" value={r.qty} disabled={r.remaining <= 0} style={{ width: 100 }}
                      onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, qty: e.target.value } : x)))} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Submit for approval"}</button>
        </div>
      </form>
    </div>
  );
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
