import { useEffect, useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { api, ApiError, type Project, type Lookup, type UserItem, type Client } from "../api/client";
import { statusBadge, formatDate, initials } from "../lib/ui";

const PAGE_SIZES = [8, 25, 50];
const SORT_OPTIONS = [
  { value: "", label: "Newest" },
  { value: "name", label: "Name A–Z" },
  { value: "-name", label: "Name Z–A" },
  { value: "status", label: "Status" },
  { value: "progress", label: "Progress ↑" },
  { value: "-progress", label: "Progress ↓" },
  { value: "duedate", label: "Due date ↑" },
  { value: "-duedate", label: "Due date ↓" },
  { value: "code", label: "Code A–Z" }
];

export default function Projects() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();

  const q = params.get("q") ?? "";
  const statusId = params.get("statusId") ?? "";
  const priorityId = params.get("priorityId") ?? "";
  const clientId = params.get("clientId") ?? "";
  const managerId = params.get("managerId") ?? "";
  const startFrom = params.get("startFrom") ?? "";
  const startTo = params.get("startTo") ?? "";
  const sort = params.get("sort") ?? "";
  const page = Math.max(1, Number(params.get("page") ?? "1") || 1);
  const pageSize = PAGE_SIZES.includes(Number(params.get("pageSize"))) ? Number(params.get("pageSize")) : PAGE_SIZES[0];

  const [projects, setProjects] = useState<Project[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [searchInput, setSearchInput] = useState(q);

  const [statuses, setStatuses] = useState<Lookup[]>([]);
  const [priorities, setPriorities] = useState<Lookup[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [users, setUsers] = useState<UserItem[]>([]);

  const statusMap = Object.fromEntries(statuses.map((s) => [s.id, s]));
  const priorityMap = Object.fromEntries(priorities.map((p) => [p.id, p]));
  const clientMap = Object.fromEntries(clients.map((c) => [c.id, c]));
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  // Update query-string filters. resetPage returns to page 1 unless page is set explicitly.
  function setFilters(next: Record<string, string | number | undefined>, resetPage = true) {
    const p = new URLSearchParams(params);
    Object.entries(next).forEach(([k, v]) => {
      if (v === undefined || v === "") p.delete(k);
      else p.set(k, String(v));
    });
    if (resetPage && !("page" in next)) p.set("page", "1");
    setParams(p);
  }

  async function loadRefData() {
    const [st, pr, cl] = await Promise.all([
      api.projectStatuses().catch(() => []),
      api.projectPriorities().catch(() => []),
      api.clients().catch(() => [])
    ]);
    setStatuses(st);
    setPriorities(pr);
    setClients(cl);
    // Users require elevated access; ignore failures.
    api.users().then((u) => setUsers(u)).catch(() => setUsers([]));
  }

  async function loadProjects() {
    setLoading(true);
    try {
      const res = await api.projects({
        page, pageSize, search: q, sort,
        statusId, priorityId, clientId, managerId,
        startDateFrom: startFrom, startDateTo: startTo
      });
      setProjects(res.data);
      setTotal(res.pagination.totalItems);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadRefData(); }, []);
  useEffect(() => { setSearchInput(q); }, [q]);
  useEffect(() => { loadProjects(); }, [q, statusId, priorityId, clientId, managerId, startFrom, startTo, sort, page, pageSize]);

  function submitSearch(e: FormEvent) {
    e.preventDefault();
    setFilters({ q: searchInput.trim() });
  }

  // Toggle a column's sort between ascending (key) and descending (-key).
  function toggleSort(key: string) {
    setFilters({ sort: sort === key ? `-${key}` : key });
  }
  function sortArrow(key: string) {
    return sort === key ? " ▲" : sort === `-${key}` ? " ▼" : "";
  }

  const activeChips: { key: string; label: string }[] = [];
  if (q) activeChips.push({ key: "q", label: `Search: “${q}”` });
  if (statusId) activeChips.push({ key: "statusId", label: `Status: ${statusMap[statusId]?.name ?? "—"}` });
  if (priorityId) activeChips.push({ key: "priorityId", label: `Priority: ${priorityMap[priorityId]?.name ?? "—"}` });
  if (clientId) activeChips.push({ key: "clientId", label: `Client: ${clientMap[clientId]?.name ?? "—"}` });
  if (managerId) activeChips.push({ key: "managerId", label: `Manager: ${userMap[managerId]?.displayName ?? userMap[managerId]?.email ?? "—"}` });
  if (startFrom) activeChips.push({ key: "startFrom", label: `Start ≥ ${formatDate(startFrom)}` });
  if (startTo) activeChips.push({ key: "startTo", label: `Start ≤ ${formatDate(startTo)}` });

  function clearAll() {
    const p = new URLSearchParams();
    if (pageSize !== PAGE_SIZES[0]) p.set("pageSize", String(pageSize));
    setParams(p);
    setSearchInput("");
  }

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

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
            <input placeholder="Search by name or code…" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} />
          </form>
          <button className="btn primary" onClick={() => setShowCreate(true)}>+ Create Project</button>
        </div>
      </div>

      <div className="filter-bar">
        <select value={statusId} onChange={(e) => setFilters({ statusId: e.target.value })}>
          <option value="">All Statuses</option>
          {statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        <select value={priorityId} onChange={(e) => setFilters({ priorityId: e.target.value })}>
          <option value="">All Priorities</option>
          {priorities.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        <select value={clientId} onChange={(e) => setFilters({ clientId: e.target.value })}>
          <option value="">All Clients</option>
          {clients.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select value={managerId} onChange={(e) => setFilters({ managerId: e.target.value })}>
          <option value="">All Managers</option>
          {users.map((u) => <option key={u.id} value={u.id}>{u.displayName ?? u.email}</option>)}
        </select>
        <label className="filter-date">Start ≥ <input type="date" value={startFrom} onChange={(e) => setFilters({ startFrom: e.target.value })} /></label>
        <label className="filter-date">Start ≤ <input type="date" value={startTo} onChange={(e) => setFilters({ startTo: e.target.value })} /></label>
        <select value={sort} onChange={(e) => setFilters({ sort: e.target.value })}>
          {SORT_OPTIONS.map((o) => <option key={o.value} value={o.value}>Sort: {o.label}</option>)}
        </select>
      </div>

      {activeChips.length > 0 && (
        <div className="chips-row">
          {activeChips.map((c) => (
            <span key={c.key} className="chip removable" onClick={() => setFilters({ [c.key]: "" })}>
              {c.label} <span className="chip-x">×</span>
            </span>
          ))}
          <button className="btn btn-sm" onClick={clearAll}>Clear all</button>
        </div>
      )}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th className="sortable" onClick={() => toggleSort("name")}>Project Name{sortArrow("name")}</th>
              <th>Client</th>
              <th>Manager</th>
              <th className="sortable" onClick={() => toggleSort("status")}>Status{sortArrow("status")}</th>
              <th className="sortable" onClick={() => toggleSort("progress")}>Progress{sortArrow("progress")}</th>
              <th className="sortable" onClick={() => toggleSort("duedate")}>Due Date{sortArrow("duedate")}</th>
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
              <tr><td colSpan={6}><div className="empty">No projects match your filters.{activeChips.length > 0 ? " Try clearing some filters." : ""}</div></td></tr>
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
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <span>Showing {from}–{to} of {total} projects</span>
            <label className="page-size">Rows:
              <select value={pageSize} onChange={(e) => setFilters({ pageSize: e.target.value })}>
                {PAGE_SIZES.map((n) => <option key={n} value={n}>{n}</option>)}
              </select>
            </label>
          </div>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <button className="btn" disabled={page <= 1} onClick={() => setFilters({ page: page - 1 }, false)}>‹</button>
            <span className="muted">Page {page} of {totalPages}</span>
            <button className="btn" disabled={page >= totalPages} onClick={() => setFilters({ page: page + 1 }, false)}>›</button>
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
            setFilters({ page: 1 }, false);
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
