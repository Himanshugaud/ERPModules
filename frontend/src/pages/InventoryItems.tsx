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

type Group = { key: string; label: string; items: InventoryItem[] };

/** Pull every page of a filtered item list (the API caps a page at 100). */
async function fetchAllItems(params: Record<string, string | number | boolean | undefined>): Promise<InventoryItem[]> {
  const all: InventoryItem[] = [];
  for (let page = 1; page <= 20; page++) {
    const res = await api.items({ ...params, page, pageSize: 100 });
    all.push(...res.data);
    if (res.data.length < 100) break;
  }
  return all;
}

export default function InventoryItems() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes("item.create") ?? false;
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [categories, setCategories] = useState<ItemCategory[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [activeRoot, setActiveRoot] = useState<string>("");
  const [fabOnly, setFabOnly] = useState(false);
  const [lowOnly, setLowOnly] = useState(false);
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [modal, setModal] = useState<{ mode: "create" | "edit"; item?: InventoryItem } | null>(null);
  const [catModal, setCatModal] = useState(false);

  const uomMap = useMemo(() => new Map(uoms.map((u) => [u.id, u.code])), [uoms]);
  const catMap = useMemo(() => new Map(categories.map((c) => [c.id, c])), [categories]);

  // Categories that have sub-categories or items; empty legacy ones are hidden from the tabs.
  const roots = useMemo(() => {
    const active = categories.filter((c) => c.isActive);
    const subtreeCount = (id: string): number =>
      (catMap.get(id)?.itemCount ?? 0) + active.filter((c) => c.parentCategoryId === id).reduce((n, c) => n + subtreeCount(c.id), 0);
    return active
      .filter((c) => !c.parentCategoryId)
      .map((c) => ({ ...c, total: subtreeCount(c.id) }))
      .filter((c) => c.total > 0 || active.some((x) => x.parentCategoryId === c.id))
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
  }, [categories, catMap]);

  async function load(rootId = activeRoot) {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string | boolean> = {};
      if (search.trim()) params.search = search.trim();
      if (rootId) params.categoryId = rootId;
      if (lowOnly) params.lowStock = true;
      if (fabOnly) params.manufactured = true;
      const [list, cats, us] = await Promise.all([fetchAllItems(params), api.itemCategories(), api.uoms()]);
      setItems(list);
      setCategories(cats);
      setUoms(us);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load items.");
    } finally {
      setLoading(false);
    }
  }

  // The first tab is selected once categories are known.
  useEffect(() => {
    api.itemCategories().then((cats) => {
      const first = cats.filter((c) => c.isActive && !c.parentCategoryId && (c.itemCount > 0 || cats.some((x) => x.parentCategoryId === c.id)))
        .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))[0];
      setActiveRoot(first?.id ?? "");
      load(first?.id ?? "");
    }).catch(() => load(""));
    // eslint-disable-next-line
  }, []);

  useEffect(() => { if (!loading || items.length) load(); /* eslint-disable-next-line */ }, [activeRoot, lowOnly, fabOnly]);

  const groups: Group[] = useMemo(() => {
    const byCat = new Map<string, InventoryItem[]>();
    for (const it of items) {
      const k = it.categoryId ?? "none";
      byCat.set(k, [...(byCat.get(k) ?? []), it]);
    }
    // A freshly added sub-category has no items yet; keep it visible when nothing is being filtered.
    if (activeRoot && !search.trim() && !fabOnly && !lowOnly) {
      for (const c of categories) if (c.isActive && c.parentCategoryId === activeRoot && !byCat.has(c.id)) byCat.set(c.id, []);
    }
    const label = (id: string) => {
      const c = catMap.get(id);
      if (!c) return "Uncategorised";
      const parent = c.parentCategoryId ? catMap.get(c.parentCategoryId) : undefined;
      return activeRoot || !parent ? c.name : `${parent.name} › ${c.name}`;
    };
    const rank = (id: string) => {
      const c = catMap.get(id);
      if (!c) return [999, 999, ""] as const;
      const parent = c.parentCategoryId ? catMap.get(c.parentCategoryId) : undefined;
      return [(parent ?? c).sortOrder || 900, parent ? c.sortOrder || 900 : 0, c.name] as const;
    };
    return [...byCat.entries()]
      .sort(([a], [b]) => {
        const ra = rank(a), rb = rank(b);
        return ra[0] - rb[0] || ra[1] - rb[1] || ra[2].localeCompare(rb[2]);
      })
      .map(([k, list]) => ({ key: k, label: label(k), items: list }));
  }, [items, catMap, activeRoot, categories, search, fabOnly, lowOnly]);

  const searching = search.trim().length > 0;
  // Sections open by default only when searching (so hits are visible) or for the first one.
  useEffect(() => {
    setOpen(new Set(searching ? groups.map((g) => g.key) : groups.slice(0, 1).map((g) => g.key)));
    // eslint-disable-next-line
  }, [items]);
  const isOpen = (key: string) => open.has(key);
  const toggle = (key: string) =>
    setOpen((prev) => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Items</h1>
          <div className="page-sub">Universal item list — everything Planning, Supply Chain and the factory pick from, grouped by category and sub-category.</div>
        </div>
        <div className="head-actions">
          <form className="search" onSubmit={(e) => { e.preventDefault(); load(); }}>
            <span>⌕</span>
            <input placeholder="Search items…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </form>
          {canManage && <button className="btn" onClick={() => setCatModal(true)}>+ Category</button>}
          {canManage && <button className="btn primary" onClick={() => setModal({ mode: "create" })}>+ Add Item</button>}
        </div>
      </div>

      <div className="tabs" style={{ marginBottom: 12 }}>
        <a className={`tab ${activeRoot === "" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setActiveRoot("")}>All</a>
        {roots.map((r) => (
          <a key={r.id} className={`tab ${activeRoot === r.id ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setActiveRoot(r.id)}>
            {r.name} <span className="muted">({r.total})</span>
          </a>
        ))}
      </div>

      <div className="filter-bar" style={{ marginBottom: 12 }}>
        <label className="chip removable" onClick={() => setFabOnly((v) => !v)} style={{ background: fabOnly ? "var(--blue-soft)" : undefined, color: fabOnly ? "var(--blue)" : undefined }}>
          {fabOnly ? "✓ " : ""}Fabricated in factory
        </label>
        <label className="chip removable" onClick={() => setLowOnly((v) => !v)} style={{ background: lowOnly ? "var(--amber-soft)" : undefined, color: lowOnly ? "var(--amber)" : undefined }}>
          {lowOnly ? "✓ " : ""}Low stock only
        </label>
        <span className="muted" style={{ marginLeft: "auto" }}>{loading ? "Loading…" : `${items.length} items in ${groups.length} sections`}</span>
        <button className="btn btn-sm" onClick={() => setOpen(new Set(groups.map((g) => g.key)))}>Expand all</button>
        <button className="btn btn-sm" onClick={() => setOpen(new Set())}>Collapse all</button>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      {loading && items.length === 0 ? (
        <div className="card card-pad">{[...Array(4)].map((_, i) => <div key={i} className="skeleton" style={{ height: 16, width: 260, marginBottom: 12 }} />)}</div>
      ) : groups.length === 0 ? (
        <div className="empty">No items found.</div>
      ) : (
        groups.map((g) => {
          const expanded = isOpen(g.key);
          const fab = g.items.filter((i) => i.isManufactured).length;
          return (
            <div className="card" key={g.key} style={{ marginBottom: 10 }}>
              <div className="section-head" style={{ borderBottom: expanded ? "1px solid var(--border)" : "none" }}>
                <button type="button" className="section-toggle" onClick={() => toggle(g.key)} aria-expanded={expanded}>
                  <span className={`chev ${expanded ? "open" : ""}`}>▸</span>
                  <span><strong>{g.label}</strong><span className="muted" style={{ marginLeft: 8 }}>({g.items.length})</span></span>
                </button>
                {fab > 0 && <span className="badge blue">{fab} fabricated</span>}
              </div>
              {expanded && (
                <div className="table-wrap" style={{ border: "none", borderRadius: 0 }}>
                  <table>
                    <thead><tr><th style={{ width: 150 }}>Code</th><th>Item</th><th style={{ width: 70 }}>Unit</th><th style={{ width: 130 }}>Source</th><th style={{ width: 90 }}>Status</th><th style={{ width: 70 }}></th></tr></thead>
                    <tbody>
                      {g.items.map((it) => (
                        <tr key={it.id}>
                          <td className="cell-code">{it.code}</td>
                          <td style={{ fontWeight: 600 }}>{it.name}</td>
                          <td>{it.baseUomId ? uomMap.get(it.baseUomId) ?? "—" : "—"}</td>
                          <td>{it.isManufactured ? <span className="badge blue">Fabricated</span> : <span className="badge gray">Purchased</span>}</td>
                          <td><span className={`badge ${it.isActive ? "green" : "gray"}`}>{it.isActive ? "Active" : "Inactive"}</span></td>
                          <td>{canManage && <button className="btn btn-sm" onClick={() => setModal({ mode: "edit", item: it })}>Edit</button>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          );
        })
      )}

      {modal && (
        <ItemModal
          mode={modal.mode}
          item={modal.item}
          categories={categories}
          uoms={uoms}
          defaultRootId={activeRoot}
          onClose={() => setModal(null)}
          onSaved={async () => { setModal(null); await load(); }}
        />
      )}
      {catModal && (
        <CategoryModal
          categories={categories}
          onClose={() => setCatModal(false)}
          onSaved={async () => { setCatModal(false); await load(); }}
        />
      )}
    </>
  );
}

function CategoryModal(props: { categories: ItemCategory[]; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState("");
  const [parentId, setParentId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const topLevel = props.categories.filter((c) => c.isActive && !c.parentCategoryId);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const code = name.trim().toUpperCase().replace(/[^A-Z0-9]+/g, "_").replace(/^_|_$/g, "").slice(0, 40);
    try {
      await api.createItemCategory({ code, name: name.trim(), parentCategoryId: parentId || undefined });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to save category.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" style={{ maxWidth: 460 }} onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>Add Category / Sub-category</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field"><label>Name *</label><input value={name} onChange={(e) => setName(e.target.value)} required placeholder="e.g. Flooring" /></div>
          <div className="field"><label>Parent category</label>
            <select value={parentId} onChange={(e) => setParentId(e.target.value)}>
              <option value="">None — new top-level category</option>
              {topLevel.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </div>
          <div className="muted" style={{ fontSize: 12 }}>A sub-category added under Structure &amp; Civil, Plumbing or Electrical appears automatically as a section in the requirement sheets.</div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving || !name.trim()}>{saving ? <span className="spinner" /> : "Add"}</button>
        </div>
      </form>
    </div>
  );
}

function ItemModal(props: {
  mode: "create" | "edit";
  item?: InventoryItem;
  categories: ItemCategory[];
  uoms: Uom[];
  defaultRootId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = props.mode === "edit";
  const it = props.item;
  const initialCat = it?.categoryId ? props.categories.find((c) => c.id === it.categoryId) : undefined;
  const [rootId, setRootId] = useState(initialCat?.parentCategoryId ?? initialCat?.id ?? props.defaultRootId);
  const [subId, setSubId] = useState(initialCat?.parentCategoryId ? initialCat.id : "");
  const [form, setForm] = useState({
    code: it?.code ?? "",
    name: it?.name ?? "",
    itemType: it?.itemType ?? "RAW_MATERIAL",
    baseUomId: it?.baseUomId ?? "",
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

  const topLevel = props.categories.filter((c) => c.isActive && !c.parentCategoryId);
  const subs = props.categories.filter((c) => c.isActive && c.parentCategoryId === rootId);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const payload = {
      name: form.name.trim(),
      itemType: form.itemType,
      categoryId: subId || rootId || undefined,
      baseUomId: form.baseUomId || undefined,
      standardCost: form.standardCost ? Number(form.standardCost) : undefined,
      reorderLevel: form.reorderLevel ? Number(form.reorderLevel) : undefined,
      trackBatches: form.trackBatches,
      trackExpiry: form.trackExpiry,
      isManufactured: form.isManufactured,
      isPurchasable: !form.isManufactured
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
            <div className="field"><label>Category</label>
              <select value={rootId} onChange={(e) => { setRootId(e.target.value); setSubId(""); }}>
                <option value="">—</option>
                {topLevel.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div className="field"><label>Sub-category</label>
              <select value={subId} onChange={(e) => setSubId(e.target.value)} disabled={subs.length === 0}>
                <option value="">—</option>
                {subs.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field"><label>Type *</label>
              <select value={form.itemType} onChange={(e) => set("itemType", e.target.value)}>
                {ITEM_TYPES.map((t) => <option key={t.v} value={t.v}>{t.l}</option>)}
              </select>
            </div>
            <div className="field"><label>Base Unit</label>
              <select value={form.baseUomId} onChange={(e) => set("baseUomId", e.target.value)}>
                <option value="">—</option>
                {props.uoms.map((u) => <option key={u.id} value={u.id}>{u.code} — {u.name}</option>)}
              </select>
            </div>
          </div>
          <div className="row2">
            <div className="field"><label>Standard Cost</label><input type="number" step="0.01" value={form.standardCost} onChange={(e) => set("standardCost", e.target.value)} /></div>
            <div className="field"><label>Reorder Level</label><input type="number" step="0.01" value={form.reorderLevel} onChange={(e) => set("reorderLevel", e.target.value)} /></div>
          </div>
          <div className="chips-row">
            <label className="chip removable" onClick={() => set("isManufactured", !form.isManufactured)} style={{ background: form.isManufactured ? "var(--blue-soft)" : undefined, color: form.isManufactured ? "var(--blue)" : undefined }}>{form.isManufactured ? "✓ " : ""}Fabricated in factory</label>
            <label className="chip removable" onClick={() => set("trackBatches", !form.trackBatches)} style={{ background: form.trackBatches ? "var(--blue-soft)" : undefined, color: form.trackBatches ? "var(--blue)" : undefined }}>{form.trackBatches ? "✓ " : ""}Track batches</label>
            <label className="chip removable" onClick={() => set("trackExpiry", !form.trackExpiry)} style={{ background: form.trackExpiry ? "var(--blue-soft)" : undefined, color: form.trackExpiry ? "var(--blue)" : undefined }}>{form.trackExpiry ? "✓ " : ""}Track expiry</label>
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
