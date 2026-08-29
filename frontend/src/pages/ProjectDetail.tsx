import { Fragment, useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { api, ApiError, type Project, type Task, type Lookup, type UserItem } from "../api/client";
import { statusBadge, formatDate, initials } from "../lib/ui";

type ModalMode = "create" | "edit" | "subtask";

export default function ProjectDetail() {
  const { projectId = "" } = useParams();
  const [project, setProject] = useState<Project | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [projectStatuses, setProjectStatuses] = useState<Lookup[]>([]);
  const [taskStatuses, setTaskStatuses] = useState<Lookup[]>([]);
  const [taskPriorities, setTaskPriorities] = useState<Lookup[]>([]);
  const [clients, setClients] = useState<Lookup[]>([]);
  const [users, setUsers] = useState<UserItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ mode: ModalMode; task?: Task; parentId?: string } | null>(null);

  async function reloadTasks() {
    const page = await api.projectTasks(projectId);
    setTasks(page.data);
  }

  useEffect(() => {
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [proj, taskPage, ps, ts, tp, cl] = await Promise.all([
          api.project(projectId),
          api.projectTasks(projectId),
          api.projectStatuses().catch(() => []),
          api.taskStatuses().catch(() => []),
          api.taskPriorities().catch(() => []),
          api.clients().catch(() => [])
        ]);
        setProject(proj);
        setTasks(taskPage.data);
        setProjectStatuses(ps);
        setTaskStatuses(ts);
        setTaskPriorities(tp);
        setClients(cl);
        api.users().then(setUsers).catch(() => setUsers([]));
      } catch (err) {
        setError(err instanceof ApiError ? err.message : "Unable to load project.");
      } finally {
        setLoading(false);
      }
    })();
  }, [projectId]);

  const psMap = Object.fromEntries(projectStatuses.map((s) => [s.id, s]));
  const tpMap = Object.fromEntries(taskPriorities.map((p) => [p.id, p]));
  const clientMap = Object.fromEntries(clients.map((c) => [c.id, c]));
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  async function onQuickStatus(task: Task, statusId: string) {
    if (!statusId || statusId === task.statusId) return;
    try {
      await api.changeTaskStatus(task.id, statusId);
      await reloadTasks();
    } catch (err) {
      alert(err instanceof ApiError ? err.message : "Unable to update status.");
    }
  }

  if (loading) return <div className="muted">Loading project…</div>;
  if (error || !project)
    return (
      <>
        <Link className="btn" to="/projects">← Projects</Link>
        <div className="card" style={{ marginTop: 16 }}><div className="empty">{error ?? "Project not found."}</div></div>
      </>
    );

  const pb = statusBadge(psMap[project.statusId ?? ""]?.code);
  const pct = Math.round(Number(project.completionPercentage));
  const parents = tasks.filter((t) => !t.parentTaskId);
  const childrenOf = (id: string) => tasks.filter((t) => t.parentTaskId === id);

  const meta: { label: string; value: string }[] = [
    { label: "Client", value: clientMap[project.clientId ?? ""]?.name ?? "—" },
    { label: "Manager", value: userMap[project.managerId ?? ""]?.displayName ?? userMap[project.managerId ?? ""]?.email ?? "—" },
    { label: "Start Date", value: formatDate(project.startDate) },
    { label: "Planned Completion", value: formatDate(project.plannedEndDate) },
    { label: "Budget", value: project.budget != null ? `${project.currencyCode ?? ""} ${Number(project.budget).toLocaleString()}`.trim() : "—" },
    { label: "Completion", value: `${pct}%` }
  ];

  function TaskRow({ t, child }: { t: Task; child?: boolean }) {
    const prio = tpMap[t.priorityId ?? ""]?.name ?? "—";
    const assignee = userMap[t.assigneeId ?? ""];
    const tp = Math.round(Number(t.completionPercentage));
    return (
      <tr>
        <td>
          <div className={child ? "subtask-title" : "cell-title"}>
            {child && <span className="subtask-mark">↳</span>}
            {t.title}
          </div>
        </td>
        <td>
          <select className="mini-select" value={t.statusId ?? ""} onChange={(e) => onQuickStatus(t, e.target.value)}>
            <option value="">— Set status —</option>
            {taskStatuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </td>
        <td>{prio}</td>
        <td>
          {assignee ? (
            <span className="person"><span className="avatar">{initials(assignee.displayName ?? assignee.email)}</span>{assignee.displayName ?? assignee.email}</span>
          ) : "—"}
        </td>
        <td>{tp}%</td>
        <td>{formatDate(t.dueDate)}</td>
        <td>
          <div className="row-actions">
            <button className="btn btn-sm" onClick={() => setModal({ mode: "edit", task: t })}>Edit</button>
            {!child && <button className="btn btn-sm" onClick={() => setModal({ mode: "subtask", parentId: t.id })}>+ Sub</button>}
          </div>
        </td>
      </tr>
    );
  }

  return (
    <>
      <Link className="btn-ghost btn" to="/projects" style={{ paddingLeft: 0 }}>← Projects</Link>

      <div className="card" style={{ marginTop: 12 }}>
        <div className="card-pad">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 16 }}>
            <div>
              <h1 className="page-title" style={{ fontSize: 22 }}>{project.name}</h1>
              <div className="cell-code" style={{ fontSize: 13 }}>{project.code}</div>
            </div>
            <span className={`badge ${pb.cls}`}><span className="dot" />{pb.label}</span>
          </div>

          <div className="progress" style={{ maxWidth: 320, marginTop: 14 }}>
            <span className="pct">Overall progress · {pct}%</span>
            <div className="bar"><span style={{ width: `${pct}%` }} /></div>
          </div>

          <div className="meta-grid">
            {meta.map((m) => (
              <div key={m.label} className="meta-item">
                <div className="meta-label">{m.label}</div>
                <div className="meta-value">{m.value}</div>
              </div>
            ))}
          </div>

          {project.description && (
            <div style={{ marginTop: 16 }}>
              <div className="meta-label">Description</div>
              <div style={{ marginTop: 4 }}>{project.description}</div>
            </div>
          )}
        </div>
      </div>

      <div className="card" style={{ marginTop: 18 }}>
        <div className="card-pad" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid var(--border)" }}>
          <div><strong>Tasks &amp; Subtasks</strong><span className="muted" style={{ marginLeft: 8 }}>({tasks.length})</span></div>
          <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ New Task</button>
        </div>
        {tasks.length === 0 ? (
          <div className="empty">No tasks yet. Create the first task for this project.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Task</th><th>Status</th><th>Priority</th><th>Assignee</th><th>Progress</th><th>Due Date</th><th></th>
              </tr>
            </thead>
            <tbody>
              {parents.map((t) => (
                <Fragment key={t.id}>
                  <TaskRow t={t} />
                  {childrenOf(t.id).map((c) => <TaskRow key={c.id} t={c} child />)}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal && (
        <TaskModal
          mode={modal.mode}
          task={modal.task}
          parentId={modal.parentId}
          projectId={projectId}
          statuses={taskStatuses}
          priorities={taskPriorities}
          users={users}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await reloadTasks(); }}
        />
      )}
    </>
  );
}

function TaskModal(props: {
  mode: ModalMode;
  task?: Task;
  parentId?: string;
  projectId: string;
  statuses: Lookup[];
  priorities: Lookup[];
  users: UserItem[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = props.mode === "edit";
  const t = props.task;
  const [form, setForm] = useState<Record<string, string>>({
    title: t?.title ?? "",
    description: t?.description ?? "",
    statusId: t?.statusId ?? "",
    priorityId: t?.priorityId ?? "",
    assigneeId: t?.assigneeId ?? "",
    startDate: t?.startDate?.slice(0, 10) ?? "",
    dueDate: t?.dueDate?.slice(0, 10) ?? "",
    estimatedHours: t?.estimatedHours != null ? String(t.estimatedHours) : "",
    completionPercentage: t?.completionPercentage != null ? String(t.completionPercentage) : ""
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const title = editing ? "Edit Task" : props.mode === "subtask" ? "New Subtask" : "New Task";

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      if (editing && t) {
        await api.updateTask(t.id, {
          title: form.title.trim(),
          description: form.description || undefined,
          statusId: form.statusId || undefined,
          priorityId: form.priorityId || undefined,
          startDate: form.startDate || undefined,
          dueDate: form.dueDate || undefined,
          estimatedHours: form.estimatedHours ? Number(form.estimatedHours) : undefined,
          completionPercentage: form.completionPercentage ? Number(form.completionPercentage) : undefined,
          rowVersion: t.rowVersion
        });
        if ((form.assigneeId || "") !== (t.assigneeId || "")) {
          await api.assignTask(t.id, form.assigneeId || null);
        }
      } else {
        const payload = {
          title: form.title.trim(),
          description: form.description || undefined,
          statusId: form.statusId || undefined,
          priorityId: form.priorityId || undefined,
          assigneeId: form.assigneeId || undefined,
          startDate: form.startDate || undefined,
          dueDate: form.dueDate || undefined,
          estimatedHours: form.estimatedHours ? Number(form.estimatedHours) : undefined
        };
        if (props.mode === "subtask" && props.parentId) await api.createSubtask(props.parentId, payload);
        else await api.createTask(props.projectId, payload);
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save task.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{title}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field">
            <label>Title *</label>
            <input value={form.title} onChange={(e) => set("title", e.target.value)} placeholder="Implement payment API" required />
          </div>
          <div className="field">
            <label>Description</label>
            <textarea rows={2} value={form.description} onChange={(e) => set("description", e.target.value)} />
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
          <div className="field">
            <label>Assignee</label>
            <select value={form.assigneeId} onChange={(e) => set("assigneeId", e.target.value)}>
              <option value="">— Unassigned —</option>
              {props.users.map((u) => <option key={u.id} value={u.id}>{u.displayName ?? u.email}</option>)}
            </select>
          </div>
          <div className="row2">
            <div className="field">
              <label>Start Date</label>
              <input type="date" value={form.startDate} onChange={(e) => set("startDate", e.target.value)} />
            </div>
            <div className="field">
              <label>Due Date</label>
              <input type="date" value={form.dueDate} onChange={(e) => set("dueDate", e.target.value)} />
            </div>
          </div>
          <div className="row2">
            <div className="field">
              <label>Estimated Hours</label>
              <input type="number" value={form.estimatedHours} onChange={(e) => set("estimatedHours", e.target.value)} />
            </div>
            {editing && (
              <div className="field">
                <label>Completion %</label>
                <input type="number" min={0} max={100} value={form.completionPercentage} onChange={(e) => set("completionPercentage", e.target.value)} />
              </div>
            )}
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Create Task"}</button>
        </div>
      </form>
    </div>
  );
}
