import { useMemo, useRef, useState, type FormEvent } from "react";
import { api, ApiError, type MaterialRequirement, type Project, type SheetCatalogType, type Warehouse } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { downloadTemplateXlsx, parseRequirementXlsx, type ParsedSheet } from "../lib/sheetExcel";

type Props = {
  project: Project;
  catalog: SheetCatalogType[];
  warehouses: Warehouse[];
  /** True until the plan is published: sheets are saved as drafts. */
  asDraft: boolean;
  /** Pre-select a sheet type. */
  typeCode?: string;
  /** Values read from an Excel file. */
  parsed?: ParsedSheet;
  /** Editing a draft: it is replaced by the new version on save. */
  replace?: MaterialRequirement;
  onClose: () => void;
  onSaved: (saved: MaterialRequirement, replaced?: MaterialRequirement) => void;
};

export default function SheetCreator({ project, catalog, warehouses, asDraft, typeCode, parsed, replace, onClose, onSaved }: Props) {
  const { user } = useAuth();
  const orgName = user?.organizationName ?? "";
  const siteWarehouses = warehouses.filter((w) => w.warehouseType === "SITE_STORE" && w.projectId === project.id);

  const [code, setCode] = useState(() => parsed?.sheetTypeCode ?? typeCode ?? replace?.sheetType ?? catalog[0]?.code ?? "");
  const type = catalog.find((t) => t.code === code) ?? catalog[0];

  const [qty, setQty] = useState<Record<string, string>>(() => {
    const q: Record<string, string> = {};
    for (const l of replace?.lines ?? []) q[l.itemId] = String(l.qty);
    for (const l of parsed?.lines ?? []) q[l.itemId] = String(l.qty);
    return q;
  });
  const [remark, setRemark] = useState<Record<string, string>>(() => {
    const r: Record<string, string> = {};
    for (const l of replace?.lines ?? []) if (l.notes) r[l.itemId] = l.notes;
    for (const l of parsed?.lines ?? []) if (l.remark) r[l.itemId] = l.remark;
    return r;
  });
  const [blockName, setBlockName] = useState(parsed?.header.blockName ?? replace?.blockName ?? "");
  const [blockSize, setBlockSize] = useState(parsed?.header.blockSize ?? replace?.blockSize ?? "");
  const [destination, setDestination] = useState(
    parsed?.header.destination ?? replace?.destinationAddress ?? siteWarehouses[0]?.name ?? ""
  );
  const [warehouseId, setWarehouseId] = useState(replace?.warehouseId ?? "");
  const validPriority = (p?: string) => (p && ["LOW", "MEDIUM", "HIGH", "URGENT"].includes(p.toUpperCase()) ? p.toUpperCase() : undefined);
  const [priority, setPriority] = useState(validPriority(parsed?.header.priority) ?? replace?.priority ?? "MEDIUM");
  const [requiredDate, setRequiredDate] = useState(
    /^\d{4}-\d{2}-\d{2}$/.test(parsed?.header.requiredDate ?? "") ? parsed!.header.requiredDate! : replace?.requiredDate ?? ""
  );
  const [notes, setNotes] = useState(parsed?.header.notes ?? replace?.notes ?? "");
  const [search, setSearch] = useState("");
  const [onlyFilled, setOnlyFilled] = useState(false);
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [unmatched, setUnmatched] = useState(parsed?.unmatched ?? []);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const typeItemIds = useMemo(() => new Set(type?.sections.flatMap((s) => s.items.map((i) => i.id)) ?? []), [type]);
  const filledIds = Object.keys(qty).filter((id) => Number(qty[id]) > 0 && typeItemIds.has(id));
  const q = search.trim().toLowerCase();
  const filtering = q.length > 0 || onlyFilled;

  function switchType(next: string) {
    if (next === code) return;
    if (filledIds.length > 0 && !window.confirm("Switching the sheet type clears the quantities entered so far. Continue?")) return;
    setCode(next);
    setQty({});
    setRemark({});
    setOpen({});
  }

  async function importFile(file: File | undefined) {
    if (!file) return;
    setError(null);
    setBusy(true);
    try {
      const result = await parseRequirementXlsx(file, catalog);
      const target = catalog.find((t) => t.code === (result.sheetTypeCode ?? code)) ?? type;
      const ids = new Set(target.sections.flatMap((s) => s.items.map((i) => i.id)));
      const inType = result.lines.filter((l) => ids.has(l.itemId));
      const otherType = result.lines.length - inType.length;
      if (target.code !== code) setCode(target.code);
      setQty(Object.fromEntries(inType.map((l) => [l.itemId, String(l.qty)])));
      setRemark(Object.fromEntries(inType.filter((l) => l.remark).map((l) => [l.itemId, l.remark!])));
      if (result.header.blockName) setBlockName(result.header.blockName);
      if (result.header.blockSize) setBlockSize(result.header.blockSize);
      if (result.header.destination) setDestination(result.header.destination);
      if (validPriority(result.header.priority)) setPriority(validPriority(result.header.priority)!);
      if (/^\d{4}-\d{2}-\d{2}$/.test(result.header.requiredDate ?? "")) setRequiredDate(result.header.requiredDate!);
      if (result.header.notes) setNotes(result.header.notes);
      setUnmatched([
        ...result.unmatched,
        ...(otherType > 0 ? [{ label: `${otherType} item(s) belong to a different sheet type and were skipped`, qty: 0 }] : [])
      ]);
      setOpen({});
      if (inType.length === 0) setError("No items from this file matched the sheet. Check the unmatched list below.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to read the Excel file.");
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = "";
    }
  }

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!type) return;
    if (filledIds.length === 0) { setError("Enter a quantity for at least one item."); return; }
    if (!destination.trim()) { setError("Enter the ship-to address (site) for this sheet."); return; }
    const lines = type.sections.flatMap((s) =>
      s.items.filter((i) => Number(qty[i.id]) > 0).map((i) => ({
        itemId: i.id, qty: Number(qty[i.id]), uomId: i.uomId, notes: remark[i.id]?.trim() || undefined, sectionName: s.name
      }))
    );
    setBusy(true);
    try {
      const saved = await api.createMaterialRequirement({
        projectId: project.id,
        asDraft,
        sheetType: type.code,
        blockName: blockName.trim() || undefined,
        blockSize: blockSize.trim() || undefined,
        warehouseId: warehouseId || undefined,
        destinationAddress: destination.trim(),
        priority,
        requiredDate: requiredDate || undefined,
        notes: notes.trim() || undefined,
        lines
      });
      if (replace) await api.deleteMaterialRequirement(replace.id).catch(() => undefined);
      onSaved(saved, replace);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save the sheet.");
      setBusy(false);
    }
  }

  if (!type) {
    return (
      <div className="modal-overlay" onClick={onClose}>
        <div className="modal" onClick={(e) => e.stopPropagation()}>
          <div className="modal-body"><div className="empty">No requirement sheet types are set up yet.</div></div>
          <div className="modal-foot"><button className="btn" onClick={onClose}>Close</button></div>
        </div>
      </div>
    );
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <form className="modal" style={{ maxWidth: 980, width: "96vw" }} onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <div>
            <h3>{replace ? "Edit requirement sheet" : "New requirement sheet"}</h3>
            <div className="muted" style={{ fontSize: 13 }}>{project.name} · {project.code}</div>
          </div>
          <button type="button" className="icon-btn" onClick={onClose}>✕</button>
        </div>

        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}

          <div className="sheet-types">
            {catalog.map((t) => (
              <button type="button" key={t.code} className={`sheet-type ${t.code === code ? "active" : ""}`} onClick={() => switchType(t.code)} disabled={!!replace && t.code !== code}>
                <strong>{t.name}</strong>
                <span className="muted">{t.sections.length} sections · {t.sections.reduce((n, s) => n + s.items.length, 0)} items</span>
              </button>
            ))}
          </div>

          <div className="row2">
            <div className="field"><label>Block name</label><input value={blockName} onChange={(e) => setBlockName(e.target.value)} maxLength={100} placeholder="e.g. Site Office" /></div>
            <div className="field"><label>Block size</label><input value={blockSize} onChange={(e) => setBlockSize(e.target.value)} maxLength={100} placeholder="e.g. 11x8.5x3 Mtr." /></div>
          </div>
          <div className="field"><label>Ship to (site address) *</label>
            <input value={destination} onChange={(e) => setDestination(e.target.value)} maxLength={300} required placeholder="Site address and contact" /></div>
          <div className="row2">
            <div className="field"><label>Link to warehouse (optional)</label>
              <select value={warehouseId} onChange={(e) => {
                setWarehouseId(e.target.value);
                const w = warehouses.find((x) => x.id === e.target.value);
                if (w) setDestination(w.address ? `${w.name} — ${w.address}` : w.name);
              }}>
                <option value="">Not linked — address only</option>
                {(siteWarehouses.length ? siteWarehouses : warehouses).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select></div>
            <div className="row2">
              <div className="field"><label>Priority</label>
                <select value={priority} onChange={(e) => setPriority(e.target.value)}>
                  <option value="LOW">Low</option><option value="MEDIUM">Medium</option><option value="HIGH">High</option><option value="URGENT">Urgent</option>
                </select></div>
              <div className="field"><label>Required by</label><input type="date" value={requiredDate} onChange={(e) => setRequiredDate(e.target.value)} /></div>
            </div>
          </div>

          <div className="sheet-toolbar">
            <input className="sheet-search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={`Search ${type.name} items…`} />
            <label className="sheet-check"><input type="checkbox" checked={onlyFilled} onChange={(e) => setOnlyFilled(e.target.checked)} /> Only items with quantity</label>
            <span className="muted" style={{ fontSize: 13 }}>{filledIds.length} item{filledIds.length === 1 ? "" : "s"} selected</span>
            <span style={{ flex: 1 }} />
            <input ref={fileInput} type="file" hidden accept=".xlsx" onChange={(e) => importFile(e.target.files?.[0])} />
            <button type="button" className="btn btn-sm" disabled={busy} onClick={() => fileInput.current?.click()}>⭱ Import Excel</button>
            <button type="button" className="btn btn-sm" disabled={busy} onClick={() => downloadTemplateXlsx(type, { orgName, projectName: project.name, projectCode: project.code })}>⭳ Blank Excel</button>
            {!filtering && (
              <button type="button" className="btn-link" onClick={() => {
                const all = type.sections.every((s) => open[s.id]);
                setOpen(Object.fromEntries(type.sections.map((s) => [s.id, !all])));
              }}>{type.sections.every((s) => open[s.id]) ? "Collapse all" : "Expand all"}</button>
            )}
          </div>

          {unmatched.length > 0 && (
            <details className="sheet-unmatched" open>
              <summary>{unmatched.length} line{unmatched.length === 1 ? "" : "s"} from the file could not be matched to the item master</summary>
              <ul>{unmatched.map((u, i) => <li key={i}>{u.label}{u.qty ? ` — qty ${u.qty}` : ""}</li>)}</ul>
              <div className="muted" style={{ fontSize: 12 }}>Add the missing items under Inventory → Items, then import again — or use Custom items for one-offs.</div>
            </details>
          )}

          {type.sections.map((section) => {
            const items = section.items.filter((i) => (!q || i.name.toLowerCase().includes(q) || i.code.toLowerCase().includes(q)) && (!onlyFilled || Number(qty[i.id]) > 0));
            if (filtering && items.length === 0) return null;
            const filled = section.items.filter((i) => Number(qty[i.id]) > 0).length;
            const isOpen = filtering || open[section.id];
            return (
              <div key={section.id} className="sheet-section">
                <button type="button" className="sheet-section-head" aria-expanded={!!isOpen} onClick={() => setOpen((o) => ({ ...o, [section.id]: !o[section.id] }))}>
                  <span><span className={`chev ${isOpen ? "open" : ""}`}>▸</span> {section.name}</span>
                  <span className={`badge ${filled ? "blue" : "gray"}`}>{filled} / {section.items.length}</span>
                </button>
                {isOpen && (
                  items.length === 0 ? <div className="muted" style={{ padding: "10px 14px", fontSize: 13 }}>No items in this section yet — add items to the matching category under Inventory → Items.</div> : (
                    <div className="sheet-rows">
                      <div className="sheet-row head"><span>Item</span><span>Unit</span><span>Qty</span><span>Remark</span></div>
                      {items.map((i) => (
                        <div key={i.id} className={`sheet-row ${Number(qty[i.id]) > 0 ? "filled" : ""}`}>
                          <span title={i.code}>{i.name}{i.isFabricated && <span className="badge blue" style={{ marginLeft: 6, padding: "1px 7px", fontSize: 11 }} title="Made in the factory — goes on the fabrication list">Fabricated</span>}</span>
                          <span className="muted">{i.uomCode ?? ""}</span>
                          <input type="number" min={0} step="any" inputMode="decimal" value={qty[i.id] ?? ""} onChange={(e) => setQty((s) => ({ ...s, [i.id]: e.target.value }))} aria-label={`${i.name} quantity`} />
                          <input value={remark[i.id] ?? ""} maxLength={500} onChange={(e) => setRemark((s) => ({ ...s, [i.id]: e.target.value }))} placeholder="Remark" aria-label={`${i.name} remark`} />
                        </div>
                      ))}
                    </div>
                  )
                )}
              </div>
            );
          })}

          <div className="field" style={{ marginTop: 14 }}><label>Notes</label>
            <input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} placeholder="Anything Supply Chain should know about this sheet" /></div>
        </div>

        <div className="modal-foot">
          <span className="muted" style={{ marginRight: "auto", fontSize: 13 }}>{orgName}</span>
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>{busy ? <span className="spinner" /> : asDraft ? "Save sheet" : "Send to Supply Chain"}</button>
        </div>
      </form>
    </div>
  );
}
