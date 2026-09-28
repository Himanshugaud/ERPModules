import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Supplier } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

export default function InventorySuppliers() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes("supplier.create") ?? false;
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [modal, setModal] = useState<{ mode: "create" | "edit"; supplier?: Supplier } | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.suppliers({ pageSize: 100, search: search.trim() || undefined });
      setSuppliers(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load suppliers.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Suppliers</h1>
          <div className="page-sub">Vendors for raw materials and equipment.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={(e) => { e.preventDefault(); load(); }}>
            <span>⌕</span>
            <input placeholder="Search suppliers…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          {canManage && <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Supplier</button>}
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead><tr><th>Supplier</th><th>Code</th><th>Email</th><th>Phone</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {loading ? <tr><td colSpan={6}><div className="empty">Loading…</div></td></tr>
              : suppliers.length === 0 ? <tr><td colSpan={6}><div className="empty">No suppliers yet.</div></td></tr>
              : suppliers.map((s) => (
                <tr key={s.id}>
                  <td><span className="person"><span className="avatar">{initials(s.name)}</span>{s.name}</span></td>
                  <td className="cell-code">{s.code}</td>
                  <td>{s.email ?? "—"}</td>
                  <td>{s.phone ?? "—"}</td>
                  <td><span className={`badge ${s.status === "ACTIVE" ? "green" : "gray"}`}>{s.status}</span></td>
                  <td>{canManage && <button className="btn btn-sm" onClick={() => setModal({ mode: "edit", supplier: s })}>Edit</button>}</td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      {modal && <SupplierModal mode={modal.mode} supplier={modal.supplier} onClose={() => setModal(null)} onSaved={async () => { setModal(null); await load(); }} />}
    </>
  );
}

function SupplierModal(props: { mode: "create" | "edit"; supplier?: Supplier; onClose: () => void; onSaved: () => void }) {
  const editing = props.mode === "edit";
  const s = props.supplier;
  const [form, setForm] = useState({
    code: s?.code ?? "",
    name: s?.name ?? "",
    email: s?.email ?? "",
    phone: s?.phone ?? "",
    address: s?.address ?? "",
    status: s?.status ?? "ACTIVE"
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      if (editing && s) {
        await api.updateSupplier(s.id, { name: form.name.trim(), email: form.email || undefined, phone: form.phone || undefined, address: form.address || undefined, status: form.status });
      } else {
        await api.createSupplier({ code: form.code.trim(), name: form.name.trim(), email: form.email || undefined, phone: form.phone || undefined, address: form.address || undefined });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save supplier.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{editing ? "Edit Supplier" : "Add Supplier"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Name *</label><input value={form.name} onChange={(e) => set("name", e.target.value)} required placeholder="Acme Steel Traders" /></div>
            <div className="field"><label>Code *</label><input value={form.code} onChange={(e) => set("code", e.target.value)} disabled={editing} required placeholder="ACME" /></div>
          </div>
          <div className="row2">
            <div className="field"><label>Email</label><input type="email" value={form.email} onChange={(e) => set("email", e.target.value)} /></div>
            <div className="field"><label>Phone</label><input value={form.phone} onChange={(e) => set("phone", e.target.value)} /></div>
          </div>
          <div className="field"><label>Address</label><input value={form.address} onChange={(e) => set("address", e.target.value)} /></div>
          {editing && (
            <div className="field"><label>Status</label>
              <select value={form.status} onChange={(e) => set("status", e.target.value)}>
                <option value="ACTIVE">Active</option>
                <option value="INACTIVE">Inactive</option>
              </select>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Add Supplier"}</button>
        </div>
      </form>
    </div>
  );
}
