import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Warehouse } from "../api/client";
import { useAuth } from "../auth/AuthContext";

const WH_TYPES = [
  { v: "MAIN_STORE", l: "Main Store" },
  { v: "SITE_STORE", l: "Site Store" },
  { v: "PRODUCTION_STORE", l: "Production Store" },
  { v: "TRANSIT", l: "Transit" },
  { v: "SCRAP", l: "Scrap" }
];
const typeLabel = (v: string) => WH_TYPES.find((t) => t.v === v)?.l ?? v;

export default function InventoryWarehouses() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes("warehouse.create") ?? false;
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ mode: "create" | "edit"; wh?: Warehouse } | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setWarehouses(await api.warehouses());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load warehouses.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Warehouses</h1>
          <div className="page-sub">Plant stores, site stores &amp; production locations.</div>
        </div>
        {canManage && <div className="head-actions"><button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Warehouse</button></div>}
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead><tr><th>Code</th><th>Name</th><th>Type</th><th>Address</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {loading ? <tr><td colSpan={6}><div className="empty">Loading…</div></td></tr>
              : warehouses.length === 0 ? <tr><td colSpan={6}><div className="empty">No warehouses yet.</div></td></tr>
              : warehouses.map((w) => (
                <tr key={w.id}>
                  <td className="cell-code">{w.code}</td>
                  <td style={{ fontWeight: 600 }}>{w.name}</td>
                  <td><span className="chip">{typeLabel(w.warehouseType)}</span></td>
                  <td>{w.address ?? "—"}</td>
                  <td><span className={`badge ${w.isActive ? "green" : "gray"}`}>{w.isActive ? "Active" : "Inactive"}</span></td>
                  <td>{canManage && <button className="btn btn-sm" onClick={() => setModal({ mode: "edit", wh: w })}>Edit</button>}</td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      {modal && <WarehouseModal mode={modal.mode} wh={modal.wh} onClose={() => setModal(null)} onSaved={async () => { setModal(null); await load(); }} />}
    </>
  );
}

function WarehouseModal(props: { mode: "create" | "edit"; wh?: Warehouse; onClose: () => void; onSaved: () => void }) {
  const editing = props.mode === "edit";
  const w = props.wh;
  const [form, setForm] = useState({
    code: w?.code ?? "",
    name: w?.name ?? "",
    warehouseType: w?.warehouseType ?? "MAIN_STORE",
    address: w?.address ?? "",
    isActive: w?.isActive ?? true
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      if (editing && w) {
        await api.updateWarehouse(w.id, { name: form.name.trim(), warehouseType: form.warehouseType, address: form.address || undefined, isActive: form.isActive });
      } else {
        await api.createWarehouse({ code: form.code.trim(), name: form.name.trim(), warehouseType: form.warehouseType, address: form.address || undefined });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save warehouse.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{editing ? "Edit Warehouse" : "Add Warehouse"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Code *</label><input value={form.code} onChange={(e) => set("code", e.target.value)} disabled={editing} required placeholder="WH-MAIN" /></div>
            <div className="field"><label>Name *</label><input value={form.name} onChange={(e) => set("name", e.target.value)} required placeholder="Main Plant Store" /></div>
          </div>
          <div className="field"><label>Type</label>
            <select value={form.warehouseType} onChange={(e) => set("warehouseType", e.target.value)}>
              {WH_TYPES.map((t) => <option key={t.v} value={t.v}>{t.l}</option>)}
            </select>
          </div>
          <div className="field"><label>Address</label><input value={form.address} onChange={(e) => set("address", e.target.value)} /></div>
          {editing && <div className="chips-row"><label className="chip removable" onClick={() => set("isActive", !form.isActive)} style={{ background: form.isActive ? "var(--green-soft)" : undefined, color: form.isActive ? "var(--green)" : undefined }}>{form.isActive ? "✓ Active" : "Inactive"}</label></div>}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Add Warehouse"}</button>
        </div>
      </form>
    </div>
  );
}
