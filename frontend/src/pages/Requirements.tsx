import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError, type Lookup, type Client } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate } from "../lib/ui";

interface Requirement {
  id: string;
  title: string;
  clientId: string;
  clientName: string;
  description: string;
  priority: string; // LOW | MEDIUM | HIGH | CRITICAL
  budget: string;
  targetDate: string;
  status: "NEW" | "CONVERTED";
  projectId?: string;
  createdAt: string;
}

function storageKey(orgId?: string) {
  return `erp_requirements_${orgId ?? "default"}`;
}
function loadReqs(orgId?: string): Requirement[] {
  try {
    const raw = localStorage.getItem(storageKey(orgId));
    return raw ? (JSON.parse(raw) as Requirement[]) : [];
  } catch {
    return [];
  }
}
function saveReqs(orgId: string | undefined, reqs: Requirement[]) {
  localStorage.setItem(storageKey(orgId), JSON.stringify(reqs));
}

export default function Requirements() {
  const { user } = useAuth();
  const orgId = user?.organizationId;
  const navigate = useNavigate();
  const [reqs, setReqs] = useState<Requirement[]>(() => loadReqs(orgId));
  const [clients, setClients] = useState<Client[]>([]);
  const [statuses, setStatuses] = useState<Lookup[]>([]);
  const [priorities, setPriorities] = useState<Lookup[]>([]);
  const [wizard, setWizard] = useState(false);
  const [convertReq, setConvert] = useState<Requirement | null>(null);

  useEffect(() => {
    api.clients().then(setClients).catch(() => setClients([]));
    api.projectStatuses().then(setStatuses).catch(() => setStatuses([]));
    api.projectPriorities().then(setPriorities).catch(() => setPriorities([]));
  }, []);

  useEffect(() => { setReqs(loadReqs(orgId)); }, [orgId]);

  function persist(next: Requirement[]) {
    setReqs(next);
    saveReqs(orgId, next);
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Client Requirements</h1>
          <div className="page-sub">Capture incoming client requirements, then convert them into projects.</div>
        </div>
        <div className="head-actions">
          <button className="btn primary" onClick={() => setWizard(true)}>+ New Requirement</button>
        </div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Requirement</th><th>Client</th><th>Priority</th><th>Target Date</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {reqs.length === 0 ? (
              <tr><td colSpan={6}><div className="empty">No requirements yet. Capture your first client requirement.</div></td></tr>
            ) : (
              reqs.map((r) => (
                <tr key={r.id}>
                  <td>
                    <div className="cell-title">{r.title}</div>
                    <div className="cell-code" style={{ maxWidth: 320, whiteSpace: "normal" }}>{r.description}</div>
                  </td>
                  <td>{r.clientName || "—"}</td>
                  <td>{r.priority}</td>
                  <td>{formatDate(r.targetDate)}</td>
                  <td><span className={`badge ${r.status === "CONVERTED" ? "green" : "blue"}`}>{r.status === "CONVERTED" ? "Converted" : "New"}</span></td>
                  <td>
                    <div className="row-actions">
                      {r.status === "CONVERTED" && r.projectId ? (
                        <button className="btn btn-sm" onClick={() => navigate(`/projects/${r.projectId}`)}>Open Project</button>
                      ) : (
                        <button className="btn btn-sm primary" onClick={() => setConvert(r)}>Convert →</button>
                      )}
                      <button className="btn btn-sm danger" onClick={() => persist(reqs.filter((x) => x.id !== r.id))}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {wizard && (
        <RequirementWizard
          clients={clients}
          statuses={statuses}
          priorities={priorities}
          onClose={() => setWizard(false)}
          onCreated={(req, project) => {
            const next = [{ ...req, status: (project ? "CONVERTED" : "NEW") as Requirement["status"], projectId: project?.id }, ...reqs];
            persist(next);
            setWizard(false);
            if (project) navigate(`/projects/${project.id}`);
          }}
        />
      )}

      {convertReq && (
        <ConvertModal
          requirement={convertReq}
          clients={clients}
          statuses={statuses}
          priorities={priorities}
          onClose={() => setConvert(null)}
          onConverted={(projectId) => {
            persist(reqs.map((x) => x.id === convertReq.id ? { ...x, status: "CONVERTED", projectId } : x));
            setConvert(null);
            navigate(`/projects/${projectId}`);
          }}
        />
      )}
    </>
  );
}

function pickStatusId(statuses: Lookup[]): string | undefined {
  return statuses.find((s) => s.code === "PLANNING")?.id ?? statuses[0]?.id;
}
function pickPriorityId(priorities: Lookup[], code: string): string | undefined {
  return priorities.find((p) => p.code === code)?.id ?? priorities.find((p) => p.code === "MEDIUM")?.id;
}
function genCode() {
  return `PRJ-${Math.floor(1000 + Math.random() * 9000)}`;
}

function RequirementWizard(props: {
  clients: Client[];
  statuses: Lookup[];
  priorities: Lookup[];
  onClose: () => void;
  onCreated: (req: Requirement, project?: { id: string }) => void;
}) {
  const [step, setStep] = useState(1);
  const [form, setForm] = useState<Record<string, string>>({
    title: "", clientId: "", description: "", priority: "MEDIUM", budget: "", targetDate: ""
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const client = props.clients.find((c) => c.id === form.clientId);

  function next(e: FormEvent) {
    e.preventDefault();
    if (!form.title.trim()) { setError("Requirement title is required."); return; }
    setError(null);
    setStep(2);
  }

  function buildReq(): Requirement {
    return {
      id: crypto.randomUUID(),
      title: form.title.trim(),
      clientId: form.clientId,
      clientName: client?.name ?? "",
      description: form.description,
      priority: form.priority,
      budget: form.budget,
      targetDate: form.targetDate,
      status: "NEW",
      createdAt: new Date().toISOString()
    };
  }

  async function saveOnly() {
    props.onCreated(buildReq());
  }

  async function convertNow() {
    setSaving(true);
    setError(null);
    try {
      const project = await api.createProject({
        code: genCode(),
        name: form.title.trim(),
        description: form.description || undefined,
        clientId: form.clientId || undefined,
        statusId: pickStatusId(props.statuses),
        priorityId: pickPriorityId(props.priorities, form.priority),
        plannedEndDate: form.targetDate || undefined,
        budget: form.budget ? Number(form.budget) : undefined,
        currencyCode: "INR"
      });
      props.onCreated(buildReq(), project);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to convert to project.");
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <h3>New Requirement · Step {step} of 2</h3>
          <button className="icon-btn" onClick={props.onClose}>✕</button>
        </div>

        {step === 1 ? (
          <form onSubmit={next}>
            <div className="modal-body">
              {error && <div className="form-error">{error}</div>}
              <div className="field">
                <label>Requirement Title *</label>
                <input value={form.title} onChange={(e) => set("title", e.target.value)} placeholder="Build a 3-tower residential complex" required />
              </div>
              <div className="field">
                <label>Client</label>
                <select value={form.clientId} onChange={(e) => set("clientId", e.target.value)}>
                  <option value="">—</option>
                  {props.clients.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="field">
                <label>Description / Scope</label>
                <textarea rows={3} value={form.description} onChange={(e) => set("description", e.target.value)} placeholder="Describe what the client needs…" />
              </div>
              <div className="row2">
                <div className="field">
                  <label>Priority</label>
                  <select value={form.priority} onChange={(e) => set("priority", e.target.value)}>
                    <option value="LOW">Low</option>
                    <option value="MEDIUM">Medium</option>
                    <option value="HIGH">High</option>
                    <option value="CRITICAL">Critical</option>
                  </select>
                </div>
                <div className="field">
                  <label>Target Date</label>
                  <input type="date" value={form.targetDate} onChange={(e) => set("targetDate", e.target.value)} />
                </div>
              </div>
              <div className="field">
                <label>Estimated Budget</label>
                <input type="number" value={form.budget} onChange={(e) => set("budget", e.target.value)} placeholder="0" />
              </div>
            </div>
            <div className="modal-foot">
              <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
              <button className="btn primary">Next →</button>
            </div>
          </form>
        ) : (
          <>
            <div className="modal-body">
              {error && <div className="form-error">{error}</div>}
              <p className="muted" style={{ marginTop: 0 }}>Review the requirement. You can save it as a draft or convert it directly into a project.</p>
              <div className="meta-grid">
                <div className="meta-item"><div className="meta-label">Title</div><div className="meta-value">{form.title}</div></div>
                <div className="meta-item"><div className="meta-label">Client</div><div className="meta-value">{client?.name ?? "—"}</div></div>
                <div className="meta-item"><div className="meta-label">Priority</div><div className="meta-value">{form.priority}</div></div>
                <div className="meta-item"><div className="meta-label">Target Date</div><div className="meta-value">{formatDate(form.targetDate)}</div></div>
                <div className="meta-item"><div className="meta-label">Budget</div><div className="meta-value">{form.budget ? `INR ${Number(form.budget).toLocaleString()}` : "—"}</div></div>
              </div>
              {form.description && (
                <div style={{ marginTop: 12 }}>
                  <div className="meta-label">Description</div>
                  <div style={{ marginTop: 4 }}>{form.description}</div>
                </div>
              )}
            </div>
            <div className="modal-foot">
              <button type="button" className="btn" onClick={() => setStep(1)}>← Back</button>
              <button type="button" className="btn" onClick={saveOnly} disabled={saving}>Save as Draft</button>
              <button type="button" className="btn primary" onClick={convertNow} disabled={saving}>{saving ? <span className="spinner" /> : "Convert to Project"}</button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function ConvertModal(props: {
  requirement: Requirement;
  clients: Client[];
  statuses: Lookup[];
  priorities: Lookup[];
  onClose: () => void;
  onConverted: (projectId: string) => void;
}) {
  const r = props.requirement;
  const [code, setCode] = useState(genCode());
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function convert() {
    setSaving(true);
    setError(null);
    try {
      const project = await api.createProject({
        code: code.trim(),
        name: r.title,
        description: r.description || undefined,
        clientId: r.clientId || undefined,
        statusId: pickStatusId(props.statuses),
        priorityId: pickPriorityId(props.priorities, r.priority),
        plannedEndDate: r.targetDate || undefined,
        budget: r.budget ? Number(r.budget) : undefined,
        currencyCode: "INR"
      });
      props.onConverted(project.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to convert to project.");
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-head">
          <h3>Convert to Project</h3>
          <button className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <p className="muted" style={{ marginTop: 0 }}>Create a project from requirement “{r.title}”.</p>
          <div className="field">
            <label>Project Code *</label>
            <input value={code} onChange={(e) => setCode(e.target.value)} required />
          </div>
          <div className="meta-grid">
            <div className="meta-item"><div className="meta-label">Name</div><div className="meta-value">{r.title}</div></div>
            <div className="meta-item"><div className="meta-label">Client</div><div className="meta-value">{r.clientName || "—"}</div></div>
            <div className="meta-item"><div className="meta-label">Priority</div><div className="meta-value">{r.priority}</div></div>
            <div className="meta-item"><div className="meta-label">Target Date</div><div className="meta-value">{formatDate(r.targetDate)}</div></div>
          </div>
        </div>
        <div className="modal-foot">
          <button className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" onClick={convert} disabled={saving}>{saving ? <span className="spinner" /> : "Create Project"}</button>
        </div>
      </div>
    </div>
  );
}
