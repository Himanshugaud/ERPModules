import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import {
  api, ApiError,
  type MaterialRequirement, type Warehouse, type InventoryItem, type Project, type UserItem, type Uom, type StockLevel, type InventoryDocument
} from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate, formatDateTime } from "../lib/ui";

type ArrangeLine = { itemId: string; uomId?: string; requestedQty: number; remainingQty: number; shipQty: string; include: boolean };

export default function InventoryRequests() {
  const { user } = useAuth();
  const canApprove = (user?.permissions ?? []).includes("materialrequirement.approve");
  const canArrange = (user?.permissions ?? []).includes("transfer.create");
  const [requirements, setRequirements] = useState<MaterialRequirement[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [users, setUsers] = useState<UserItem[]>([]);
  const [stock, setStock] = useState<StockLevel[]>([]);
  const [status, setStatus] = useState("SUBMITTED");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<MaterialRequirement | null>(null);
  const [arranging, setArranging] = useState<MaterialRequirement | null>(null);
  const [viewing, setViewing] = useState<MaterialRequirement | null>(null);

  const warehouseMap = Object.fromEntries(warehouses.map((w) => [w.id, w]));
  const projectMap = Object.fromEntries(projects.map((p) => [p.id, p]));

  // Available qty per item summed across every warehouse, and per item+warehouse for source selection.
  const stockByItem: Record<string, number> = {};
  const stockByItemWarehouse: Record<string, number> = {};
  for (const s of stock) {
    stockByItem[s.itemId] = (stockByItem[s.itemId] ?? 0) + s.qtyAvailable;
    const key = `${s.itemId}|${s.warehouseId}`;
    stockByItemWarehouse[key] = (stockByItemWarehouse[key] ?? 0) + s.qtyAvailable;
  }

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.materialRequirements({ status: status || undefined, pageSize: 100 });
      setRequirements(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load material requests.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
    api.items({ pageSize: 200 }).then((r) => setItems(r.data)).catch(() => setItems([]));
    api.uoms().then(setUoms).catch(() => setUoms([]));
    api.projects({ pageSize: 200 }).then((r) => setProjects(r.data)).catch(() => setProjects([]));
    api.users().then(setUsers).catch(() => setUsers([]));
    api.stock({ pageSize: 1000 }).then((r) => setStock(r.data)).catch(() => setStock([]));
  }, []);
  useEffect(() => { load(); }, [status]);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Material Requests</h1>
          <div className="page-sub">Review estimates from Planning, approve them, and arrange stock for shipment.</div>
        </div>
        <div className="head-actions">
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="SUBMITTED">Pending Review</option>
            <option value="APPROVED">Approved (ready to arrange)</option>
            <option value="REJECTED">Rejected</option>
            <option value="">All</option>
          </select>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Req #</th><th>Project</th><th>Destination</th><th>Priority</th><th>Materials</th><th>Required</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => (
                <tr key={i}>{[...Array(8)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 120 : 90 }} /></td>)}</tr>
              ))
            ) : requirements.length === 0 ? (
              <tr><td colSpan={8}><div className="empty">Nothing here. New estimates from Planning will show up automatically.</div></td></tr>
            ) : (
              requirements.map((r) => (
                <tr key={r.id} className="clickable" onClick={() => setViewing(r)}>
                  <td>{r.reqNumber}</td>
                  <td>
                    <div className="cell-title">{projectMap[r.projectId]?.name ?? "—"}</div>
                    <div className="cell-code">{projectMap[r.projectId]?.code ?? ""}</div>
                  </td>
                  <td>{r.destinationAddress || warehouseMap[r.warehouseId ?? ""]?.name || "—"}</td>
                  <td>{r.priority}</td>
                  <td className="muted">{r.lines.length} material{r.lines.length === 1 ? "" : "s"}</td>
                  <td>{formatDate(r.requiredDate)}</td>
                  <td><span className={`badge ${r.status === "SUBMITTED" ? "blue" : r.status === "APPROVED" ? "green" : r.status === "REJECTED" ? "red" : "gray"}`}>{r.status}</span></td>
                  <td>
                    <div className="row-actions">
                      {r.status === "SUBMITTED" && canApprove && (
                        <>
                          <button className="btn btn-sm primary" onClick={(e) => { e.stopPropagation(); setArranging(r); }}>Approve</button>
                          <button className="btn btn-sm danger" onClick={(e) => { e.stopPropagation(); setRejecting(r); }}>Reject</button>
                        </>
                      )}
                      {r.status === "APPROVED" && canArrange && (
                        <button className="btn btn-sm primary" onClick={(e) => { e.stopPropagation(); setArranging(r); }}>Arrange Shipment</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {rejecting && (
        <RejectModal
          requirement={rejecting}
          onClose={() => setRejecting(null)}
          onRejected={async () => { setRejecting(null); await load(); }}
        />
      )}

      {arranging && (
        <ArrangeTransferModal
          requirement={arranging}
          warehouses={warehouses}
          items={items}
          uoms={uoms}
          stockByItemWarehouse={stockByItemWarehouse}
          onClose={() => setArranging(null)}
          onArranged={async () => { setArranging(null); await load(); }}
        />
      )}

      {viewing && (
        <RequestDetailModal
          requirement={viewing}
          project={projectMap[viewing.projectId]}
          warehouses={warehouses}
          items={items}
          uoms={uoms}
          users={users}
          stockByItem={stockByItem}
          canApprove={canApprove}
          canArrange={canArrange}
          onClose={() => setViewing(null)}
          onReject={() => { setRejecting(viewing); setViewing(null); }}
          onArrange={() => { setArranging(viewing); setViewing(null); }}
        />
      )}
    </>
  );
}

function RequestDetailModal(props: {
  requirement: MaterialRequirement;
  project?: Project;
  warehouses: Warehouse[];
  items: InventoryItem[];
  uoms: Uom[];
  users: UserItem[];
  stockByItem: Record<string, number>;
  canApprove: boolean;
  canArrange: boolean;
  onClose: () => void;
  onReject: () => void;
  onArrange: () => void;
}) {
  const r = props.requirement;
  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));
  const userMap = Object.fromEntries(props.users.map((u) => [u.id, u]));
  const [shipments, setShipments] = useState<InventoryDocument[]>([]);
  const [loadingShipments, setLoadingShipments] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoadingShipments(true);
    api.stockTransfers({ materialRequirementId: r.id, pageSize: 50 })
      .then((res) => { if (!cancelled) setShipments(res.data); })
      .catch(() => { if (!cancelled) setShipments([]); })
      .finally(() => { if (!cancelled) setLoadingShipments(false); });
    return () => { cancelled = true; };
  }, [r.id]);

  const requesterName = userMap[r.requestedBy ?? ""]?.displayName ?? userMap[r.requestedBy ?? ""]?.email ?? "—";
  const meta: { label: string; value: string }[] = [
    { label: "Destination", value: r.destinationAddress || props.warehouses.find((w) => w.id === r.warehouseId)?.name || "—" },
    { label: "Priority", value: r.priority },
    { label: "Required Date", value: formatDate(r.requiredDate) },
    { label: "Requested By", value: requesterName },
    { label: "Created", value: formatDateTime(r.createdAt) }
  ];

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 840 }}>
        <div className="modal-head">
          <div>
            <h3>{r.reqNumber}</h3>
            <div className="muted" style={{ fontSize: 13 }}>
              {props.project ? (
                <Link to={`/projects/${props.project.id}`} onClick={(e) => e.stopPropagation()}>{props.project.name} · {props.project.code}</Link>
              ) : "No linked project"}
            </div>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span className={`badge ${r.status === "SUBMITTED" ? "blue" : r.status === "APPROVED" ? "green" : r.status === "REJECTED" ? "red" : "gray"}`}>{r.status}</span>
            <button className="icon-btn" onClick={props.onClose}>✕</button>
          </div>
        </div>
        <div className="modal-body">
          <div className="meta-grid" style={{ marginTop: 0 }}>
            {meta.map((m) => (
              <div key={m.label} className="meta-item">
                <div className="meta-label">{m.label}</div>
                <div className="meta-value">{m.value}</div>
              </div>
            ))}
          </div>
          {r.notes && (
            <div style={{ marginTop: 14 }}>
              <div className="meta-label">Notes</div>
              <div style={{ marginTop: 4 }}>{r.notes}</div>
            </div>
          )}
          {r.rejectionReason && <div className="form-error" style={{ marginTop: 14 }}>Rejected: {r.rejectionReason}</div>}

          <label style={{ fontSize: 13, fontWeight: 600, marginTop: 18, display: "block" }}>Materials</label>
          <div className="table-wrap" style={{ marginTop: 6 }}>
            <table>
              <thead><tr><th>Item</th><th>Qty</th><th>UOM</th><th>Note</th><th>Stock Available</th></tr></thead>
              <tbody>
                {r.lines.map((l) => {
                  const available = props.stockByItem[l.itemId] ?? 0;
                  const cls = available >= l.qty ? "green" : available > 0 ? "amber" : "red";
                  return (
                    <tr key={l.id}>
                      <td>
                        <div className="cell-title">{itemMap[l.itemId]?.name ?? l.itemId}</div>
                        <div className="cell-code">{itemMap[l.itemId]?.code ?? ""}</div>
                      </td>
                      <td>{l.qty}</td>
                      <td>{uomMap[l.uomId ?? ""]?.code ?? "—"}</td>
                      <td>{l.notes || "—"}</td>
                      <td><span className={`badge ${cls}`}>{available} available</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <label style={{ fontSize: 13, fontWeight: 600, marginTop: 18, display: "block" }}>Shipment history</label>
          {loadingShipments ? (
            <div className="skeleton" style={{ height: 40, marginTop: 6 }} />
          ) : shipments.length === 0 ? (
            <div className="empty" style={{ padding: 20 }}>No shipments arranged yet.</div>
          ) : (
            <div className="table-wrap" style={{ marginTop: 6 }}>
              <table>
                <thead><tr><th>Transfer #</th><th>From → To</th><th>Status</th><th>Materials shipped</th></tr></thead>
                <tbody>
                  {shipments.map((s) => (
                    <tr key={s.id}>
                      <td>{s.number}</td>
                      <td>{s.warehouseId ? (props.warehouses.find((w) => w.id === s.warehouseId)?.name ?? "—") : (s.sourceAddress || "—")} → {props.warehouses.find((w) => w.id === s.toWarehouseId)?.name ?? "—"}</td>
                      <td><span className="badge blue">{s.status}</span></td>
                      <td>{(s.lines ?? []).map((l) => `${itemMap[l.itemId]?.name ?? l.itemId} ×${l.qty}`).join(", ") || `${s.lineCount} line(s)`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Close</button>
          {r.status === "SUBMITTED" && props.canApprove && (
            <>
              <button className="btn danger" onClick={props.onReject}>Reject</button>
              <button className="btn primary" onClick={props.onArrange}>Approve & Arrange Shipment</button>
            </>
          )}
          {r.status === "APPROVED" && props.canArrange && (
            <button className="btn primary" onClick={props.onArrange}>Arrange Shipment</button>
          )}
        </div>
      </div>
    </div>
  );
}

function RejectModal(props: { requirement: MaterialRequirement; onClose: () => void; onRejected: () => void }) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!reason.trim()) { setError("A reason is required."); return; }
    setSaving(true);
    setError(null);
    try {
      await api.rejectMaterialRequirement(props.requirement.id, reason.trim());
      props.onRejected();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to reject the request.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>Reject {props.requirement.reqNumber}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field">
            <label>Reason *</label>
            <input value={reason} onChange={(e) => setReason(e.target.value)} required placeholder="Why is this being rejected?" />
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn danger" disabled={saving}>{saving ? <span className="spinner" /> : "Reject"}</button>
        </div>
      </form>
    </div>
  );
}

function ArrangeTransferModal(props: { requirement: MaterialRequirement; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Uom[]; stockByItemWarehouse: Record<string, number>; onClose: () => void; onArranged: () => void }) {
  const needsApproval = props.requirement.status === "SUBMITTED";
  const linkedDestination = props.warehouses.find((w) => w.id === props.requirement.warehouseId);
  const sourceCandidates = props.warehouses.filter((w) => w.id !== props.requirement.warehouseId);
  const defaultSourceWarehouse = sourceCandidates.find((w) => w.warehouseType === "MAIN_STORE") ?? sourceCandidates[0];
  const [fromWarehouseId, setFromWarehouseId] = useState(defaultSourceWarehouse?.id ?? "");
  const [sourceAddress, setSourceAddress] = useState(defaultSourceWarehouse ? (defaultSourceWarehouse.address ? `${defaultSourceWarehouse.name} — ${defaultSourceWarehouse.address}` : defaultSourceWarehouse.name) : "");
  const [toWarehouseId, setToWarehouseId] = useState(linkedDestination?.id ?? "");
  const [transportId, setTransportId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [lines, setLines] = useState<ArrangeLine[]>([]);
  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));
  const toCandidates = props.warehouses.filter((w) => w.id !== fromWarehouseId);

  // Sum qty already shipped in prior (partial) transfers against this requirement, so we can offer the remaining balance.
  useEffect(() => {
    let cancelled = false;
    setLoadingHistory(true);
    api.stockTransfers({ materialRequirementId: props.requirement.id, pageSize: 50 })
      .then((res) => {
        if (cancelled) return;
        const shippedByItem: Record<string, number> = {};
        for (const s of res.data) {
          for (const l of s.lines ?? []) shippedByItem[l.itemId] = (shippedByItem[l.itemId] ?? 0) + l.qty;
        }
        setLines(props.requirement.lines.map((l) => {
          const remaining = Math.max(0, l.qty - (shippedByItem[l.itemId] ?? 0));
          return { itemId: l.itemId, uomId: l.uomId, requestedQty: l.qty, remainingQty: remaining, shipQty: remaining > 0 ? String(remaining) : "0", include: remaining > 0 };
        }));
      })
      .catch(() => {
        if (!cancelled) setLines(props.requirement.lines.map((l) => ({ itemId: l.itemId, uomId: l.uomId, requestedQty: l.qty, remainingQty: l.qty, shipQty: String(l.qty), include: true })));
      })
      .finally(() => { if (!cancelled) setLoadingHistory(false); });
    return () => { cancelled = true; };
  }, [props.requirement.id, props.requirement.lines]);

  const setLineQty = (itemId: string, v: string) => setLines((ls) => ls.map((l) => (l.itemId === itemId ? { ...l, shipQty: v } : l)));
  const toggleLine = (itemId: string) => setLines((ls) => ls.map((l) => (l.itemId === itemId ? { ...l, include: !l.include } : l)));

  function linkSourceWarehouse(id: string) {
    setFromWarehouseId(id);
    const w = sourceCandidates.find((cw) => cw.id === id);
    if (w) setSourceAddress(w.address ? `${w.name} — ${w.address}` : w.name);
  }

  // Hard gate: block approval/arrangement if the linked warehouse doesn't have enough stock for a checked line.
  const shortfalls = fromWarehouseId
    ? lines.filter((l) => l.include && Number(l.shipQty) > (props.stockByItemWarehouse[`${l.itemId}|${fromWarehouseId}`] ?? 0))
    : [];

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!sourceAddress.trim()) { setError("Enter the source — a warehouse or a supplier address."); return; }
    if (!toWarehouseId) { setError("Choose a warehouse to ship to."); return; }
    if (!transportId.trim()) { setError("A transport ID is required for every shipment."); return; }
    const selected = lines.filter((l) => l.include && Number(l.shipQty) > 0);
    if (!selected.length) { setError("Select at least one material with a quantity to ship."); return; }
    if (shortfalls.length) { setError("Stock is not available for all selected materials at the chosen warehouse. Adjust quantities, pick another warehouse, or source from a supplier address."); return; }
    setSaving(true);
    try {
      if (needsApproval) await api.approveMaterialRequirement(props.requirement.id);
      const transfer = await api.createStockTransfer({
        fromWarehouseId: fromWarehouseId || undefined,
        sourceAddress: sourceAddress.trim(),
        toWarehouseId,
        projectId: props.requirement.projectId,
        materialRequirementId: props.requirement.id,
        transportId: transportId.trim(),
        lines: selected.map((l) => ({ itemId: l.itemId, qty: Number(l.shipQty), uomId: l.uomId }))
      });
      await api.approveStockTransfer(transfer.id);
      props.onArranged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to arrange the shipment.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ maxWidth: 760 }}>
        <div className="modal-head">
          <h3>{needsApproval ? "Approve & Arrange Shipment" : "Arrange Shipment"} · {props.requirement.reqNumber}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          {props.requirement.destinationAddress && (
            <div className="muted" style={{ fontSize: 13, marginBottom: 8 }}>Requested destination: {props.requirement.destinationAddress}</div>
          )}
          <div className="field">
            <label>Source (Warehouse or Supplier Address) *</label>
            <input value={sourceAddress} onChange={(e) => setSourceAddress(e.target.value)} required placeholder="Type the source address, or link a warehouse below to auto-fill" />
          </div>
          <div className="row2">
            <div className="field">
              <label>Link to Warehouse (optional, enables stock check)</label>
              <select value={fromWarehouseId} onChange={(e) => linkSourceWarehouse(e.target.value)}>
                <option value="">Not linked — external source (e.g. supplier)</option>
                {sourceCandidates.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label>Ship To (warehouse) *</label>
              <select value={toWarehouseId} onChange={(e) => setToWarehouseId(e.target.value)} required>
                <option value="">Select warehouse…</option>
                {toCandidates.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
          </div>
          {!fromWarehouseId && (
            <div className="muted" style={{ fontSize: 12, marginTop: -8, marginBottom: 14 }}>Sourcing from an external address — stock availability can't be verified automatically.</div>
          )}
          <div className="field">
            <label>Transport ID *</label>
            <input value={transportId} onChange={(e) => setTransportId(e.target.value)} required placeholder="Vehicle / LR / e-way bill no." />
          </div>
          <label style={{ fontSize: 13, fontWeight: 600 }}>
            Materials to ship <span className="muted" style={{ fontWeight: 400 }}>· uncheck or reduce qty for a partial shipment</span>
          </label>
          {loadingHistory ? (
            <div className="skeleton" style={{ height: 60, marginTop: 6 }} />
          ) : (
            <div className="table-wrap" style={{ marginTop: 6 }}>
              <table>
                <thead><tr><th></th><th>Item</th><th>Requested</th><th>Remaining</th><th>Ship Qty</th><th>Available at source</th></tr></thead>
                <tbody>
                  {lines.map((l) => {
                    const available = fromWarehouseId ? props.stockByItemWarehouse[`${l.itemId}|${fromWarehouseId}`] ?? 0 : null;
                    const shipQty = Number(l.shipQty) || 0;
                    const shortfall = available !== null && shipQty > available;
                    return (
                      <tr key={l.itemId}>
                        <td><input type="checkbox" checked={l.include} disabled={l.remainingQty <= 0} onChange={() => toggleLine(l.itemId)} /></td>
                        <td>
                          <div className="cell-title">{itemMap[l.itemId]?.name ?? l.itemId}</div>
                          <div className="cell-code">{itemMap[l.itemId]?.code ?? ""} · {uomMap[l.uomId ?? ""]?.code ?? ""}</div>
                        </td>
                        <td>{l.requestedQty}</td>
                        <td>{l.remainingQty}</td>
                        <td>
                          <input type="number" step="0.01" min={0} max={l.remainingQty} value={l.shipQty} disabled={!l.include}
                            onChange={(e) => setLineQty(l.itemId, e.target.value)} style={{ width: 90 }} />
                        </td>
                        <td>
                          {available === null ? "—" : <span className={`badge ${shortfall ? "red" : "green"}`}>{available}{shortfall ? " · short" : ""}</span>}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          {shortfalls.length > 0 && (
            <div className="form-error" style={{ marginTop: 10 }}>
              Not enough stock at the linked warehouse for: {shortfalls.map((l) => itemMap[l.itemId]?.name ?? l.itemId).join(", ")}. Reduce the quantity, pick another warehouse, or source from a supplier address to proceed.
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving || shortfalls.length > 0}>
            {saving ? <span className="spinner" /> : needsApproval ? "Approve & Arrange Shipment" : "Arrange & Approve Shipment"}
          </button>
        </div>
      </form>
    </div>
  );
}
