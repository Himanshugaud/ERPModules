import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError, type InventoryItem, type Warehouse, type Bom, type WorkOrder } from "../api/client";
import { useAuth } from "../auth/AuthContext";

type Tab = "orders" | "boms";
const woBadge = (s: string) => s === "COMPLETED" ? "green" : s === "RELEASED" ? "blue" : s === "CANCELLED" ? "red" : "gray";

export default function Production() {
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const [tab, setTab] = useState<Tab>("orders");
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [boms, setBoms] = useState<Bom[]>([]);
  const [orders, setOrders] = useState<WorkOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [woModal, setWoModal] = useState(false);
  const [bomModal, setBomModal] = useState(false);
  const [complete, setComplete] = useState<WorkOrder | null>(null);

  const itemMap = useMemo(() => new Map(items.map((i) => [i.id, i])), [items]);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const [its, whs, bs, wos] = await Promise.all([
        api.items({ pageSize: 200 }), api.warehouses(), api.boms({ pageSize: 100 }), api.workOrders({ pageSize: 100 })
      ]);
      setItems(its.data);
      setWarehouses(whs);
      setBoms(bs.data);
      setOrders(wos.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load production data.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function doRelease(wo: WorkOrder) {
    try { await api.releaseWorkOrder(wo.id); await load(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Unable to release."); }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Production</h1>
          <div className="page-sub">Bills of materials and work orders (raw → finished).</div>
        </div>
        <div className="head-actions">
          {tab === "orders" && perms.includes("workorder.create") && <button className="btn primary" onClick={() => setWoModal(true)}>+ Work Order</button>}
          {tab === "boms" && perms.includes("bom.create") && <button className="btn primary" onClick={() => setBomModal(true)}>+ BOM</button>}
        </div>
      </div>

      <div className="topbar" style={{ padding: 0, border: "none", background: "none", marginBottom: 12 }}>
        <div className="tabs">
          <span className={`tab ${tab === "orders" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("orders")}>Work Orders</span>
          <span className={`tab ${tab === "boms" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("boms")}>Bills of Materials</span>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        {tab === "orders" ? (
          <table>
            <thead><tr><th>WO #</th><th>Output Item</th><th>Planned</th><th>Produced</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan={6}><div className="empty">Loading…</div></td></tr>
                : orders.length === 0 ? <tr><td colSpan={6}><div className="empty">No work orders yet.</div></td></tr>
                : orders.map((w) => (
                  <tr key={w.id}>
                    <td className="cell-code">{w.woNumber}</td>
                    <td style={{ fontWeight: 600 }}>{itemMap.get(w.outputItemId)?.name ?? "—"}</td>
                    <td>{w.plannedQty}</td>
                    <td>{w.producedQty}</td>
                    <td><span className={`badge ${woBadge(w.status)}`}>{w.status}</span></td>
                    <td style={{ textAlign: "right" }}>
                      {w.status === "DRAFT" && perms.includes("workorder.release") && <button className="btn btn-sm" onClick={() => doRelease(w)}>Release</button>}
                      {w.status === "RELEASED" && perms.includes("workorder.complete") && <button className="btn btn-sm primary" onClick={() => setComplete(w)}>Complete</button>}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        ) : (
          <table>
            <thead><tr><th>Code</th><th>Output Item</th><th>Output Qty</th><th>Components</th><th>Version</th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan={5}><div className="empty">Loading…</div></td></tr>
                : boms.length === 0 ? <tr><td colSpan={5}><div className="empty">No BOMs yet.</div></td></tr>
                : boms.map((b) => (
                  <tr key={b.id}>
                    <td className="cell-code">{b.code}</td>
                    <td style={{ fontWeight: 600 }}>{itemMap.get(b.outputItemId)?.name ?? "—"}</td>
                    <td>{b.outputQty}</td>
                    <td>{b.lines.length}</td>
                    <td>v{b.version}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        )}
      </div>

      {woModal && <WorkOrderModal items={items} warehouses={warehouses} boms={boms} onClose={() => setWoModal(false)} onSaved={async () => { setWoModal(false); await load(); }} />}
      {bomModal && <BomModal items={items} onClose={() => setBomModal(false)} onSaved={async () => { setBomModal(false); await load(); }} />}
      {complete && <CompleteModal wo={complete} onClose={() => setComplete(null)} onSaved={async () => { setComplete(null); await load(); }} />}
    </>
  );
}

function WorkOrderModal(props: { items: InventoryItem[]; warehouses: Warehouse[]; boms: Bom[]; onClose: () => void; onSaved: () => void }) {
  const [outputItemId, setOutputItemId] = useState("");
  const [bomId, setBomId] = useState("");
  const [plannedQty, setPlannedQty] = useState("");
  const [warehouseId, setWarehouseId] = useState(props.warehouses[0]?.id ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const matchingBoms = props.boms.filter((b) => !outputItemId || b.outputItemId === outputItemId);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!outputItemId || !plannedQty || !warehouseId) { setError("Output item, quantity and warehouse are required."); return; }
    setSaving(true);
    try {
      await api.createWorkOrder({ outputItemId, bomId: bomId || undefined, plannedQty: Number(plannedQty), warehouseId });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to create work order.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>New Work Order</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="field"><label>Output Item *</label>
            <select value={outputItemId} onChange={(e) => { setOutputItemId(e.target.value); setBomId(""); }} required>
              <option value="">Select item…</option>
              {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
            </select>
          </div>
          <div className="field"><label>Bill of Materials</label>
            <select value={bomId} onChange={(e) => setBomId(e.target.value)}>
              <option value="">— none (no auto components) —</option>
              {matchingBoms.map((b) => <option key={b.id} value={b.id}>{b.code} ({b.lines.length} components)</option>)}
            </select>
          </div>
          <div className="row2">
            <div className="field"><label>Planned Qty *</label><input type="number" step="0.01" value={plannedQty} onChange={(e) => setPlannedQty(e.target.value)} required /></div>
            <div className="field"><label>Warehouse *</label>
              <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Create"}</button>
        </div>
      </form>
    </div>
  );
}

function CompleteModal(props: { wo: WorkOrder; onClose: () => void; onSaved: () => void }) {
  const [producedQty, setProducedQty] = useState(props.wo.plannedQty.toString());
  const [scrapQty, setScrapQty] = useState("0");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      await api.completeWorkOrder(props.wo.id, { producedQty: Number(producedQty), scrapQty: Number(scrapQty || 0) });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to complete work order.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head"><h3>Complete {props.wo.woNumber}</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="page-sub" style={{ marginBottom: 12 }}>Consumes reserved components and produces the finished good into stock.</div>
          <div className="row2">
            <div className="field"><label>Produced Qty *</label><input type="number" step="0.01" value={producedQty} onChange={(e) => setProducedQty(e.target.value)} required /></div>
            <div className="field"><label>Scrap Qty</label><input type="number" step="0.01" value={scrapQty} onChange={(e) => setScrapQty(e.target.value)} /></div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Complete"}</button>
        </div>
      </form>
    </div>
  );
}

interface BomLineRow { componentItemId: string; qty: string; scrapPercent: string }

function BomModal(props: { items: InventoryItem[]; onClose: () => void; onSaved: () => void }) {
  const [code, setCode] = useState("");
  const [outputItemId, setOutputItemId] = useState("");
  const [outputQty, setOutputQty] = useState("1");
  const [lines, setLines] = useState<BomLineRow[]>([{ componentItemId: "", qty: "", scrapPercent: "0" }]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const setLine = (i: number, k: keyof BomLineRow, v: string) => setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, [k]: v } : l)));
  const addLine = () => setLines((ls) => [...ls, { componentItemId: "", qty: "", scrapPercent: "0" }]);
  const removeLine = (i: number) => setLines((ls) => ls.filter((_, idx) => idx !== i));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    const valid = lines.filter((l) => l.componentItemId && l.qty);
    if (!code || !outputItemId || !valid.length) { setError("Code, output item and at least one component are required."); return; }
    setSaving(true);
    try {
      await api.createBom({
        code: code.trim(),
        outputItemId,
        outputQty: Number(outputQty || 1),
        lines: valid.map((l) => ({ componentItemId: l.componentItemId, qty: Number(l.qty), scrapPercent: Number(l.scrapPercent || 0) }))
      });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to create BOM.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ maxWidth: 620 }}>
        <div className="modal-head"><h3>New Bill of Materials</h3><button type="button" className="icon-btn" onClick={props.onClose}>✕</button></div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>Code *</label><input value={code} onChange={(e) => setCode(e.target.value)} required placeholder="BOM-FRAME-01" /></div>
            <div className="field"><label>Output Qty *</label><input type="number" step="0.01" value={outputQty} onChange={(e) => setOutputQty(e.target.value)} required /></div>
          </div>
          <div className="field"><label>Output Item *</label>
            <select value={outputItemId} onChange={(e) => setOutputItemId(e.target.value)} required>
              <option value="">Select item…</option>
              {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
            </select>
          </div>
          <label style={{ fontSize: 13, fontWeight: 600 }}>Components</label>
          {lines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr 80px 80px 28px", gap: 8, alignItems: "center", marginTop: 6 }}>
              <select value={l.componentItemId} onChange={(e) => setLine(i, "componentItemId", e.target.value)}>
                <option value="">Select item…</option>
                {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
              </select>
              <input placeholder="Qty" type="number" step="0.01" value={l.qty} onChange={(e) => setLine(i, "qty", e.target.value)} />
              <input placeholder="Scrap%" type="number" step="0.01" value={l.scrapPercent} onChange={(e) => setLine(i, "scrapPercent", e.target.value)} />
              <button type="button" className="icon-btn" onClick={() => removeLine(i)} disabled={lines.length === 1}>✕</button>
            </div>
          ))}
          <button type="button" className="btn btn-sm" style={{ marginTop: 8 }} onClick={addLine}>+ Add component</button>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Create BOM"}</button>
        </div>
      </form>
    </div>
  );
}
