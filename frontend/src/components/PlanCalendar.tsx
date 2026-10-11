import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError, type PlanEntry, type UserItem } from "../api/client";

const TYPES = ["ACTIVITY", "MILESTONE", "MATERIAL", "INSPECTION", "MEETING", "OTHER"] as const;
const STATUSES = ["PLANNED", "IN_PROGRESS", "DONE", "CANCELLED"] as const;
const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

const pad = (n: number) => String(n).padStart(2, "0");
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const label = (v: string) => v.charAt(0) + v.slice(1).toLowerCase().replace(/_/g, " ");

function gridDays(month: Date): Date[] {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const offset = (first.getDay() + 6) % 7; // Monday-first
  const start = new Date(first.getFullYear(), first.getMonth(), 1 - offset);
  const weeks = Math.ceil((offset + new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()) / 7);
  return Array.from({ length: weeks * 7 }, (_, i) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + i));
}

type Props = { projectId: string; canEdit: boolean; users?: UserItem[]; onChanged?: () => void };

export default function PlanCalendar({ projectId, canEdit, users = [], onChanged }: Props) {
  const [month, setMonth] = useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  const [entries, setEntries] = useState<PlanEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<{ entry?: PlanEntry; date?: string } | null>(null);
  const [dayView, setDayView] = useState<string | null>(null);

  const days = useMemo(() => gridDays(month), [month]);
  const from = iso(days[0]);
  const to = iso(days[days.length - 1]);
  const today = iso(new Date());
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  async function load() {
    try {
      setEntries(await api.planEntries(projectId, from, to));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load the calendar.");
    }
  }
  useEffect(() => { load(); }, [projectId, from, to]);

  const onDay = (d: string) => entries.filter((e) => e.startDate <= d && e.endDate >= d);
  const shift = (delta: number) => setMonth((m) => new Date(m.getFullYear(), m.getMonth() + delta, 1));

  return (
    <div>
      <div className="cal-head">
        <div className="cal-nav">
          <button type="button" className="btn btn-sm" onClick={() => shift(-1)} aria-label="Previous month">‹</button>
          <strong className="cal-title">{month.toLocaleDateString(undefined, { month: "long", year: "numeric" })}</strong>
          <button type="button" className="btn btn-sm" onClick={() => shift(1)} aria-label="Next month">›</button>
          <button type="button" className="btn btn-sm" onClick={() => setMonth(new Date(new Date().getFullYear(), new Date().getMonth(), 1))}>Today</button>
        </div>
        {canEdit && <button type="button" className="btn btn-sm primary" onClick={() => setEditor({ date: today })}>+ Add plan item</button>}
      </div>

      {error && <div className="form-error">{error}</div>}

      <div className="cal-grid">
        {WEEKDAYS.map((w) => <div key={w} className="cal-dow">{w}</div>)}
        {days.map((d) => {
          const key = iso(d);
          const list = onDay(key);
          const inMonth = d.getMonth() === month.getMonth();
          return (
            <div
              key={key}
              className={`cal-cell ${inMonth ? "" : "out"} ${key === today ? "today" : ""} ${canEdit ? "editable" : ""}`}
              onClick={() => (canEdit ? setEditor({ date: key }) : list.length ? setDayView(key) : undefined)}
            >
              <div className="cal-daynum">{d.getDate()}</div>
              {list.slice(0, 3).map((e) => (
                <button
                  type="button"
                  key={e.id}
                  className={`cal-chip type-${e.entryType} ${e.status === "DONE" ? "done" : ""} ${e.status === "CANCELLED" ? "cancelled" : ""}`}
                  title={`${e.title} · ${label(e.entryType)} · ${label(e.status)}`}
                  onClick={(ev) => { ev.stopPropagation(); setEditor({ entry: e }); }}
                >
                  {e.title}
                </button>
              ))}
              {list.length > 3 && (
                <button type="button" className="cal-more" onClick={(ev) => { ev.stopPropagation(); setDayView(key); }}>+{list.length - 3} more</button>
              )}
            </div>
          );
        })}
      </div>

      <div className="cal-legend">
        {TYPES.map((t) => <span key={t} className={`cal-chip type-${t}`}>{label(t)}</span>)}
      </div>

      {dayView && (
        <div className="modal-overlay" onClick={() => setDayView(null)}>
          <div className="modal" style={{ maxWidth: 460 }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-head"><h3>{new Date(dayView + "T00:00:00").toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long" })}</h3>
              <button className="icon-btn" onClick={() => setDayView(null)}>✕</button></div>
            <div className="modal-body">
              {onDay(dayView).map((e) => (
                <div key={e.id} className="attach-row">
                  <div>
                    <button type="button" className={`cal-chip type-${e.entryType}`} onClick={() => { setDayView(null); setEditor({ entry: e }); }}>{e.title}</button>
                    <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>
                      {label(e.entryType)} · {label(e.status)}{e.assignedTo ? ` · ${userMap[e.assignedTo]?.displayName ?? ""}` : ""}
                    </div>
                    {e.description && <div style={{ fontSize: 13, marginTop: 4 }}>{e.description}</div>}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {editor && (
        <EntryModal
          projectId={projectId}
          entry={editor.entry}
          date={editor.date}
          canEdit={canEdit}
          users={users}
          onClose={() => setEditor(null)}
          onSaved={async () => { setEditor(null); await load(); onChanged?.(); }}
        />
      )}
    </div>
  );
}

function EntryModal(props: {
  projectId: string; entry?: PlanEntry; date?: string; canEdit: boolean; users: UserItem[];
  onClose: () => void; onSaved: () => void;
}) {
  const e = props.entry;
  const [title, setTitle] = useState(e?.title ?? "");
  const [description, setDescription] = useState(e?.description ?? "");
  const [entryType, setEntryType] = useState(e?.entryType ?? "ACTIVITY");
  const [status, setStatus] = useState(e?.status ?? "PLANNED");
  const [startDate, setStartDate] = useState(e?.startDate ?? props.date ?? iso(new Date()));
  const [endDate, setEndDate] = useState(e?.endDate ?? props.date ?? iso(new Date()));
  const [assignedTo, setAssignedTo] = useState(e?.assignedTo ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function save(ev: FormEvent) {
    ev.preventDefault();
    if (endDate < startDate) { setError("End date cannot be before the start date."); return; }
    setSaving(true);
    setError(null);
    const payload = { title: title.trim(), description: description.trim() || undefined, entryType, status, startDate, endDate, assignedTo: assignedTo || undefined };
    try {
      if (e) await api.updatePlanEntry(e.id, payload); else await api.createPlanEntry(props.projectId, payload);
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save the plan item.");
      setSaving(false);
    }
  }

  async function remove() {
    if (!e || !window.confirm(`Delete "${e.title}"?`)) return;
    setSaving(true);
    try { await api.deletePlanEntry(e.id); props.onSaved(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Unable to delete."); setSaving(false); }
  }

  const ro = !props.canEdit;
  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(x) => x.stopPropagation()} onSubmit={save} style={{ maxWidth: 520 }}>
        <div className="modal-head">
          <h3>{e ? (ro ? "Plan item" : "Edit plan item") : "New plan item"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field"><label>What is planned? *</label>
            <input value={title} onChange={(x) => setTitle(x.target.value)} required maxLength={200} disabled={ro} placeholder="e.g. Excavation of foundation" /></div>
          <div className="row2">
            <div className="field"><label>Start date *</label>
              <input type="date" value={startDate} onChange={(x) => { setStartDate(x.target.value); if (endDate < x.target.value) setEndDate(x.target.value); }} required disabled={ro} /></div>
            <div className="field"><label>End date</label>
              <input type="date" value={endDate} min={startDate} onChange={(x) => setEndDate(x.target.value)} disabled={ro} /></div>
          </div>
          <div className="row2">
            <div className="field"><label>Type</label>
              <select value={entryType} onChange={(x) => setEntryType(x.target.value)} disabled={ro}>{TYPES.map((t) => <option key={t} value={t}>{label(t)}</option>)}</select></div>
            <div className="field"><label>Status</label>
              <select value={status} onChange={(x) => setStatus(x.target.value)} disabled={ro}>{STATUSES.map((s) => <option key={s} value={s}>{label(s)}</option>)}</select></div>
          </div>
          <div className="field"><label>Assigned to</label>
            <select value={assignedTo} onChange={(x) => setAssignedTo(x.target.value)} disabled={ro}>
              <option value="">Unassigned</option>
              {props.users.map((u) => <option key={u.id} value={u.id}>{u.displayName ?? u.email}</option>)}
            </select></div>
          <div className="field"><label>Notes</label>
            <textarea rows={3} value={description} onChange={(x) => setDescription(x.target.value)} maxLength={1000} disabled={ro} /></div>
        </div>
        <div className="modal-foot">
          {e && !ro && <button type="button" className="btn danger" style={{ marginRight: "auto" }} onClick={remove} disabled={saving}>Delete</button>}
          <button type="button" className="btn" onClick={props.onClose}>{ro ? "Close" : "Cancel"}</button>
          {!ro && <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Save"}</button>}
        </div>
      </form>
    </div>
  );
}
