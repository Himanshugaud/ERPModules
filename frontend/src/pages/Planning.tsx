import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError, type Project, type Lookup, type Warehouse, type InventoryItem, type MaterialRequirement, type ProjectPlan, type SheetCatalogType, type UserItem } from "../api/client";
import AttachmentsPanel from "../components/AttachmentsPanel";
import PlanCalendar from "../components/PlanCalendar";
import SheetCreator from "../components/SheetCreator";
import { downloadRequirementXlsx, parseRequirementXlsx, requirementXlsxFile, type ParsedSheet } from "../lib/sheetExcel";
import { formatDate, formatDateTime } from "../lib/ui";
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

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.projects({ statusCode: showAll ? undefined : "PLANNING", pageSize: 100 });
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
    api.uoms().then(setUoms).catch(() => setUoms([]));
  }, []);

  // The item list is only needed inside the workspace, so fetch it when one is opened.
  useEffect(() => {
    if (selected && items.length === 0) api.allItems().then((r) => setItems(r.data)).catch(() => setItems([]));
    // eslint-disable-next-line
  }, [selected]);

  useEffect(() => { load(); }, [showAll]);

  const statusMap = Object.fromEntries(statuses.map((s) => [s.id, s]));

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Planning</h1>
          <div className="page-sub">Finalized requirements land here. Build the materials estimate, plan the calendar, attach documents, then publish to Supply Chain.</div>
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
                      <button className="btn btn-sm primary" onClick={(e) => { e.stopPropagation(); setSelected(p); }}>Plan Workspace</button>
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
  const perms = user?.permissions ?? [];
  const canManagePlan = perms.includes("plan.manage");
  const canEditMaterials = perms.includes("materialrequirement.create");
  const [tab, setTab] = useState<"materials" | "calendar" | "attachments" | "publish">("materials");
  const [plan, setPlan] = useState<ProjectPlan | null>(null);
  const [users, setUsers] = useState<UserItem[]>([]);
  const [publishNotes, setPublishNotes] = useState("");
  const [publishing, setPublishing] = useState(false);
  const [fabRaised, setFabRaised] = useState<number | null>(null);
  const [requirements, setRequirements] = useState<MaterialRequirement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [catalog, setCatalog] = useState<SheetCatalogType[]>([]);
  const [creator, setCreator] = useState<{ typeCode?: string; parsed?: ParsedSheet; replace?: MaterialRequirement } | null>(null);
  const importInput = useRef<HTMLInputElement>(null);

  const published = plan?.status === "PUBLISHED";

  async function loadPlan() {
    try { setPlan(await api.projectPlan(props.project.id)); } catch { setPlan(null); }
  }

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
    await loadPlan();
  }

  useEffect(() => {
    load();
    api.users().then(setUsers).catch(() => setUsers([]));
    api.sheetCatalog().then(setCatalog).catch(() => setCatalog([]));
  }, []);

  async function deleteDraft(r: MaterialRequirement) {
    if (!window.confirm(`Delete draft estimate ${r.reqNumber}?`)) return;
    setError(null);
    try {
      await api.deleteMaterialRequirement(r.id);
      await removeSheetFile(r);
      await load();
    }
    catch (err) { setError(err instanceof ApiError ? err.message : "Unable to delete the draft."); }
  }

  const sheetFileMarker = (r: MaterialRequirement) => `Material sheet ${r.reqNumber} `;

  // The Excel copy of a sheet lives in the project's Planning Attachments; best effort, it is not the source of truth.
  async function removeSheetFile(r: MaterialRequirement) {
    try {
      const files = await api.projectAttachments(props.project.id, "PLANNING");
      for (const f of files.filter((x) => x.description?.startsWith(sheetFileMarker(r)))) await api.deleteAttachment(f.id);
    } catch { /* plan already published or no access: leave the file */ }
  }

  async function saveSheetFile(saved: MaterialRequirement, replaced?: MaterialRequirement) {
    try {
      if (replaced) await removeSheetFile(replaced);
      const all = await api.allItems();
      const file = await requirementXlsxFile(saved, {
        orgName: user?.organizationName ?? "",
        projectName: props.project.name,
        projectCode: props.project.code,
        items: all.data,
        uoms: props.uoms,
        sheetTypeName: catalog.find((t) => t.code === saved.sheetType)?.name
      });
      await api.uploadAttachment(props.project.id, "PLANNING", file, `${sheetFileMarker(saved)}(auto-generated)`);
    } catch {
      setError("The sheet was saved, but its Excel copy could not be added to Planning attachments.");
    }
  }

  async function publish() {
    if (!window.confirm("Publish this plan? Supply Chain will receive the material list and the Execution team will see the project plan.")) return;
    setPublishing(true);
    setError(null);
    try {
      const result = await api.publishProjectPlan(props.project.id, publishNotes.trim() || undefined);
      setPlan(result);
      setFabRaised(result.fabricationOrdersRaised ?? 0);
      setPublishNotes("");
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to publish the plan.");
    } finally {
      setPublishing(false);
    }
  }

  function downloadCsv(r: MaterialRequirement) {
    const sheetTypeName = catalog.find((t) => t.code === r.sheetType)?.name;
    downloadRequirementXlsx(r, {
      orgName: user?.organizationName ?? "",
      projectName: props.project.name,
      projectCode: props.project.code,
      items: props.items,
      uoms: props.uoms,
      sheetTypeName
    }).catch(() => setError("Unable to create the Excel file."));
  }

  async function importExcel(file: File | undefined) {
    if (!file) return;
    setError(null);
    try {
      const parsed = await parseRequirementXlsx(file, catalog);
      if (parsed.lines.length === 0 && parsed.unmatched.length === 0) { setError("No quantities were found in that file."); return; }
      setCreator({ parsed });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to read the Excel file.");
    } finally {
      if (importInput.current) importInput.current.value = "";
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 960, width: "96vw" }}>
        <div className="modal-head">
          <div>
            <h3>Plan Workspace</h3>
            <div className="muted" style={{ fontSize: 13 }}>{props.project.name} · {props.project.code}</div>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {plan && <span className={`badge ${published ? "green" : "amber"}`}>{published ? "Published" : "Draft"}</span>}
            <button className="icon-btn" onClick={props.onClose}>✕</button>
          </div>
        </div>
        <div className="modal-body">
          <div className="tabs" style={{ marginBottom: 14 }}>
            {([["materials", "Materials"], ["calendar", "Calendar Planner"], ["attachments", "Attachments"], ["publish", "Publish"]] as const).map(([key, text]) => (
              <span key={key} className={`tab ${tab === key ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab(key)}>{text}</span>
            ))}
          </div>
          {error && <div className="form-error">{error}</div>}

          {tab === "materials" && (<>
          {canEditMaterials && (
            <div className="head-actions" style={{ marginBottom: 10, flexWrap: "wrap" }}>
              {catalog.map((t) => (
                <button key={t.code} className="btn primary btn-sm" onClick={() => setCreator({ typeCode: t.code })}>+ {t.name} sheet</button>
              ))}
              <input ref={importInput} type="file" accept=".xlsx" hidden onChange={(e) => importExcel(e.target.files?.[0])} />
              <button className="btn btn-sm" onClick={() => importInput.current?.click()} disabled={catalog.length === 0}>⭱ Import Excel</button>
              <button className="btn btn-sm" onClick={() => setAddOpen(true)}>+ Custom items</button>
              {!published && <span className="muted" style={{ fontSize: 12 }}>Sheets stay as drafts until you publish the plan.</span>}
            </div>
          )}
          {loading ? (
            <div className="skeleton" style={{ height: 60 }} />
          ) : requirements.length === 0 ? (
            <div className="empty">No requirement sheets yet. Create one per material type (structure, plumbing, electrical…) or import an Excel sheet, then publish the plan to send it to Supply Chain.</div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Sheet</th><th>Ship to</th><th>Priority</th><th>Items</th><th>Status</th><th>Required</th><th></th></tr></thead>
                <tbody>
                  {requirements.map((r) => (
                    <tr key={r.id}>
                      <td>
                        <div className="cell-title">{catalog.find((t) => t.code === r.sheetType)?.name ?? "Custom items"}</div>
                        <div className="cell-code">{r.reqNumber}{r.blockName ? ` · ${r.blockName}` : ""}{r.blockSize ? ` (${r.blockSize})` : ""}</div>
                      </td>
                      <td>{r.destinationAddress || props.warehouses.find((w) => w.id === r.warehouseId)?.name || "—"}</td>
                      <td>{r.priority}</td>
                      <td>
                        <div className="muted" style={{ fontSize: 13 }}>{r.lines.length} item{r.lines.length === 1 ? "" : "s"}</div>
                        <button type="button" className="btn-link" onClick={() => downloadCsv(r)}>⭳ Excel</button>
                      </td>
                      <td><span className={`badge ${r.status === "SUBMITTED" ? "blue" : r.status === "APPROVED" ? "green" : r.status === "REJECTED" ? "red" : r.status === "DRAFT" ? "amber" : "gray"}`}>{r.status}</span></td>
                      <td>{formatDate(r.requiredDate)}</td>
                      <td>
                        {r.status === "DRAFT" && canEditMaterials && (
                          <div className="row-actions">
                            {r.sheetType && <button className="btn btn-sm" onClick={() => setCreator({ replace: r, typeCode: r.sheetType })}>Edit</button>}
                            <button className="btn btn-sm danger" onClick={() => deleteDraft(r)}>Delete</button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          </>)}

          {tab === "calendar" && (
            <PlanCalendar projectId={props.project.id} canEdit={canManagePlan} users={users} onChanged={loadPlan} />
          )}

          {tab === "attachments" && (
            <AttachmentsPanel projectId={props.project.id} defaultCategory="PLANNING" users={users} deletable={!published && canManagePlan} onChanged={loadPlan} />
          )}

          {tab === "publish" && (
            <div>
              <p className="muted" style={{ marginTop: 0 }}>
                Publishing hands the plan over: Supply Chain receives the material list to arrange stock or purchase orders, and the Execution team can see the project plan.
              </p>
              <div className="meta-grid" style={{ marginTop: 8 }}>
                <div className="meta-item"><div className="meta-label">Materials estimates</div><div className="meta-value">{plan?.requirementCount ?? 0}{plan && plan.draftRequirementCount > 0 ? ` (${plan.draftRequirementCount} draft)` : ""}</div></div>
                <div className="meta-item"><div className="meta-label">Planning attachments</div><div className="meta-value">{plan?.planningAttachmentCount ?? 0}</div></div>
                <div className="meta-item"><div className="meta-label">Calendar items</div><div className="meta-value">{plan?.calendarEntryCount ?? 0}</div></div>
              </div>
              {plan && (plan.requirementCount === 0 || plan.planningAttachmentCount === 0 || plan.calendarEntryCount === 0) && (
                <div className="draft-note" style={{ marginTop: 14 }}>
                  <span className="dot" style={{ background: "var(--amber)" }} />
                  {[plan.requirementCount === 0 && "add a materials estimate (required)", plan.planningAttachmentCount === 0 && "upload planning documents", plan.calendarEntryCount === 0 && "plan the calendar"].filter(Boolean).join(" · ")}
                </div>
              )}
              {published && (
                <div className="badge green" style={{ marginTop: 14 }}>Published {formatDateTime(plan?.publishedAt)}{plan?.notes ? ` · ${plan.notes}` : ""}</div>
              )}
              {fabRaised !== null && (
                <div className="draft-note" style={{ marginTop: 10 }}>
                  <span className="dot" style={{ background: "var(--blue)" }} />
                  {fabRaised > 0
                    ? `${fabRaised} fabrication order${fabRaised > 1 ? "s" : ""} raised for factory-made items that stock cannot cover — see Inventory → Production.`
                    : "No fabrication needed — factory-made items on this plan are covered by stock (or there are none)."}
                </div>
              )}
              {canManagePlan ? (
                <div style={{ marginTop: 16 }}>
                  <div className="field">
                    <label>Hand-over note (optional)</label>
                    <textarea rows={2} value={publishNotes} onChange={(e) => setPublishNotes(e.target.value)} maxLength={1000} placeholder="Anything Supply Chain and Execution should know" />
                  </div>
                  <button className="btn primary" disabled={publishing || !plan || plan.requirementCount === 0} onClick={publish}>
                    {publishing ? <span className="spinner" /> : published ? (plan && plan.draftRequirementCount > 0 ? "Publish new estimates" : "Re-publish") : "Publish plan"}
                  </button>
                </div>
              ) : (
                <div className="muted" style={{ marginTop: 14 }}>Only the Planning team can publish a plan.</div>
              )}
            </div>
          )}
        </div>
        <div className="modal-foot">
          <button className="btn" onClick={props.onClose}>Close</button>
        </div>
      </div>

      {creator && (
        <SheetCreator
          project={props.project}
          catalog={catalog}
          warehouses={props.warehouses}
          asDraft={!published}
          typeCode={creator.typeCode}
          parsed={creator.parsed}
          replace={creator.replace}
          onClose={() => setCreator(null)}
          onSaved={async (saved, replaced) => { setCreator(null); await saveSheetFile(saved, replaced); await load(); }}
        />
      )}

      {addOpen && (
        <AddMaterialsModal
          project={props.project}
          asDraft={!published}
          warehouses={props.warehouses}
          items={props.items}
          uoms={props.uoms}
          onClose={() => setAddOpen(false)}
          onSaved={async (saved) => { setAddOpen(false); await saveSheetFile(saved); await load(); }}
        />
      )}
    </div>
  );
}

type Draft = { warehouseId: string; destinationAddress: string; priority: string; requiredDate: string; notes: string; lines: LineRow[] };
const EMPTY_LINE: LineRow = { itemId: "", qty: "", uomId: "", notes: "" };

function AddMaterialsModal(props: { project: Project; asDraft: boolean; warehouses: Warehouse[]; items: InventoryItem[]; uoms: Lookup[]; onClose: () => void; onSaved: (saved: MaterialRequirement) => void }) {
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
      const saved = await api.createMaterialRequirement({
        projectId: props.project.id,
        asDraft: props.asDraft,
        warehouseId: warehouseId || undefined,
        destinationAddress: destinationAddress.trim(),
        priority,
        requiredDate: requiredDate || undefined,
        notes: notes || undefined,
        lines: valid.map((l) => ({ itemId: l.itemId, qty: Number(l.qty), uomId: l.uomId || undefined, notes: l.notes || undefined }))
      });
      localStorage.removeItem(draftKey);
      props.onSaved(saved);
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
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : props.asDraft ? "Save to estimate" : "Send to Supply Chain"}</button>
        </div>
      </form>
    </div>
  );
}
