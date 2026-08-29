import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Client } from "../api/client";
import { initials, formatDate } from "../lib/ui";

export default function Clients() {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [modal, setModal] = useState<{ mode: "create" | "edit"; client?: Client } | null>(null);
  const [profile, setProfile] = useState<Client | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setClients(await api.clients());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load clients.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  const filtered = clients.filter((c) => {
    const q = search.trim().toLowerCase();
    if (!q) return true;
    return c.name.toLowerCase().includes(q) || c.code.toLowerCase().includes(q) || (c.email ?? "").toLowerCase().includes(q);
  });

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Clients</h1>
          <div className="page-sub">Manage client accounts and contacts.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={(e) => e.preventDefault()}>
            <span>⌕</span>
            <input placeholder="Search clients…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Client</button>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Client</th><th>Code</th><th>Email</th><th>Phone</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(4)].map((_, i) => (
                <tr key={i}>{[...Array(6)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 160 : 90 }} /></td>)}</tr>
              ))
            ) : filtered.length === 0 ? (
              <tr><td colSpan={6}><div className="empty">No clients found.</div></td></tr>
            ) : (
              filtered.map((c) => (
                <tr key={c.id} className="clickable" onClick={() => setProfile(c)}>
                  <td><span className="person"><span className="avatar">{initials(c.name)}</span>{c.name}</span></td>
                  <td className="cell-code">{c.code}</td>
                  <td>{c.email ?? "—"}</td>
                  <td>{c.phone ?? "—"}</td>
                  <td><span className={`badge ${c.status === "ACTIVE" || !c.status ? "green" : "gray"}`}>{c.status ?? "ACTIVE"}</span></td>
                  <td><button className="btn btn-sm" onClick={(e) => { e.stopPropagation(); setModal({ mode: "edit", client: c }); }}>Edit</button></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {profile && (
        <div className="modal-overlay" onClick={() => setProfile(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <h3>Client Profile</h3>
              <button className="icon-btn" onClick={() => setProfile(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div style={{ display: "flex", gap: 14, alignItems: "center", marginBottom: 14 }}>
                <div className="avatar" style={{ width: 52, height: 52, fontSize: 20 }}>{initials(profile.name)}</div>
                <div>
                  <div style={{ fontSize: 17, fontWeight: 600 }}>{profile.name}</div>
                  <div className="muted">{profile.code}</div>
                </div>
              </div>
              <div className="meta-grid">
                <div className="meta-item"><div className="meta-label">Email</div><div className="meta-value">{profile.email ?? "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Phone</div><div className="meta-value">{profile.phone ?? "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Status</div><div className="meta-value">{profile.status ?? "ACTIVE"}</div></div>
                <div className="meta-item"><div className="meta-label">Since</div><div className="meta-value">{formatDate(profile.createdAt)}</div></div>
              </div>
            </div>
            <div className="modal-foot">
              <button className="btn" onClick={() => setProfile(null)}>Close</button>
              <button className="btn primary" onClick={() => { setModal({ mode: "edit", client: profile }); setProfile(null); }}>Edit</button>
            </div>
          </div>
        </div>
      )}

      {modal && (
        <ClientModal
          mode={modal.mode}
          client={modal.client}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await load(); }}
        />
      )}
    </>
  );
}

function ClientModal(props: { mode: "create" | "edit"; client?: Client; onClose: () => void; onSaved: () => void }) {
  const editing = props.mode === "edit";
  const c = props.client;
  const [form, setForm] = useState<Record<string, string>>({
    name: c?.name ?? "",
    code: c?.code ?? "",
    email: c?.email ?? "",
    phone: c?.phone ?? "",
    status: c?.status ?? "ACTIVE"
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      if (editing && c) {
        await api.updateClient(c.id, {
          name: form.name.trim(),
          email: form.email || undefined,
          phone: form.phone || undefined,
          status: form.status || undefined
        });
      } else {
        await api.createClient({
          name: form.name.trim(),
          code: form.code.trim(),
          email: form.email || undefined,
          phone: form.phone || undefined,
          status: form.status || undefined
        });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save client.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{editing ? "Edit Client" : "Add Client"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field">
              <label>Client Name *</label>
              <input value={form.name} onChange={(e) => set("name", e.target.value)} required placeholder="Skyline Developers" />
            </div>
            <div className="field">
              <label>Code *</label>
              <input value={form.code} onChange={(e) => set("code", e.target.value)} disabled={editing} required placeholder="SKY" />
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Email</label>
              <input type="email" value={form.email} onChange={(e) => set("email", e.target.value)} />
            </div>
            <div className="field">
              <label>Phone</label>
              <input value={form.phone} onChange={(e) => set("phone", e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Status</label>
            <select value={form.status} onChange={(e) => set("status", e.target.value)}>
              <option value="ACTIVE">Active</option>
              <option value="INACTIVE">Inactive</option>
            </select>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Add Client"}</button>
        </div>
      </form>
    </div>
  );
}
