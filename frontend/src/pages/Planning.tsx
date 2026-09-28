import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError, type Project, type Lookup, type Warehouse, type InventoryItem, type MaterialRequirement } from "../api/client";
import { formatDate } from "../lib/ui";
import { useAuth } from "../auth/AuthContext";

type LineRow = { itemId: string; qty: string; uomId: string; notes: string };

export default function Planning() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [statuses, setStatuses] = useState<Lookup[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [uoms, setUoms] = useState<Lookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [selected, setSelected] = useState<Project | null>(null);

  const planningStatusId = useMemo(() => statuses.find((s) => s.code === "PLANNING")?.id, [statuses]);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.projects({ statusId: showAll ? undefined : planningStatusId, pageSize: 100 });
      setProjects(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load projects.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.projectStatuses().then(setStatuses).catch(() => setStatuses([]));
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
    api.items({ pageSize: 200 }).then((r) => setItems(r.data)).catch(() => setItems([]));
    api.uoms().then(setUoms).catch(() => setUoms([]));
  }, []);

  // Re-run once statuses have loaded (so the default PLANNING filter is applied) and whenever the toggle changes.
  useEffect(() => { load(); }, [planningStatusId, showAll]);

  const statusMap = Object.fromEntries(statuses.map((s) => [s.id, s]));

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Planning</h1>
          <div className="page-sub">Finalized requirements land here. Build the materials estimate before it moves to Inventory.</div>
        </div>
        <div className="head-actions">
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13 }}>
            <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} />
            Show all projects
          </label>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Project</th><th>Status</th><th>Planned End</th><th>Budget</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => (
                <tr key={i}>{[...Array(5)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 200 : 90 }} /></td>)}</tr>
              ))
            ) : projects.length === 0 ? (
              <tr><td colSpan={5}><div className="empty">No projects in Planning. Convert a client requirement to get started.</div></td></tr>
            ) : (
              projects.map((p) => (
                <tr key={p.id} className="clickable" onClick={() => setSelected(p)}>
                  <td>
                    <div className="cell-title">{p.name}</div>
                    <div className="cell-code">{p.code}</div>
                  </td>
                  <td><span className="badge blue">{statusMap[p.statusId ?? ""]?.name ?? "—"}</span></td>
                  <td>{formatDate(p.plannedEndDate)}</td>
                  <td>{p.budget ? `${p.currencyCode ?? ""} ${p.budget.toLocaleString()}` : "—"}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn btn-sm primary" onClick={(e) => { e.stopPropagation(); setSelected(p); }}>Materials Estimate</button>
                      <button className="btn btn-sm" onClick={(e) => { e.stopPropagation(); navigate(`/projects/${p.id}`); }}>Open Project</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {selected && (
        <MaterialsEstimateModal
          project={selected}
          warehouses={warehouses}
          items={items}
          uoms={uoms}
          onClose={() => setSelected(null)}
        />
      )}
    </>
  );
}

function MaterialsEstimateModal(props: { project: Project; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Lookup[]; onClose: () => void }) {
  const { user } = useAuth();
  const [requirements, setRequirements] = useState<MaterialRequirement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);

  const itemMap = Object.fromEntries(props.items.map((it) => [it.id, it]));
  const uomMap = Object.fromEntries(props.uoms.map((u) => [u.id, u]));

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.materialRequirements({ projectId: props.project.id, pageSize: 50 });
      setRequirements(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load material requirements.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  function downloadCsv(r: MaterialRequirement) {
    const escape = (v: string) => `"${v.replace(/"/g, '""')}"`;
    const line = (cells: string[]) => cells.map(escape).join(",") + "\r\n";
    const destination = r.destinationAddress || props.warehouses.find((w) => w.id === r.warehouseId)?.name || "—";

    let csv = "";
    csv += line(["Organization", user?.organizationName ?? ""]);
    csv += line(["Project Name", props.project.name]);
    csv += line(["Project Code", props.project.code]);
    csv += line(["Requirement #", r.reqNumber]);
    csv += line(["Destination", destination]);
    csv += line(["Priority", r.priority]);
    csv += line(["Status", r.status]);
    csv += line(["Required Date", formatDate(r.requiredDate)]);
    csv += "\r\n";
    csv += line(["Item Code", "Item Name", "Qty", "UOM", "Notes"]);
    for (const l of r.lines) {
      csv += line([
        itemMap[l.itemId]?.code ?? "",
        itemMap[l.itemId]?.name ?? l.itemId,
        String(l.qty),
        uomMap[l.uomId ?? ""]?.code ?? "",
        l.notes ?? ""
      ]);
    }

    const safeName = props.project.name.replace(/[\\/:*?"<>|]+/g, "").trim();
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${safeName}_Material Estimate.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 720 }}>
        <div className="modal-head">
          <div>
            <h3>Materials Estimate</h3>
            <div className="muted" style={{ fontSize: 13 }}>{props.project.name} · {props.project.code}</div>
          </div>
          <button className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="head-actions" style={{ marginBottom: 10 }}>
            <button className="btn primary btn-sm" onClick={() => setAddOpen(true)}>+ Add Materials</button>
          </div>
          {loading ? (
            <div className="skeleton" style={{ height: 60 }} />
          ) : requirements.length === 0 ? (
            <div className="empty">No materials estimate yet. Add materials from the item master to send this to Inventory.</div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Req #</th><th>Destination</th><th>Priority</th><th>Materials</th><th>Status</th><th>Required</th></tr></thead>
                <tbody>
                  {requirements.map((r) => (
                    <tr key={r.id}>
                      <td>{r.reqNumber}</td>
                      <td>{r.destinationAddress || props.warehouses.find((w) => w.id === r.warehouseId)?.name || "—"}</td>
                      <td>{r.priority}</td>
                      <td>
                        <div className="muted" style={{ fontSize: 13 }}>{r.lines.length} material{r.lines.length === 1 ? "" : "s"}</div>
                        <button type="button" className="btn-link" onClick={() => downloadCsv(r)}>⭳ Download CSV</button>
                      </td>
                      <td><span className={`badge ${r.status === "SUBMITTED" ? "blue" : r.status === "APPROVED" ? "green" : r.status === "REJECTED" ? "red" : "gray"}`}>{r.status}</span></td>
                      <td>{formatDate(r.requiredDate)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button className="btn" onClick={props.onClose}>Close</button>
        </div>
      </div>

      {addOpen && (
        <AddMaterialsModal
          project={props.project}
          warehouses={props.warehouses}
          items={props.items}
          uoms={props.uoms}
          onClose={() => setAddOpen(false)}
          onSaved={async () => { setAddOpen(false); await load(); }}
        />
      )}
    </div>
  );
}

type Draft = { warehouseId: string; destinationAddress: string; priority: string; requiredDate: string; notes: string; lines: LineRow[] };
const EMPTY_LINE: LineRow = { itemId: "", qty: "", uomId: "", notes: "" };

function AddMaterialsModal(props: { project: Project; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Lookup[]; onClose: () => void; onSaved: () => void }) {
  const draftKey = `erp_planning_draft_${props.project.id}`;
  const siteWarehouses = props.warehouses.filter((w) => w.warehouseType === "SITE_STORE" && w.projectId === props.project.id);
  const candidateWarehouses = siteWarehouses.length > 0 ? siteWarehouses : props.warehouses;

  function readDraft(): Draft | null {
    try {
      const raw = localStorage.getItem(draftKey);
      return raw ? (JSON.parse(raw) as Draft) : null;
    } catch {
      return null;
    }
  }

  const [warehouseId, setWarehouseId] = useState(() => readDraft()?.warehouseId ?? "");
  const [destinationAddress, setDestinationAddress] = useState(() => readDraft()?.destinationAddress ?? siteWarehouses[0]?.name ?? "");
  const [priority, setPriority] = useState(() => readDraft()?.priority ?? "MEDIUM");
  const [requiredDate, setRequiredDate] = useState(() => readDraft()?.requiredDate ?? "");
  const [notes, setNotes] = useState(() => readDraft()?.notes ?? "");
  const [lines, setLines] = useState<LineRow[]>(() => {
    const saved = readDraft()?.lines;
    return saved && saved.length ? saved : [{ ...EMPTY_LINE }];
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [hasDraft] = useState(() => readDraft() !== null);

  // Auto-save so the draft survives closing the modal / navigating away and coming back.
  useEffect(() => {
    const draft: Draft = { warehouseId, destinationAddress, priority, requiredDate, notes, lines };
    try {
      localStorage.setItem(draftKey, JSON.stringify(draft));
    } catch {
      // ignore storage quota errors
    }
  }, [draftKey, warehouseId, destinationAddress, priority, requiredDate, notes, lines]);

  function linkWarehouse(id: string) {
    setWarehouseId(id);
    const w = candidateWarehouses.find((cw) => cw.id === id);
    if (w) setDestinationAddress(w.address ? `${w.name} — ${w.address}` : w.name);
  }

  const setLine = (i: number, k: keyof LineRow, v: string) =>
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, [k]: v } : l)));
  const addLine = () => setLines((ls) => [...ls, { ...EMPTY_LINE }]);
  const removeLine = (i: number) => setLines((ls) => ls.filter((_, idx) => idx !== i));

  const filledCount = lines.filter((l) => l.itemId && l.qty).length;

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    const valid = lines.filter((l) => l.itemId && l.qty);
    if (!valid.length) { setError("Add at least one material with a quantity."); return; }
    if (!destinationAddress.trim()) { setError("Enter the destination — a site address or a warehouse name."); return; }
    setSaving(true);
    try {
      await api.createMaterialRequirement({
        projectId: props.project.id,
        warehouseId: warehouseId || undefined,
        destinationAddress: destinationAddress.trim(),
        priority,
        requiredDate: requiredDate || undefined,
        notes: notes || undefined,
        lines: valid.map((l) => ({ itemId: l.itemId, qty: Number(l.qty), uomId: l.uomId || undefined, notes: l.notes || undefined }))
      });
      localStorage.removeItem(draftKey);
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to create the materials estimate.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ maxWidth: 780 }}>
        <div className="modal-head">
          <h3>Add Materials · {props.project.name}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          {hasDraft && !error && (
            <div className="draft-note"><span className="dot" />Resumed your unsaved draft — it's saved automatically as you type.</div>
          )}
          <div className="field">
            <label>Destination (Site Address) *</label>
            <input value={destinationAddress} onChange={(e) => setDestinationAddress(e.target.value)} required placeholder="Type the site address, or link a warehouse below to auto-fill" />
          </div>
          <div className="row2">
            <div className="field">
              <label>Link to Warehouse (optional)</label>
              <select value={warehouseId} onChange={(e) => linkWarehouse(e.target.value)}>
                <option value="">Not linked — address only</option>
                {candidateWarehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label>Priority</label>
              <select value={priority} onChange={(e) => setPriority(e.target.value)}>
                <option value="LOW">Low</option>
                <option value="MEDIUM">Medium</option>
                <option value="HIGH">High</option>
                <option value="URGENT">Urgent</option>
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Required Date</label>
              <input type="date" value={requiredDate} onChange={(e) => setRequiredDate(e.target.value)} />
            </div>
            <div className="field">
              <label>Notes</label>
              <input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional context for Inventory" />
            </div>
          </div>

          <label style={{ fontSize: 13, fontWeight: 600 }}>
            Materials (from item master) {filledCount > 0 && <span className="muted" style={{ fontWeight: 400 }}>· {filledCount} added</span>}
          </label>
          <div className="lines-editor">
            <div className="lines-editor-head">
              <span></span>
              <span>Item</span>
              <span>Qty</span>
              <span>UOM</span>
              <span>Note</span>
              <span></span>
            </div>
            <div className="lines-editor-body">
              {lines.map((l, i) => (
                <div key={i} className="lines-editor-row">
                  <span className="lines-editor-index">{i + 1}</span>
                  <select value={l.itemId} onChange={(e) => setLine(i, "itemId", e.target.value)}>
                    <option value="">Select item…</option>
                    {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
                  </select>
                  <input placeholder="Qty" type="number" step="0.01" value={l.qty} onChange={(e) => setLine(i, "qty", e.target.value)} />
                  <select value={l.uomId} onChange={(e) => setLine(i, "uomId", e.target.value)}>
                    <option value="">UOM</option>
                    {props.uoms.map((u) => <option key={u.id} value={u.id}>{u.code}</option>)}
                  </select>
                  <input placeholder="Optional" value={l.notes} onChange={(e) => setLine(i, "notes", e.target.value)} />
                  <button type="button" className="icon-btn" onClick={() => removeLine(i)} disabled={lines.length === 1} title="Remove line">✕</button>
                </div>
              ))}
            </div>
            <div className="lines-editor-foot">
              <span className="muted" style={{ fontSize: 12 }}>{lines.length} line{lines.length === 1 ? "" : "s"}</span>
              <button type="button" className="btn btn-sm btn-dashed" onClick={addLine}>+ Add another material</button>
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Submit to Inventory"}</button>
        </div>
      </form>
    </div>
  );
}
