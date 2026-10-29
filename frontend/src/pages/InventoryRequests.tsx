import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import {
  api, ApiError,
  type MaterialRequirement, type Warehouse, type InventoryItem, type Project, type UserItem, type Uom, type StockLevel, type InventoryDocument, type PurchaseOrder, type WorkOrder
} from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { downloadRequirementXlsx } from "../lib/sheetExcel";
import { formatDate, formatDateTime } from "../lib/ui";

type ArrangeLine = { itemId: string; uomId?: string; requestedQty: number; remainingQty: number; shipQty: string; include: boolean };

export default function InventoryRequests() {
  const { user } = useAuth();
  const canApprove = (user?.permissions ?? []).includes("materialrequirement.approve");
  const canArrange = (user?.permissions ?? []).includes("transfer.create");
  const canConvert = (user?.permissions ?? []).includes("materialrequirement.convert");
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
  const [notice, setNotice] = useState<string | null>(null);
  const [arranging, setArranging] = useState<MaterialRequirement | null>(null);
  const [viewing, setViewing] = useState<MaterialRequirement | null>(null);
  const [creatingPo, setCreatingPo] = useState<MaterialRequirement | null>(null);

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
      const [res, stockResult] = await Promise.all([
        api.materialRequirements({ status: status || undefined, pageSize: 100 }),
        api.allStock()
      ]);
      setRequirements(res.data);
      setStock(stockResult);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load material requests.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
    api.allItems().then((r) => setItems(r.data)).catch(() => setItems([]));
    api.uoms().then(setUoms).catch(() => setUoms([]));
    api.projects({ pageSize: 200 }).then((r) => setProjects(r.data)).catch(() => setProjects([]));
    api.users().then(setUsers).catch(() => setUsers([]));
  }, []);
  useEffect(() => { load(); }, [status]);

  // Approval is part of the action now: a new request is approved as it is arranged or ordered.
  const canShip = (r: MaterialRequirement) => canArrange && (r.status !== "SUBMITTED" || canApprove);
  const canOrder = (r: MaterialRequirement) => canConvert && (r.status !== "SUBMITTED" || canApprove);
  const isOpen = (r: MaterialRequirement) => r.status === "SUBMITTED" || r.status === "APPROVED" || r.status === "CONVERTED";

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Material Requests</h1>
          <div className="page-sub">Plans published by Planning arrive here with the ship-to already set. Choose the source warehouse and mark the request ready to ship, or raise a purchase order for what is not in stock.</div>
        </div>
        <div className="head-actions">
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="SUBMITTED">New requests</option>
            <option value="APPROVED">In progress (ship / PO)</option>
            <option value="CONVERTED">PO raised (ship when received)</option>
            <option value="REJECTED">Rejected</option>
            <option value="">All</option>
          </select>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}
      {notice && <div className="draft-note" style={{ marginBottom: 12 }}><span className="dot" />{notice}</div>}

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
                      {isOpen(r) && r.status !== "CONVERTED" && canOrder(r) && (
                        <button className="btn btn-sm" onClick={(e) => { e.stopPropagation(); setCreatingPo(r); }}>Create PO</button>
                      )}
                      {isOpen(r) && canShip(r) && (
                        <button className="btn btn-sm primary" onClick={(e) => { e.stopPropagation(); setArranging(r); }}>Ready to Ship</button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {arranging && (
        <ArrangeTransferModal
          requirement={arranging}
          warehouses={warehouses}
          items={items}
          uoms={uoms}
          stockByItemWarehouse={stockByItemWarehouse}
          onClose={() => setArranging(null)}
          onArranged={async (number) => { setArranging(null); setNotice(`Ready to ship: ${number} has been sent to the Shipment team.`); await load(); }}
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
          canShip={isOpen(viewing) && canShip(viewing)}
          canOrder={isOpen(viewing) && viewing.status !== "CONVERTED" && canOrder(viewing)}
          onClose={() => setViewing(null)}
          onArrange={() => { setArranging(viewing); setViewing(null); }}
          onCreatePo={() => { setCreatingPo(viewing); setViewing(null); }}
        />
      )}

      {creatingPo && (
        <CreatePoModal
          requirement={creatingPo}
          warehouses={warehouses}
          items={items}
          uoms={uoms}
          stockByItem={stockByItem}
          stockByItemWarehouse={stockByItemWarehouse}
          onClose={() => setCreatingPo(null)}
          onCreated={async (poNumber) => { setCreatingPo(null); setNotice(`Draft purchase order ${poNumber} created.`); await load(); }}
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
  canShip: boolean;
  canOrder: boolean;
  onClose: () => void;
  onArrange: () => void;
  onCreatePo: () => void;
}) {
  const r = props.requirement;
  const { user } = useAuth();
  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));
  const userMap = Object.fromEntries(props.users.map((u) => [u.id, u]));
  const [shipments, setShipments] = useState<InventoryDocument[]>([]);
  const [loadingShipments, setLoadingShipments] = useState(true);
  const [po, setPo] = useState<PurchaseOrder | null>(null);
  const [fabOrders, setFabOrders] = useState<WorkOrder[]>([]);
  const [sending, setSending] = useState(false);
  const [fabError, setFabError] = useState<string | null>(null);
  const canRelease = (user?.permissions ?? []).includes("workorder.release");
  const loadFab = () => api.workOrders({ materialRequirementId: r.id, pageSize: 50 }).then((res) => setFabOrders(res.data)).catch(() => setFabOrders([]));

  async function sendToFabrication() {
    setSending(true);
    setFabError(null);
    try {
      for (const w of fabOrders.filter((x) => x.status === "DRAFT")) await api.releaseWorkOrder(w.id);
      await loadFab();
    } catch (err) {
      setFabError(err instanceof ApiError ? err.message : "Unable to send to the fabrication team.");
    } finally {
      setSending(false);
    }
  }

  useEffect(() => { loadFab(); }, [r.id]);
  useEffect(() => {
    if (!r.purchaseOrderId) { setPo(null); return; }
    api.purchaseOrder(r.purchaseOrderId).then(setPo).catch(() => setPo(null));
  }, [r.purchaseOrderId]);

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
    { label: "Created", value: formatDateTime(r.createdAt) },
    ...(r.blockName ? [{ label: "Block", value: `${r.blockName}${r.blockSize ? ` (${r.blockSize})` : ""}` }] : []),
    ...(r.purchaseOrderId ? [{ label: "Purchase Order", value: po ? `${po.poNumber} · ${po.status.replace(/_/g, " ")}` : "…" }] : [])
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
              <thead><tr><th>Item</th>{r.sheetType && <th>Section</th>}<th>Qty</th><th>UOM</th><th>Note</th><th>Stock Available</th></tr></thead>
              <tbody>
                {r.lines.map((l) => {
                  const available = props.stockByItem[l.itemId] ?? 0;
                  const cls = available >= l.qty ? "green" : available > 0 ? "amber" : "red";
                  return (
                    <tr key={l.id}>
                      <td>
                        <div className="cell-title">{itemMap[l.itemId]?.name ?? l.itemId}{itemMap[l.itemId]?.isManufactured && <span className="badge blue" style={{ marginLeft: 6, padding: "1px 7px", fontSize: 11 }}>Fabricated</span>}</div>
                        <div className="cell-code">{itemMap[l.itemId]?.code ?? ""}</div>
                      </td>
                      {r.sheetType && <td>{l.sectionName ?? "—"}</td>}
                      <td>{l.qty}</td>
                      <td>{uomMap[l.uomId ?? ""]?.code ?? "—"}</td>
                      <td>{l.notes || "—"}</td>
                      <td><span className={`badge ${cls}`}>{available} in stock{available < l.qty ? ` · short ${l.qty - available}` : ""}</span></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {fabOrders.length > 0 && (
            <>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 18 }}>
                <label style={{ fontSize: 13, fontWeight: 600 }}>Fabrication list (factory-made items)</label>
                {fabOrders.some((w) => w.status === "DRAFT") && canRelease && (
                  <button type="button" className="btn btn-sm primary" disabled={sending} onClick={sendToFabrication}>{sending ? <span className="spinner" /> : "Send to fabrication team"}</button>
                )}
              </div>
              {fabError && <div className="form-error" style={{ marginTop: 6 }}>{fabError}</div>}
              <div className="table-wrap" style={{ marginTop: 6 }}>
                <table>
                  <thead><tr><th>WO #</th><th>Item</th><th>Qty to make</th><th>Status</th></tr></thead>
                  <tbody>
                    {fabOrders.map((w) => (
                      <tr key={w.id}>
                        <td className="cell-code">{w.woNumber}</td>
                        <td>{itemMap[w.outputItemId]?.name ?? w.outputItemId}</td>
                        <td>{w.plannedQty}</td>
                        <td><span className={`badge ${w.status === "COMPLETED" ? "green" : w.status === "RELEASED" ? "blue" : "gray"}`}>{w.status === "DRAFT" ? "Not sent yet" : w.status === "RELEASED" ? "With fabrication team" : w.status === "COMPLETED" ? "Fabricated" : w.status}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

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
          <button type="button" className="btn" onClick={() => downloadRequirementXlsx(r, {
            orgName: user?.organizationName ?? "",
            projectName: props.project?.name ?? "",
            projectCode: props.project?.code ?? "",
            items: props.items,
            uoms: props.uoms,
            sheetTypeName: r.sheetType
          })}>⭳ Excel</button>
          {props.canOrder && <button className="btn" onClick={props.onCreatePo}>Create Purchase Order</button>}
          {props.canShip && <button className="btn primary" onClick={props.onArrange}>Ready to Ship</button>}
        </div>
      </div>
    </div>
  );
}

function buildShortageRows(requirement: MaterialRequirement, items: InventoryItem[], stockByItem: Record<string, number>) {
  const remaining = { ...stockByItem };
  return requirement.lines.filter(line => !line.purchaseOrderLineId && !items.find(item => item.id === line.itemId)?.isManufactured).map(line => {
    const available = Math.max(0, remaining[line.itemId] ?? 0);
    const shortage = Math.max(0, line.qty - available);
    remaining[line.itemId] = Math.max(0, available - line.qty);
    return { lineId: line.id, itemId: line.itemId, requested: line.qty, shortage, include: shortage > 0, qty: String(shortage) };
  });
}

function CreatePoModal(props: { requirement: MaterialRequirement; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Uom[]; stockByItem: Record<string, number>; stockByItemWarehouse: Record<string, number>; onClose: () => void; onCreated: (poNumber: string) => void }) {
  // Purchased goods arrive at a store first; the shipment to site is arranged afterwards.
  const defaultWarehouse = props.warehouses.find((w) => w.warehouseType === "MAIN_STORE") ?? props.warehouses[0];
  const [warehouseId, setWarehouseId] = useState(defaultWarehouse?.id ?? "");
  const [expectedDate, setExpectedDate] = useState(props.requirement.requiredDate ?? "");
  const [notes, setNotes] = useState("");
  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));
  const warehouseStock = Object.fromEntries(props.requirement.lines.map(line => [line.itemId, props.stockByItemWarehouse[`${line.itemId}|${warehouseId}`] ?? 0]));
  const [rows, setRows] = useState(() => buildShortageRows(props.requirement, props.items, warehouseStock));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => { setRows(buildShortageRows(props.requirement, props.items, warehouseStock)); }, [warehouseId]);

  const setRow = (id: string, patch: Partial<(typeof rows)[number]>) => setRows((rs) => rs.map((r) => (r.lineId === id ? { ...r, ...patch } : r)));
  const selected = rows.filter((r) => r.include);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!warehouseId) { setError("Choose a receiving warehouse."); return; }
    if (!selected.length) { setError("Select at least one material to order."); return; }
    if (selected.some((r) => !(Number(r.qty) > 0) || Number(r.qty) > r.shortage)) { setError("Enter a positive quantity no greater than the missing quantity for each material."); return; }
    setSaving(true);
    try {
      if (props.requirement.status === "SUBMITTED") await api.approveMaterialRequirement(props.requirement.id);
      const po = await api.convertMaterialRequirementToPo(props.requirement.id, {
        warehouseId: warehouseId || undefined,
        expectedDate: expectedDate || undefined,
        notes: notes.trim() || undefined,
        lines: selected.map((r) => ({ materialRequirementLineId: r.lineId, qty: Number(r.qty) }))
      });
      props.onCreated(po.poNumber);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to create the purchase order.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal procurement-modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ width: "min(820px, calc(100vw - 40px))" }}>
        <div className="modal-head">
          <h3>Draft Purchase Order · {props.requirement.reqNumber}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field">
              <label>Receiving warehouse *</label>
              <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                <option value="">—</option>
                {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field"><label>Expected delivery</label><input type="date" value={expectedDate} onChange={(e) => setExpectedDate(e.target.value)} /></div>
            <div className="field"><label>Notes</label><input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} /></div>
          </div>
          <label style={{ fontSize: 13, fontWeight: 600 }}>Materials to order <span className="muted" style={{ fontWeight: 400 }}>· quantities default to what is missing from stock</span></label>
          {rows.length === 0 ? (
            <div className="empty" style={{ padding: 20 }}>Every line already has a purchase order.</div>
          ) : (
            <div className="table-wrap" style={{ marginTop: 6 }}>
              <table>
                <thead><tr><th></th><th>Item</th><th>Requested</th><th>At warehouse</th><th>Missing</th><th>Draft qty</th></tr></thead>
                <tbody>
                  {rows.map((r) => {
                    const inStock = warehouseStock[r.itemId] ?? 0;
                    return (
                      <tr key={r.lineId}>
                        <td><input type="checkbox" checked={r.include} disabled={r.shortage <= 0} onChange={() => setRow(r.lineId, { include: !r.include })} /></td>
                        <td>
                          <div className="cell-title">{itemMap[r.itemId]?.name ?? r.itemId}</div>
                          <div className="cell-code">{itemMap[r.itemId]?.code ?? ""}{uomMap[props.requirement.lines.find((l) => l.id === r.lineId)?.uomId ?? ""]?.code ? ` · ${uomMap[props.requirement.lines.find((l) => l.id === r.lineId)?.uomId ?? ""].code}` : ""}</div>
                        </td>
                        <td>{r.requested}</td>
                        <td><span className={`badge ${inStock >= r.requested ? "green" : inStock > 0 ? "amber" : "red"}`}>{inStock}</span></td>
                        <td>{r.shortage}</td>
                        <td><input type="number" min={0} max={r.shortage} step="0.01" value={r.qty} disabled={!r.include} onChange={(e) => setRow(r.lineId, { qty: e.target.value })} style={{ width: 90 }} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving || selected.length === 0}>{saving ? <span className="spinner" /> : "Create draft PO"}</button>
        </div>
      </form>
    </div>
  );
}

function ArrangeTransferModal(props: { requirement: MaterialRequirement; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Uom[]; stockByItemWarehouse: Record<string, number>; onClose: () => void; onArranged: (transferNumber: string) => void }) {
  const needsApproval = props.requirement.status === "SUBMITTED";
  const linkedDestination = props.warehouses.find((w) => w.id === props.requirement.warehouseId);
  const destinationLabel = props.requirement.destinationAddress || (linkedDestination ? (linkedDestination.address ? `${linkedDestination.name} — ${linkedDestination.address}` : linkedDestination.name) : "Project site store");
  const sourceCandidates = props.warehouses.filter((w) => w.id !== props.requirement.warehouseId && !(w.warehouseType === "SITE_STORE" && w.projectId === props.requirement.projectId));
  const defaultSourceWarehouse = sourceCandidates.find((w) => w.warehouseType === "MAIN_STORE") ?? sourceCandidates[0];
  const [fromWarehouseId, setFromWarehouseId] = useState(defaultSourceWarehouse?.id ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [lines, setLines] = useState<ArrangeLine[]>([]);
  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));

  // Sum qty already shipped in prior (partial) transfers against this requirement, so we can offer the remaining balance.
  useEffect(() => {
    let cancelled = false;
    setLoadingHistory(true);
    api.allStockTransfers({ materialRequirementId: props.requirement.id })
      .then((res) => {
        if (cancelled) return;
        const shippedByItem: Record<string, number> = {};
        for (const s of res.filter(transfer => transfer.status !== "CANCELLED" && transfer.status !== "REJECTED")) {
          for (const l of s.lines ?? []) shippedByItem[l.itemId] = (shippedByItem[l.itemId] ?? 0) + l.qty;
        }
        const grouped = new Map<string, { itemId: string; uomId?: string; qty: number }>();
        for (const line of props.requirement.lines) {
          const previous = grouped.get(line.itemId);
          grouped.set(line.itemId, { itemId: line.itemId, uomId: line.uomId, qty: (previous?.qty ?? 0) + line.qty });
        }
        setLines([...grouped.values()].map((l) => {
          const remaining = Math.max(0, l.qty - (shippedByItem[l.itemId] ?? 0));
          return { itemId: l.itemId, uomId: l.uomId, requestedQty: l.qty, remainingQty: remaining, shipQty: remaining > 0 ? String(remaining) : "0", include: remaining > 0 };
        }));
      })
      .catch(() => {
        if (!cancelled) setError("Unable to load previous shipments. Close and reopen this request to retry.");
      })
      .finally(() => { if (!cancelled) setLoadingHistory(false); });
    return () => { cancelled = true; };
  }, [props.requirement.id, props.requirement.lines]);

  const setLineQty = (itemId: string, v: string) => setLines((ls) => ls.map((l) => (l.itemId === itemId ? { ...l, shipQty: v } : l)));
  const toggleLine = (itemId: string) => setLines((ls) => ls.map((l) => (l.itemId === itemId ? { ...l, include: !l.include } : l)));

  // Ship what the source holds now; the rest is left on the request for a purchase order.
  function shipWhatIsInStock() {
    setLines((ls) => ls.map((l) => {
      const qty = Math.min(l.remainingQty, props.stockByItemWarehouse[`${l.itemId}|${fromWarehouseId}`] ?? 0);
      return { ...l, shipQty: String(qty), include: qty > 0 };
    }));
  }

  function linkSourceWarehouse(id: string) {
    setFromWarehouseId(id);
  }

  // Hard gate: block approval/arrangement if the linked warehouse doesn't have enough stock for a checked line.
  const shortfalls = fromWarehouseId
    ? lines.filter((l) => l.include && Number(l.shipQty) > (props.stockByItemWarehouse[`${l.itemId}|${fromWarehouseId}`] ?? 0))
    : [];

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!fromWarehouseId) { setError("Select a source warehouse."); return; }
    const selected = lines.filter((l) => l.include && Number(l.shipQty) > 0);
    if (!selected.length) { setError("Select at least one material with a quantity to ship."); return; }
    if (selected.some(line => Number(line.shipQty) > line.remainingQty)) { setError("Shipment quantity cannot exceed the remaining request quantity."); return; }
    if (shortfalls.length) { setError("Stock is not available for all selected materials at the chosen warehouse."); return; }
    setSaving(true);
    try {
      if (needsApproval) await api.approveMaterialRequirement(props.requirement.id);
      const transfer = await api.createStockTransfer({
        fromWarehouseId: fromWarehouseId || undefined,
        sourceAddress: sourceCandidates.find(warehouse => warehouse.id === fromWarehouseId)?.address,
        toWarehouseId: props.requirement.warehouseId || undefined,
        projectId: props.requirement.projectId,
        materialRequirementId: props.requirement.id,
        lines: selected.map((l) => ({ itemId: l.itemId, qty: Number(l.shipQty), uomId: l.uomId }))
      });
      await api.approveStockTransfer(transfer.id);
      props.onArranged(transfer.number);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to arrange the shipment.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal procurement-modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ width: "min(760px, calc(100vw - 40px))" }}>
        <div className="modal-head">
          <h3>Ready to Ship · {props.requirement.reqNumber}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field">
            <label>Ship To (from the planning request)</label>
            <input value={destinationLabel} readOnly disabled />
          </div>
          <div className="field">
            <label>Source warehouse *</label>
            <select value={fromWarehouseId} required onChange={(e) => linkSourceWarehouse(e.target.value)}>
              <option value="">Select warehouse</option>
              {sourceCandidates.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
            </select>
          </div>
          <div className="muted" style={{ fontSize: 12, marginBottom: 14 }}>
            Marking this ready to ship publishes it to the Shipment team, who assign the vehicle and driver and dispatch it to site.
          </div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8, justifyContent: "space-between", alignItems: "center" }}>
            <label style={{ fontSize: 13, fontWeight: 600 }}>
              Materials to ship <span className="muted" style={{ fontWeight: 400 }}>· uncheck or reduce qty for a partial shipment</span>
            </label>
            {fromWarehouseId && <button type="button" className="btn btn-sm" onClick={shipWhatIsInStock}>Ship only what's in stock</button>}
          </div>
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
              Not enough stock at the selected warehouse for: {shortfalls.map((l) => itemMap[l.itemId]?.name ?? l.itemId).join(", ")}. Reduce the quantity or pick another warehouse.
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving || loadingHistory || !fromWarehouseId || !lines.some(line => line.include && Number(line.shipQty) > 0) || shortfalls.length > 0}>
            {saving ? <span className="spinner" /> : "Ready to Ship"}
          </button>
        </div>
      </form>
    </div>
  );
}
