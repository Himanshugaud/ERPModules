import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type InventoryDocument, type Project, type Warehouse } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import AttachmentsPanel from "../components/AttachmentsPanel";
import { formatDate } from "../lib/ui";

export default function Shipment() {
  const { user } = useAuth();
  const canDispatch = (user?.permissions ?? []).includes("transfer.dispatch");
  const canReceive = (user?.permissions ?? []).includes("transfer.receive");
  const [transfers, setTransfers] = useState<InventoryDocument[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [dispatching, setDispatching] = useState<InventoryDocument | null>(null);
  const [docsFor, setDocsFor] = useState<InventoryDocument | null>(null);
  const [tab, setTab] = useState<"APPROVED" | "DISPATCHED" | "RECEIVED">("APPROVED");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const warehouseName = (id?: string) => (id ? warehouses.find((w) => w.id === id)?.name ?? "—" : "—");

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.stockTransfers({ pageSize: 100 });
      setTransfers(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load shipments.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
    api.projects({ pageSize: 200 }).then((r) => setProjects(r.data)).catch(() => setProjects([]));
    load();
  }, []);

  const filtered = transfers.filter((t) => t.status === tab);
  const projectName = (id?: string) => (id ? projects.find((p) => p.id === id)?.name ?? "—" : "—");

  async function receive(t: InventoryDocument) {
    setBusyId(t.id);
    setError(null);
    try {
      await api.receiveStockTransfer(t.id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to confirm receipt.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Transport &amp; Shipment</h1>
          <div className="page-sub">Shipments arranged by Supply Chain wait here for a vehicle. Enter the vehicle and driver details, dispatch to site, then confirm delivery.</div>
        </div>
        <div className="head-actions">
          <div className="tabs">
            <span className={`tab ${tab === "APPROVED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("APPROVED")}>Awaiting Transport</span>
            <span className={`tab ${tab === "DISPATCHED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("DISPATCHED")}>In Transit</span>
            <span className={`tab ${tab === "RECEIVED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("RECEIVED")}>Delivered</span>
          </div>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Transfer #</th><th>Project</th><th>From</th><th>To (Destination)</th><th>Vehicle &amp; driver</th><th>Lines</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => (
                <tr key={i}>{[...Array(8)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 120 : 90 }} /></td>)}</tr>
              ))
            ) : filtered.length === 0 ? (
              <tr><td colSpan={8}><div className="empty">Nothing here right now.</div></td></tr>
            ) : (
              filtered.map((t) => (
                <tr key={t.id}>
                  <td>{t.number}</td>
                  <td>{projectName(t.projectId)}</td>
                  <td>{t.warehouseId ? warehouseName(t.warehouseId) : (t.sourceAddress || "—")}</td>
                  <td>{warehouseName(t.toWarehouseId)}</td>
                  <td>
                    {t.vehicleNumber ? (
                      <>
                        <div className="cell-title">{t.vehicleNumber}{t.vehicleType ? ` · ${t.vehicleType}` : ""}</div>
                        <div className="cell-code">{t.driverName}{t.driverPhone ? ` · ${t.driverPhone}` : ""}{t.transporterName ? ` · ${t.transporterName}` : ""}</div>
                        {t.expectedDeliveryDate && <div className="cell-code">ETA {formatDate(t.expectedDeliveryDate)}</div>}
                      </>
                    ) : "—"}
                  </td>
                  <td>{t.lineCount} item{t.lineCount === 1 ? "" : "s"}</td>
                  <td><span className={`badge ${t.status === "APPROVED" ? "blue" : t.status === "DISPATCHED" ? "amber" : t.status === "RECEIVED" ? "green" : "gray"}`}>{t.status === "APPROVED" ? "AWAITING TRANSPORT" : t.status}</span></td>
                  <td>
                    <div className="row-actions">
                      {t.status === "APPROVED" && canDispatch && (
                        <button className="btn btn-sm primary" onClick={() => setDispatching(t)}>Arrange &amp; Dispatch</button>
                      )}
                      {t.status === "DISPATCHED" && canReceive && (
                        <button className="btn btn-sm primary" disabled={busyId === t.id} onClick={() => receive(t)}>Confirm Delivery</button>
                      )}
                      {t.projectId && <button className="btn btn-sm" onClick={() => setDocsFor(t)}>📎 Documents</button>}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {dispatching && (
        <DispatchModal
          transfer={dispatching}
          destination={warehouseName(dispatching.toWarehouseId)}
          onClose={() => setDispatching(null)}
          onDispatched={async () => { setDispatching(null); await load(); }}
        />
      )}

      {docsFor?.projectId && (
        <div className="modal-overlay" onClick={() => setDocsFor(null)}>
          <div className="modal" style={{ maxWidth: 640 }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <div><h3>Transport documents</h3><div className="muted" style={{ fontSize: 13 }}>{docsFor.number} · {projectName(docsFor.projectId)}</div></div>
              <button className="icon-btn" onClick={() => setDocsFor(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="muted" style={{ fontSize: 12, marginBottom: 8 }}>Upload the LR copy, e-way bill or delivery challan so they are stored with the project.</div>
              <AttachmentsPanel projectId={docsFor.projectId} defaultCategory="TRANSPORT" />
            </div>
            <div className="modal-foot"><button className="btn" onClick={() => setDocsFor(null)}>Close</button></div>
          </div>
        </div>
      )}
    </>
  );
}

function DispatchModal(props: { transfer: InventoryDocument; destination: string; onClose: () => void; onDispatched: () => void }) {
  const [form, setForm] = useState<Record<string, string>>({
    transporterName: "", vehicleNumber: "", vehicleType: "", driverName: "", driverPhone: "", expectedDeliveryDate: "", dispatchNotes: ""
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!form.vehicleNumber.trim() || !form.driverName.trim()) { setError("Vehicle number and driver name are required."); return; }
    setSaving(true);
    try {
      await api.dispatchStockTransfer(props.transfer.id, {
        transporterName: form.transporterName.trim() || undefined,
        vehicleNumber: form.vehicleNumber.trim(),
        vehicleType: form.vehicleType || undefined,
        driverName: form.driverName.trim(),
        driverPhone: form.driverPhone.trim() || undefined,
        expectedDeliveryDate: form.expectedDeliveryDate || undefined,
        dispatchNotes: form.dispatchNotes.trim() || undefined
      });
      props.onDispatched();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to dispatch the shipment.");
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" style={{ maxWidth: 560 }} onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <div><h3>Arrange transport &amp; dispatch</h3><div className="muted" style={{ fontSize: 13 }}>{props.transfer.number} → {props.destination}</div></div>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Vehicle number *</label><input value={form.vehicleNumber} onChange={(e) => set("vehicleNumber", e.target.value)} required maxLength={30} placeholder="e.g. MH12AB1234" /></div>
            <div className="field"><label>Vehicle type</label>
              <select value={form.vehicleType} onChange={(e) => set("vehicleType", e.target.value)}>
                <option value="">—</option>
                {["Mini truck", "Truck", "Trailer", "Tipper", "Container", "Tanker", "Other"].map((v) => <option key={v} value={v}>{v}</option>)}
              </select></div>
          </div>
          <div className="row2">
            <div className="field"><label>Driver name *</label><input value={form.driverName} onChange={(e) => set("driverName", e.target.value)} required maxLength={150} /></div>
            <div className="field"><label>Driver phone</label><input value={form.driverPhone} onChange={(e) => set("driverPhone", e.target.value)} maxLength={30} inputMode="tel" /></div>
          </div>
          <div className="row2">
            <div className="field"><label>Transporter / company</label><input value={form.transporterName} onChange={(e) => set("transporterName", e.target.value)} maxLength={150} /></div>
            <div className="field"><label>Expected delivery</label><input type="date" value={form.expectedDeliveryDate} onChange={(e) => set("expectedDeliveryDate", e.target.value)} /></div>
          </div>
          <div className="field"><label>Notes</label><textarea rows={2} value={form.dispatchNotes} onChange={(e) => set("dispatchNotes", e.target.value)} maxLength={500} placeholder="LR no., e-way bill no., loading instructions…" /></div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Dispatch to site"}</button>
        </div>
      </form>
    </div>
  );
}
