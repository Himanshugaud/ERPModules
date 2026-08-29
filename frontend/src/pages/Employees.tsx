import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type UserItem, type Lookup } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

const EMPLOYEE_MANAGER_ROLES = ["CEO", "HR Manager"];

export default function Employees() {
  const { user } = useAuth();
  const canManage = (user?.roles ?? []).some((r) => EMPLOYEE_MANAGER_ROLES.includes(r));
  const [users, setUsers] = useState<UserItem[]>([]);
  const [departments, setDepartments] = useState<Lookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [modal, setModal] = useState<{ mode: "create" | "edit"; user?: UserItem } | null>(null);
  const [profile, setProfile] = useState<UserItem | null>(null);

  const deptMap = Object.fromEntries(departments.map((d) => [d.id, d.name]));

  async function load() {
    setLoading(true);
    setError(null);
    try {
      setUsers(await api.users());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load employees.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    api.departments().then(setDepartments).catch(() => setDepartments([]));
  }, []);

  const filtered = users.filter((u) => {
    const q = search.trim().toLowerCase();
    if (!q) return true;
    return (u.displayName ?? "").toLowerCase().includes(q) || u.email.toLowerCase().includes(q) || (u.jobTitle ?? "").toLowerCase().includes(q);
  });

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Employees</h1>
          <div className="page-sub">Manage your organization's team members.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={(e) => e.preventDefault()}>
            <span>⌕</span>
            <input placeholder="Search employees…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          {canManage && <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Employee</button>}
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Name</th><th>Email</th><th>Job Title</th><th>Department</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(4)].map((_, i) => (
                <tr key={i}>{[...Array(6)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 160 : 90 }} /></td>)}</tr>
              ))
            ) : filtered.length === 0 ? (
              <tr><td colSpan={6}><div className="empty">No employees found.</div></td></tr>
            ) : (
              filtered.map((u) => (
                <tr key={u.id} className="clickable" onClick={() => setProfile(u)}>
                  <td>
                    <span className="person"><span className="avatar">{initials(u.displayName ?? u.email)}</span>{u.displayName ?? "—"}</span>
                  </td>
                  <td>{u.email}</td>
                  <td>{u.jobTitle ?? "—"}</td>
                  <td>{u.departmentId ? (deptMap[u.departmentId] ?? "—") : "—"}</td>
                  <td><span className={`badge ${u.status === "ACTIVE" || !u.status ? "green" : "gray"}`}>{u.status ?? "ACTIVE"}</span></td>
                  <td>
                    {canManage && <button className="btn btn-sm" onClick={(e) => { e.stopPropagation(); setModal({ mode: "edit", user: u }); }}>Edit</button>}
                  </td>
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
              <h3>Employee Profile</h3>
              <button className="icon-btn" onClick={() => setProfile(null)}>✕</button>
            </div>
            <div className="modal-body">
              <div style={{ display: "flex", gap: 14, alignItems: "center", marginBottom: 14 }}>
                <div className="avatar" style={{ width: 52, height: 52, fontSize: 20 }}>{initials(profile.displayName ?? profile.email)}</div>
                <div>
                  <div style={{ fontSize: 17, fontWeight: 600 }}>{profile.displayName ?? profile.email}</div>
                  <div className="muted">{profile.jobTitle ?? "—"}</div>
                </div>
              </div>
              <div className="meta-grid">
                <div className="meta-item"><div className="meta-label">Email</div><div className="meta-value">{profile.email}</div></div>
                <div className="meta-item"><div className="meta-label">Phone</div><div className="meta-value">{profile.phone ?? "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Department</div><div className="meta-value">{profile.departmentId ? (deptMap[profile.departmentId] ?? "—") : "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Status</div><div className="meta-value">{profile.status ?? "ACTIVE"}</div></div>
                <div className="meta-item"><div className="meta-label">First Name</div><div className="meta-value">{profile.firstName ?? "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Last Name</div><div className="meta-value">{profile.lastName ?? "—"}</div></div>
              </div>
            </div>
            <div className="modal-foot">
              <button className="btn" onClick={() => setProfile(null)}>Close</button>
              {canManage && <button className="btn primary" onClick={() => { setModal({ mode: "edit", user: profile }); setProfile(null); }}>Edit</button>}
            </div>
          </div>
        </div>
      )}

      {modal && (
        <EmployeeModal
          mode={modal.mode}
          user={modal.user}
          departments={departments}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await load(); }}
        />
      )}
    </>
  );
}

function EmployeeModal(props: { mode: "create" | "edit"; user?: UserItem; departments: Lookup[]; onClose: () => void; onSaved: () => void }) {
  const editing = props.mode === "edit";
  const u = props.user;
  const [form, setForm] = useState<Record<string, string>>({
    email: u?.email ?? "",
    firstName: u?.firstName ?? "",
    lastName: u?.lastName ?? "",
    displayName: u?.displayName ?? "",
    phone: u?.phone ?? "",
    jobTitle: u?.jobTitle ?? "",
    departmentId: u?.departmentId ?? "",
    status: u?.status ?? "ACTIVE"
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      if (editing && u) {
        await api.updateUser(u.id, {
          firstName: form.firstName || undefined,
          lastName: form.lastName || undefined,
          displayName: form.displayName || undefined,
          phone: form.phone || undefined,
          jobTitle: form.jobTitle || undefined,
          departmentId: form.departmentId || undefined,
          status: form.status || undefined
        });
      } else {
        await api.createUser({
          email: form.email.trim(),
          firstName: form.firstName || undefined,
          lastName: form.lastName || undefined,
          displayName: form.displayName || undefined,
          phone: form.phone || undefined,
          jobTitle: form.jobTitle || undefined,
          departmentId: form.departmentId || undefined
        });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save employee.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{editing ? "Edit Employee" : "Add Employee"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field">
            <label>Email *</label>
            <input type="email" value={form.email} onChange={(e) => set("email", e.target.value)} disabled={editing} required placeholder="person@company.com" />
          </div>
          <div className="row2">
            <div className="field">
              <label>First Name</label>
              <input value={form.firstName} onChange={(e) => set("firstName", e.target.value)} />
            </div>
            <div className="field">
              <label>Last Name</label>
              <input value={form.lastName} onChange={(e) => set("lastName", e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Display Name</label>
            <input value={form.displayName} onChange={(e) => set("displayName", e.target.value)} placeholder="Shown across the app" />
          </div>
          <div className="row2">
            <div className="field">
              <label>Phone</label>
              <input value={form.phone} onChange={(e) => set("phone", e.target.value)} />
            </div>
            <div className="field">
              <label>Job Title</label>
              <input value={form.jobTitle} onChange={(e) => set("jobTitle", e.target.value)} />
            </div>
          </div>
          <div className="field">
            <label>Department</label>
            <select value={form.departmentId} onChange={(e) => set("departmentId", e.target.value)}>
              <option value="">— None —</option>
              {props.departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>
          {editing && (
            <div className="field">
              <label>Status</label>
              <select value={form.status} onChange={(e) => set("status", e.target.value)}>
                <option value="ACTIVE">Active</option>
                <option value="INACTIVE">Inactive</option>
                <option value="SUSPENDED">Suspended</option>
              </select>
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Add Employee"}</button>
        </div>
      </form>
    </div>
  );
}
