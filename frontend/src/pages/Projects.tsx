import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError, type Project, type Lookup, type UserItem } from "../api/client";
import { statusBadge, formatDate, initials } from "../lib/ui";

const PAGE_SIZE = 8;

export default function Projects() {
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);

  const [statuses, setStatuses] = useState<Lookup[]>([]);
  const [priorities, setPriorities] = useState<Lookup[]>([]);
  const [clients, setClients] = useState<Lookup[]>([]);
  const [users, setUsers] = useState<UserItem[]>([]);

  const statusMap = Object.fromEntries(statuses.map((s) => [s.id, s]));
  const clientMap = Object.fromEntries(clients.map((c) => [c.id, c]));
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  async function loadRefData() {
    const [st, pr, cl] = await Promise.all([
      api.projectStatuses().catch(() => []),
      api.projectPriorities().catch(() => []),
      api.clients().catch(() => [])
    ]);
    setStatuses(st);
    setPriorities(pr);
    setClients(cl);
    // Users require admin role; ignore failures.
    api.users().then((u) => setUsers(u)).catch(() => setUsers([]));
  }

  async function loadProjects() {
    setLoading(true);
    try {
      const res = await api.projects({ page, pageSize: PAGE_SIZE, search: query });
      setProjects(res.data);
      setTotal(res.pagination.totalItems);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadRefData(); }, []);
  useEffect(() => { loadProjects(); }, [page, query]);

  function submitSearch(e: FormEvent) {
    e.preventDefault();
    setPage(1);
    setQuery(search.trim());
  }

  const from = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const to = Math.min(page * PAGE_SIZE, total);
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Project Portfolio</h1>
          <div className="page-sub">Manage and monitor all construction projects.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={submitSearch}>
            <span>⌕</span>
            <input placeholder="Search projects…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          <button className="btn primary" onClick={() => setShowCreate(true)}>+ Create Project</button>
        </div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Project Name</th>
              <th>Client</th>
              <th>Manager</th>
              <th>Status</th>
              <th>Progress</th>
              <th>Due Date</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(5)].map((_, i) => (
                <tr key={i}>
                  {[...Array(6)].map((__, j) => (
                    <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 180 : 90 }} /></td>
                  ))}
                </tr>
              ))
            ) : projects.length === 0 ? (
              <tr><td colSpan={6}><div className="empty">No projects found. Create your first project.</div></td></tr>
            ) : (
              projects.map((p) => {
                const b = statusBadge(statusMap[p.statusId ?? ""]?.code);
                const pct = Math.round(Number(p.completionPercentage));
                const manager = userMap[p.managerId ?? ""];
                const client = clientMap[p.clientId ?? ""];
                return (
                  <tr key={p.id} className="clickable" onClick={() => navigate(`/projects/${p.id}`)}>
                    <td>
                      <div className="cell-title">{p.name}</div>
                      <div className="cell-code">{p.code}</div>
                    </td>
                    <td>{client?.name ?? "—"}</td>
                    <td>
                      {manager ? (
                        <span className="person">
                          <span className="avatar">{initials(manager.displayName ?? manager.email)}</span>
                          {manager.displayName ?? manager.email}
                        </span>
                      ) : "—"}
                    </td>
                    <td><span className={`badge ${b.cls}`}><span className="dot" />{b.label}</span></td>
                    <td>
                      <div className="progress">
                        <span className="pct">{pct}%</span>
                        <div className={`bar ${pct < 40 && b.cls === "red" ? "red" : ""}`}><span style={{ width: `${pct}%` }} /></div>
                      </div>
                    </td>
                    <td>{formatDate(p.plannedEndDate)}</td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
        <div className="table-foot">
          <span>Showing {from} to {to} of {total} projects</span>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>‹</button>
            <button className="btn" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>›</button>
          </div>
        </div>
      </div>

      {showCreate && (
        <CreateProjectModal
          statuses={statuses}
          priorities={priorities}
          clients={clients}
          users={users}
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            setPage(1);
            loadProjects();
          }}
        />
      )}
    </>
  );
}

function CreateProjectModal(props: {
  statuses: Lookup[];
  priorities: Lookup[];
  clients: Lookup[];
  users: UserItem[];
  onClose: () => void;
  onCreated: () => void;
}) {
  const [form, setForm] = useState<Record<string, string>>({
    code: "", name: "", description: "", clientId: "", managerId: "",
    statusId: "", priorityId: "", startDate: "", plannedEndDate: "", budget: "", currencyCode: "INR"
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  function set(k: string, v: string) { setForm((f) => ({ ...f, [k]: v })); }

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      const payload: Record<string, unknown> = {
        code: form.code.trim(),
        name: form.name.trim(),
        description: form.description || undefined,
        clientId: form.clientId || undefined,
        managerId: form.managerId || undefined,
        statusId: form.statusId || undefined,
        priorityId: form.priorityId || undefined,
        startDate: form.startDate || undefined,
        plannedEndDate: form.plannedEndDate || undefined,
        budget: form.budget ? Number(form.budget) : undefined,
        currencyCode: form.currencyCode || undefined
      };
      await api.createProject(payload);
      props.onCreated();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to create project.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>Create Project</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field">
              <label>Project Code *</label>
              <input value={form.code} onChange={(e) => set("code", e.target.value)} placeholder="PRJ-001" required />
            </div>
            <div className="field">
              <label>Project Name *</label>
              <input value={form.name} onChange={(e) => set("name", e.target.value)} placeholder="Metro Tower" required />
            </div>
          </div>
          <div className="field">
            <label>Description</label>
            <textarea rows={2} value={form.description} onChange={(e) => set("description", e.target.value)} />
          </div>
          <div className="row2">
            <div className="field">
              <label>Client</label>
              <select value={form.clientId} onChange={(e) => set("clientId", e.target.value)}>
                <option value="">—</option>
                {props.clients.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label>Project Manager</label>
              <select value={form.managerId} onChange={(e) => set("managerId", e.target.value)}>
                <option value="">—</option>
                {props.users.map((u) => <option key={u.id} value={u.id}>{u.displayName ?? u.email}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Status</label>
              <select value={form.statusId} onChange={(e) => set("statusId", e.target.value)}>
                <option value="">—</option>
                {props.statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
            <div className="field">
              <label>Priority</label>
              <select value={form.priorityId} onChange={(e) => set("priorityId", e.target.value)}>
                <option value="">—</option>
                {props.priorities.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Start Date</label>
              <input type="date" value={form.startDate} onChange={(e) => set("startDate", e.target.value)} />
            </div>
            <div className="field">
              <label>Planned Completion</label>
              <input type="date" value={form.plannedEndDate} onChange={(e) => set("plannedEndDate", e.target.value)} />
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Budget</label>
              <input type="number" value={form.budget} onChange={(e) => set("budget", e.target.value)} placeholder="0" />
            </div>
            <div className="field">
              <label>Currency</label>
              <input value={form.currencyCode} onChange={(e) => set("currencyCode", e.target.value)} maxLength={3} />
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Create Project"}</button>
        </div>
      </form>
    </div>
  );
}
