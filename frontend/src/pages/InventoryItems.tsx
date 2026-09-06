import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError, type InventoryItem, type ItemCategory, type Uom } from "../api/client";
import { useAuth } from "../auth/AuthContext";

const ITEM_TYPES = [
  { v: "RAW_MATERIAL", l: "Raw Material" },
  { v: "FINISHED_GOOD", l: "Finished Good" },
  { v: "SEMI_FINISHED", l: "Semi-Finished (WIP)" },
  { v: "CONSUMABLE", l: "Consumable" },
  { v: "SPARE_PART", l: "Spare Part" },
  { v: "TOOL_EQUIPMENT", l: "Tool / Equipment" }
];
const typeLabel = (v: string) => ITEM_TYPES.find((t) => t.v === v)?.l ?? v;

export default function InventoryItems() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes("item.create") ?? false;
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [categories, setCategories] = useState<ItemCategory[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [lowOnly, setLowOnly] = useState(false);
  const [modal, setModal] = useState<{ mode: "create" | "edit"; item?: InventoryItem } | null>(null);

  const catMap = useMemo(() => new Map(categories.map((c) => [c.id, c.name])), [categories]);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string | boolean> = {};
      if (search.trim()) params.search = search.trim();
      if (typeFilter) params.itemType = typeFilter;
      if (lowOnly) params.lowStock = true;
      const [res, cats, us] = await Promise.all([api.items({ ...params, pageSize: 100 }), api.itemCategories(), api.uoms()]);
      setItems(res.data);
      setCategories(cats);
      setUoms(us);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load items.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [typeFilter, lowOnly]);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Items</h1>
          <div className="page-sub">Raw materials, finished products, consumables &amp; equipment.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={(e) => { e.preventDefault(); load(); }}>
            <span>⌕</span>
            <input placeholder="Search items…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          {canManage && <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Item</button>}
        </div>
      </div>

      <div className="filter-bar" style={{ marginBottom: 12 }}>
        <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
          <option value="">All types</option>
          {ITEM_TYPES.map((t) => <option key={t.v} value={t.v}>{t.l}</option>)}
        </select>
        <label className="chip removable" onClick={() => setLowOnly((v) => !v)} style={{ background: lowOnly ? "var(--amber-soft)" : undefined, color: lowOnly ? "var(--amber)" : undefined }}>
          {lowOnly ? "✓ " : ""}Low stock only
        </label>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Code</th><th>Item</th><th>Type</th><th>Category</th><th>Reorder Level</th><th>Valuation</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(5)].map((_, i) => (
                <tr key={i}>{[...Array(8)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 1 ? 160 : 80 }} /></td>)}</tr>
              ))
            ) : items.length === 0 ? (
              <tr><td colSpan={8}><div className="empty">No items found.</div></td></tr>
            ) : (
              items.map((it) => (
                <tr key={it.id}>
                  <td className="cell-code">{it.code}</td>
                  <td style={{ fontWeight: 600 }}>{it.name}</td>
                  <td><span className={`badge ${it.itemType === "FINISHED_GOOD" ? "blue" : it.itemType === "RAW_MATERIAL" ? "amber" : "gray"}`}>{typeLabel(it.itemType)}</span></td>
                  <td>{it.categoryId ? catMap.get(it.categoryId) ?? "—" : "—"}</td>
                  <td>{it.reorderLevel ?? "—"}</td>
                  <td>{it.valuationMethod}</td>
                  <td><span className={`badge ${it.isActive ? "green" : "gray"}`}>{it.isActive ? "Active" : "Inactive"}</span></td>
                  <td>{canManage && <button className="btn btn-sm" onClick={() => setModal({ mode: "edit", item: it })}>Edit</button>}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {modal && (
        <ItemModal
          mode={modal.mode}
          item={modal.item}
          categories={categories}
          uoms={uoms}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await load(); }}
        />
      )}
    </>
  );
}

function ItemModal(props: {
  mode: "create" | "edit";
  item?: InventoryItem;
  categories: ItemCategory[];
  uoms: Uom[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = props.mode === "edit";
  const it = props.item;
  const [form, setForm] = useState({
    code: it?.code ?? "",
    name: it?.name ?? "",
    itemType: it?.itemType ?? "RAW_MATERIAL",
    categoryId: it?.categoryId ?? "",
    baseUomId: it?.baseUomId ?? "",
    valuationMethod: it?.valuationMethod ?? "WEIGHTED_AVG",
    standardCost: it?.standardCost?.toString() ?? "",
    reorderLevel: it?.reorderLevel?.toString() ?? "",
    trackBatches: it?.trackBatches ?? false,
    trackExpiry: it?.trackExpiry ?? false,
    isManufactured: it?.isManufactured ?? false,
    isActive: it?.isActive ?? true
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string | boolean) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const payload = {
      name: form.name.trim(),
      itemType: form.itemType,
      categoryId: form.categoryId || undefined,
      baseUomId: form.baseUomId || undefined,
      valuationMethod: form.valuationMethod,
      standardCost: form.standardCost ? Number(form.standardCost) : undefined,
      reorderLevel: form.reorderLevel ? Number(form.reorderLevel) : undefined,
      trackBatches: form.trackBatches,
      trackExpiry: form.trackExpiry,
      isManufactured: form.isManufactured
    };
    try {
      if (editing && it) {
        await api.updateItem(it.id, { ...payload, isActive: form.isActive });
      } else {
        await api.createItem({ ...payload, code: form.code.trim() });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save item.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>{editing ? "Edit Item" : "Add Item"}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Code *</label><input value={form.code} onChange={(e) => set("code", e.target.value)} disabled={editing} required placeholder="CEM-OPC-50" /></div>
            <div className="field"><label>Name *</label><input value={form.name} onChange={(e) => set("name", e.target.value)} required placeholder="OPC Cement 50kg" /></div>
          </div>
          <div className="row2">
            <div className="field"><label>Type *</label>
              <select value={form.itemType} onChange={(e) => set("itemType", e.target.value)}>
                {ITEM_TYPES.map((t) => <option key={t.v} value={t.v}>{t.l}</option>)}
              </select>
            </div>
            <div className="field"><label>Category</label>
              <select value={form.categoryId} onChange={(e) => set("categoryId", e.target.value)}>
                <option value="">—</option>
                {props.categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field"><label>Base Unit</label>
              <select value={form.baseUomId} onChange={(e) => set("baseUomId", e.target.value)}>
                <option value="">—</option>
                {props.uoms.map((u) => <option key={u.id} value={u.id}>{u.code} — {u.name}</option>)}
              </select>
            </div>
            <div className="field"><label>Valuation</label>
              <select value={form.valuationMethod} onChange={(e) => set("valuationMethod", e.target.value)}>
                <option value="WEIGHTED_AVG">Weighted Average</option>
                <option value="FIFO">FIFO</option>
                <option value="STANDARD">Standard Cost</option>
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field"><label>Standard Cost</label><input type="number" step="0.01" value={form.standardCost} onChange={(e) => set("standardCost", e.target.value)} /></div>
            <div className="field"><label>Reorder Level</label><input type="number" step="0.01" value={form.reorderLevel} onChange={(e) => set("reorderLevel", e.target.value)} /></div>
          </div>
          <div className="chips-row">
            <label className="chip removable" onClick={() => set("trackBatches", !form.trackBatches)} style={{ background: form.trackBatches ? "var(--blue-soft)" : undefined, color: form.trackBatches ? "var(--blue)" : undefined }}>{form.trackBatches ? "✓ " : ""}Track batches</label>
            <label className="chip removable" onClick={() => set("trackExpiry", !form.trackExpiry)} style={{ background: form.trackExpiry ? "var(--blue-soft)" : undefined, color: form.trackExpiry ? "var(--blue)" : undefined }}>{form.trackExpiry ? "✓ " : ""}Track expiry</label>
            <label className="chip removable" onClick={() => set("isManufactured", !form.isManufactured)} style={{ background: form.isManufactured ? "var(--blue-soft)" : undefined, color: form.isManufactured ? "var(--blue)" : undefined }}>{form.isManufactured ? "✓ " : ""}Manufactured</label>
            {editing && <label className="chip removable" onClick={() => set("isActive", !form.isActive)} style={{ background: form.isActive ? "var(--green-soft)" : undefined, color: form.isActive ? "var(--green)" : undefined }}>{form.isActive ? "✓ Active" : "Inactive"}</label>}
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : editing ? "Save Changes" : "Add Item"}</button>
        </div>
      </form>
    </div>
  );
}
