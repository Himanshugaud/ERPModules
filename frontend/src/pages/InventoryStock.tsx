import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, ApiError, type InventoryItem, type Warehouse, type StockLevel, type StockMovement, type PurchaseOrder, type Supplier, type Uom } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate } from "../lib/ui";

type Tab = "levels" | "low" | "movements";
type TxnKind = "issue" | "transfer" | "adjust";
const money = (n: number) => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const MOVE_TYPES = ["RECEIPT", "ISSUE", "TRANSFER_IN", "TRANSFER_OUT", "ADJUSTMENT", "PRODUCTION_IN", "CONSUMPTION", "RETURN_IN", "RETURN_OUT", "SCRAP", "OPENING_BALANCE"];

export default function InventoryStock() {
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [levels, setLevels] = useState<StockLevel[]>([]);
  const [movements, setMovements] = useState<StockMovement[]>([]);
  const [tab, setTab] = useState<Tab>("levels");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [txn, setTxn] = useState<TxnKind | "receive" | null>(null);

  // Filters
  const [fItem, setFItem] = useState("");
  const [fWarehouse, setFWarehouse] = useState("");
  const [fType, setFType] = useState("");
  const [fFrom, setFFrom] = useState("");
  const [fTo, setFTo] = useState("");

  const itemMap = useMemo(() => new Map(items.map((i) => [i.id, i])), [items]);
  const whMap = useMemo(() => new Map(warehouses.map((w) => [w.id, w.name])), [warehouses]);

  async function loadRefs() {
    const [its, whs] = await Promise.all([api.items({ pageSize: 200 }), api.warehouses()]);
    setItems(its.data);
    setWarehouses(whs);
  }

  async function load() {
    setLoading(true);
    setError(null);
    try {
      await loadRefs();
      if (tab === "levels")
        setLevels((await api.stock({ pageSize: 200, itemId: fItem || undefined, warehouseId: fWarehouse || undefined })).data);
      else if (tab === "low")
        setLevels((await api.lowStock({ pageSize: 200, warehouseId: fWarehouse || undefined })).data);
      else
        setMovements((await api.stockMovements({
          pageSize: 100,
          itemId: fItem || undefined,
          warehouseId: fWarehouse || undefined,
          movementType: fType || undefined,
          dateFrom: fFrom || undefined,
          dateTo: fTo || undefined
        })).data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load stock.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [tab, fItem, fWarehouse, fType, fFrom, fTo]);

  function clearFilters() {
    setFItem(""); setFWarehouse(""); setFType(""); setFFrom(""); setFTo("");
  }
  const hasFilters = fItem || fWarehouse || fType || fFrom || fTo;

  const totalValue = levels.reduce((s, l) => s + l.stockValue, 0);
  const lowCount = tab === "low" ? levels.length : levels.filter((l) => {
    const it = itemMap.get(l.itemId);
    return it?.reorderLevel != null && l.qtyOnHand <= it.reorderLevel;
  }).length;

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Stock</h1>
          <div className="page-sub">On-hand balances, valuation &amp; movement history.</div>
        </div>
        <div className="head-actions">
          {perms.includes("goodsreceipt.create") && <button className="btn primary" onClick={() => setTxn("receive")}>+ Receive</button>}
          {perms.includes("issue.create") && <button className="btn" onClick={() => setTxn("issue")}>Issue</button>}
          {perms.includes("transfer.create") && <button className="btn" onClick={() => setTxn("transfer")}>Transfer</button>}
          {perms.includes("stock.adjust") && <button className="btn" onClick={() => setTxn("adjust")}>Adjust</button>}
        </div>
      </div>

      <div className="kpis" style={{ gridTemplateColumns: "repeat(3, 1fr)" }}>
        <div className="kpi"><div className="label">Stock Lines</div><div className="value">{levels.length}</div></div>
        <div className="kpi"><div className="label">Inventory Value</div><div className="value">₹{money(totalValue)}</div></div>
        <div className="kpi"><div className="label">Below Reorder</div><div className="value" style={{ color: lowCount ? "var(--amber)" : undefined }}>{lowCount}</div></div>
      </div>

      <div className="topbar" style={{ padding: 0, border: "none", background: "none", marginBottom: 12 }}>
        <div className="tabs">
          {(["levels", "low", "movements"] as Tab[]).map((t) => (
            <span key={t} className={`tab ${tab === t ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab(t)}>
              {t === "levels" ? "Stock Levels" : t === "low" ? "Low Stock" : "Movements"}
            </span>
          ))}
        </div>
      </div>

      <div className="filter-bar" style={{ marginBottom: 12 }}>
        {tab !== "low" && (
          <select value={fItem} onChange={(e) => setFItem(e.target.value)}>
            <option value="">All items</option>
            {items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
          </select>
        )}
        <select value={fWarehouse} onChange={(e) => setFWarehouse(e.target.value)}>
          <option value="">All warehouses</option>
          {warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </select>
        {tab === "movements" && (
          <>
            <select value={fType} onChange={(e) => setFType(e.target.value)}>
              <option value="">All types</option>
              {MOVE_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
            <input className="filter-date" type="date" title="From date" value={fFrom} onChange={(e) => setFFrom(e.target.value)} />
            <input className="filter-date" type="date" title="To date" value={fTo} onChange={(e) => setFTo(e.target.value)} />
          </>
        )}
        {hasFilters && <button className="btn btn-sm" onClick={clearFilters}>Clear</button>}
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        {tab === "movements" ? (
          <table>
            <thead><tr><th>Date</th><th>Item</th><th>Warehouse</th><th>Type</th><th>Dir</th><th>Qty</th><th>Unit Cost</th><th>Value</th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan={8}><div className="empty">Loading…</div></td></tr>
                : movements.length === 0 ? <tr><td colSpan={8}><div className="empty">No movements yet.</div></td></tr>
                : movements.map((m) => (
                  <tr key={m.id}>
                    <td>{formatDate(m.occurredAt)}</td>
                    <td>{itemMap.get(m.itemId)?.name ?? "—"}</td>
                    <td>{whMap.get(m.warehouseId) ?? "—"}</td>
                    <td><span className="chip">{m.movementType}</span></td>
                    <td><span className={`badge ${m.direction === "IN" ? "green" : "red"}`}>{m.direction}</span></td>
                    <td>{m.qty}</td>
                    <td>{money(m.unitCost)}</td>
                    <td>₹{money(m.totalCost)}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        ) : (
          <table>
            <thead><tr><th>Item</th><th>Warehouse</th><th>On Hand</th><th>Available</th><th>Avg Cost</th><th>Value</th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan={6}><div className="empty">Loading…</div></td></tr>
                : levels.length === 0 ? <tr><td colSpan={6}><div className="empty">No stock records.</div></td></tr>
                : levels.map((l) => {
                  const it = itemMap.get(l.itemId);
                  const low = it?.reorderLevel != null && l.qtyOnHand <= it.reorderLevel;
                  return (
                    <tr key={l.id}>
                      <td style={{ fontWeight: 600 }}>{it?.name ?? "—"}<span className="muted" style={{ marginLeft: 6 }}>{it?.code}</span></td>
                      <td>{whMap.get(l.warehouseId) ?? "—"}</td>
                      <td>{l.qtyOnHand}{low && <span className="badge amber" style={{ marginLeft: 8 }}>Low</span>}</td>
                      <td>{l.qtyAvailable}</td>
                      <td>{money(l.avgUnitCost)}</td>
                      <td>₹{money(l.stockValue)}</td>
                    </tr>
                  );
                })}
            </tbody>
          </table>
        )}
      </div>

      {txn === "receive" ? (
        <GoodsReceiptModal
          items={items}
          warehouses={warehouses}
          onClose={() => setTxn(null)}
          onSaved={async () => { setTxn(null); await load(); }}
        />
      ) : txn && (
        <TxnModal
          kind={txn}
          items={items}
          warehouses={warehouses}
          onClose={() => setTxn(null)}
          onSaved={async () => { setTxn(null); await load(); }}
        />
      )}
    </>
  );
}

interface LineRow { itemId: string; qty: string; unitCost: string; batchNo: string }

const RECEIVABLE_PO_STATUSES = ["APPROVED", "ORDERED", "PARTIALLY_RECEIVED"];

interface ReceiveRow {
  lineId: string;
  itemId: string;
  uomId?: string;
  orderedQty: number;
  receivedQty: number;
  remainingQty: number;
  receiveQty: string;
  unitCost: string;
  batchNo: string;
}

function GoodsReceiptModal(props: { items: InventoryItem[]; warehouses: Warehouse[]; onClose: () => void; onSaved: () => void }) {
  const [pos, setPos] = useState<PurchaseOrder[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [refsLoaded, setRefsLoaded] = useState(false);
  const [mode, setMode] = useState<"po" | "manual">("po");
  const [poId, setPoId] = useState("");
  const [warehouseId, setWarehouseId] = useState(props.warehouses[0]?.id ?? "");
  const [poReference, setPoReference] = useState("");
  const [receivedDate, setReceivedDate] = useState("");
  const [rows, setRows] = useState<ReceiveRow[]>([]);
  const [manualLines, setManualLines] = useState<LineRow[]>([{ itemId: "", qty: "", unitCost: "", batchNo: "" }]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const itemMap = useMemo(() => new Map(props.items.map((i) => [i.id, i])), [props.items]);
  const uomMap = useMemo(() => new Map(uoms.map((u) => [u.id, u])), [uoms]);
  const supplierMap = useMemo(() => new Map(suppliers.map((s) => [s.id, s])), [suppliers]);
  const receivablePos = useMemo(() => pos.filter((p) => RECEIVABLE_PO_STATUSES.includes(p.status)), [pos]);
  const selectedPo = receivablePos.find((p) => p.id === poId) ?? null;

  useEffect(() => {
    (async () => {
      try {
        const [poRes, supRes, uomRes] = await Promise.all([
          api.purchaseOrders({ pageSize: 100 }),
          api.suppliers({ pageSize: 100 }),
          api.uoms()
        ]);
        setPos(poRes.data);
        setSuppliers(supRes.data);
        setUoms(uomRes);
        const receivable = poRes.data.filter((p) => RECEIVABLE_PO_STATUSES.includes(p.status));
        if (receivable.length === 0) setMode("manual");
        else setPoId(receivable[0].id);
      } finally {
        setRefsLoaded(true);
      }
    })();
  }, []);

  useEffect(() => {
    if (!selectedPo) { setRows([]); return; }
    setPoReference(selectedPo.poNumber);
    if (selectedPo.warehouseId) setWarehouseId(selectedPo.warehouseId);
    setRows(
      selectedPo.lines
        .map((l) => {
          const remaining = Math.round((l.qty - l.qtyReceived) * 100) / 100;
          return {
            lineId: l.id,
            itemId: l.itemId,
            uomId: l.uomId,
            orderedQty: l.qty,
            receivedQty: l.qtyReceived,
            remainingQty: remaining,
            receiveQty: remaining > 0 ? String(remaining) : "",
            unitCost: String(l.unitPrice),
            batchNo: ""
          };
        })
        .filter((r) => r.remainingQty > 0)
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [poId, pos]);

  const setRow = (i: number, k: keyof ReceiveRow, v: string) =>
    setRows((rs) => rs.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const setManualLine = (i: number, k: keyof LineRow, v: string) =>
    setManualLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, [k]: v } : l)));
  const addManualLine = () => setManualLines((ls) => [...ls, { itemId: "", qty: "", unitCost: "", batchNo: "" }]);
  const removeManualLine = (i: number) => setManualLines((ls) => ls.filter((_, idx) => idx !== i));

  const uomCode = (r: ReceiveRow) => uomMap.get(r.uomId ?? itemMap.get(r.itemId)?.baseUomId ?? "")?.code ?? "";
  const poTotal = rows.reduce((s, r) => s + (Number(r.receiveQty) || 0) * (Number(r.unitCost) || 0), 0);
  const manualTotal = manualLines.reduce((s, l) => s + (Number(l.qty) || 0) * (Number(l.unitCost) || 0), 0);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!warehouseId) { setError("Select a warehouse."); return; }

    let lines: { itemId: string; qty: number; unitCost: number; batchNo?: string }[];
    let poRefValue: string;
    let purchaseOrderId: string | undefined;

    if (mode === "po") {
      if (!selectedPo) { setError("Select a purchase order."); return; }
      const valid = rows.filter((r) => Number(r.receiveQty) > 0);
      if (!valid.length) { setError("Enter a receiving quantity for at least one item."); return; }
      lines = valid.map((r) => ({ itemId: r.itemId, qty: Number(r.receiveQty), unitCost: Number(r.unitCost || 0), batchNo: r.batchNo || undefined }));
      poRefValue = selectedPo.poNumber;
      purchaseOrderId = selectedPo.id;
    } else {
      const valid = manualLines.filter((l) => l.itemId && l.qty);
      if (!valid.length) { setError("Add at least one line with an item and quantity."); return; }
      if (!poReference.trim()) { setError("A purchase order reference is required for every receipt."); return; }
      lines = valid.map((l) => ({ itemId: l.itemId, qty: Number(l.qty), unitCost: Number(l.unitCost || 0), batchNo: l.batchNo || undefined }));
      poRefValue = poReference.trim();
    }

    setSaving(true);
    try {
      await api.createGoodsReceipt({
        warehouseId,
        poReference: poRefValue,
        purchaseOrderId,
        supplierId: selectedPo?.supplierId,
        receivedDate: receivedDate || undefined,
        lines
      });
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to post goods receipt.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ width: 860, maxWidth: "95vw" }}>
        <div className="modal-head">
          <h3>Goods Receipt</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}

          {refsLoaded && receivablePos.length > 0 && (
            <div className="tabs" style={{ marginBottom: 12 }}>
              <span className={`tab ${mode === "po" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setMode("po")}>Against Purchase Order</span>
              <span className={`tab ${mode === "manual" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setMode("manual")}>Manual Receipt</span>
            </div>
          )}

          {mode === "po" ? (
            <>
              <div className="row2">
                <div className="field"><label>Purchase Order *</label>
                  <select value={poId} onChange={(e) => setPoId(e.target.value)} required>
                    <option value="">Select a purchase order…</option>
                    {receivablePos.map((p) => (
                      <option key={p.id} value={p.id}>{p.poNumber} — {supplierMap.get(p.supplierId)?.name ?? "Unknown supplier"} ({p.status})</option>
                    ))}
                  </select>
                  {refsLoaded && receivablePos.length === 0 && (
                    <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>No purchase orders are awaiting receipt — use Manual Receipt below.</div>
                  )}
                </div>
                <div className="field"><label>Warehouse *</label>
                  <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                    {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                  </select>
                </div>
              </div>
              <div className="field"><label>Received Date</label>
                <input type="date" value={receivedDate} onChange={(e) => setReceivedDate(e.target.value)} />
              </div>

              {selectedPo && (
                <>
                  <div className="muted" style={{ fontSize: 13, margin: "4px 0 10px" }}>
                    Ordered {selectedPo.orderDate ? formatDate(selectedPo.orderDate) : "—"} · Expected {selectedPo.expectedDate ? formatDate(selectedPo.expectedDate) : "—"} · PO Total {selectedPo.currencyCode ?? "INR"} {money(selectedPo.totalAmount)}
                  </div>

                  {rows.length === 0 ? (
                    <div className="empty">All items on this purchase order have already been received.</div>
                  ) : (
                    <div className="table-wrap">
                      <table>
                        <thead>
                          <tr><th>Item</th><th>Ordered</th><th>Received</th><th>Remaining</th><th>Receiving Qty</th><th>Unit Cost</th><th>Batch #</th><th>Amount</th></tr>
                        </thead>
                        <tbody>
                          {rows.map((r, i) => {
                            const it = itemMap.get(r.itemId);
                            const uom = uomCode(r);
                            const amount = (Number(r.receiveQty) || 0) * (Number(r.unitCost) || 0);
                            return (
                              <tr key={r.lineId}>
                                <td>
                                  <div className="cell-title">{it?.name ?? r.itemId}</div>
                                  <div className="cell-code">{it?.code}</div>
                                </td>
                                <td>{r.orderedQty} {uom}</td>
                                <td>{r.receivedQty} {uom}</td>
                                <td>{r.remainingQty} {uom}</td>
                                <td><input type="number" step="0.01" min="0" style={{ width: 90 }} value={r.receiveQty} onChange={(e) => setRow(i, "receiveQty", e.target.value)} /></td>
                                <td><input type="number" step="0.01" min="0" style={{ width: 90 }} value={r.unitCost} onChange={(e) => setRow(i, "unitCost", e.target.value)} /></td>
                                <td><input placeholder="Batch#" style={{ width: 90 }} value={r.batchNo} onChange={(e) => setRow(i, "batchNo", e.target.value)} /></td>
                                <td>₹{money(amount)}</td>
                              </tr>
                            );
                          })}
                        </tbody>
                        <tfoot>
                          <tr>
                            <td colSpan={7} style={{ textAlign: "right", fontWeight: 600 }}>Total</td>
                            <td style={{ fontWeight: 600 }}>₹{money(poTotal)}</td>
                          </tr>
                        </tfoot>
                      </table>
                    </div>
                  )}
                </>
              )}
            </>
          ) : (
            <>
              <div className="row2">
                <div className="field"><label>Warehouse *</label>
                  <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                    {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                  </select>
                </div>
                <div className="field"><label>Purchase Order No. *</label>
                  <input value={poReference} onChange={(e) => setPoReference(e.target.value)} required placeholder="PO-2026-0001" />
                </div>
              </div>
              <div className="field"><label>Received Date</label>
                <input type="date" value={receivedDate} onChange={(e) => setReceivedDate(e.target.value)} />
              </div>

              <div className="table-wrap">
                <table>
                  <thead><tr><th>Item</th><th>Qty</th><th>Unit Cost</th><th>Batch #</th><th>Amount</th><th /></tr></thead>
                  <tbody>
                    {manualLines.map((l, i) => {
                      const amount = (Number(l.qty) || 0) * (Number(l.unitCost) || 0);
                      return (
                        <tr key={i}>
                          <td>
                            <select value={l.itemId} onChange={(e) => setManualLine(i, "itemId", e.target.value)}>
                              <option value="">Select item…</option>
                              {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
                            </select>
                          </td>
                          <td><input type="number" step="0.01" style={{ width: 80 }} value={l.qty} onChange={(e) => setManualLine(i, "qty", e.target.value)} /></td>
                          <td><input type="number" step="0.01" style={{ width: 90 }} value={l.unitCost} onChange={(e) => setManualLine(i, "unitCost", e.target.value)} /></td>
                          <td><input placeholder="Batch#" style={{ width: 90 }} value={l.batchNo} onChange={(e) => setManualLine(i, "batchNo", e.target.value)} /></td>
                          <td>₹{money(amount)}</td>
                          <td><button type="button" className="icon-btn" onClick={() => removeManualLine(i)} disabled={manualLines.length === 1}>✕</button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                  <tfoot>
                    <tr>
                      <td colSpan={4} style={{ textAlign: "right", fontWeight: 600 }}>Total</td>
                      <td style={{ fontWeight: 600 }}>₹{money(manualTotal)}</td>
                      <td />
                    </tr>
                  </tfoot>
                </table>
              </div>
              <button type="button" className="btn btn-sm" style={{ marginTop: 8 }} onClick={addManualLine}>+ Add line</button>
            </>
          )}
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Post Goods Receipt"}</button>
        </div>
      </form>
    </div>
  );
}


function TxnModal(props: { kind: TxnKind; items: InventoryItem[]; warehouses: Warehouse[]; onClose: () => void; onSaved: () => void }) {
  const { kind } = props;
  const title = kind === "issue" ? "Material Issue" : kind === "transfer" ? "Stock Transfer" : "Stock Adjustment";
  const [warehouseId, setWarehouseId] = useState(props.warehouses[0]?.id ?? "");
  const [toWarehouseId, setToWarehouseId] = useState(props.warehouses[1]?.id ?? props.warehouses[0]?.id ?? "");
  const [reasonCode, setReasonCode] = useState("DAMAGE");
  const [transportId, setTransportId] = useState("");
  const [lines, setLines] = useState<LineRow[]>([{ itemId: "", qty: "", unitCost: "", batchNo: "" }]);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const setLine = (i: number, k: keyof LineRow, v: string) =>
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, [k]: v } : l)));
  const addLine = () => setLines((ls) => [...ls, { itemId: "", qty: "", unitCost: "", batchNo: "" }]);
  const removeLine = (i: number) => setLines((ls) => ls.filter((_, idx) => idx !== i));

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    const valid = lines.filter((l) => l.itemId && l.qty);
    if (!valid.length) { setError("Add at least one line with an item and quantity."); return; }
    if (kind === "transfer" && !transportId.trim()) { setError("A transport ID is required for every transfer."); return; }
    setSaving(true);
    try {
      if (kind === "issue") {
        await api.createMaterialIssue({ warehouseId, lines: valid.map((l) => ({ itemId: l.itemId, qty: Number(l.qty) })) });
      } else if (kind === "transfer") {
        if (warehouseId === toWarehouseId) { setError("Source and destination warehouses must differ."); setSaving(false); return; }
        await api.createStockTransfer({ fromWarehouseId: warehouseId, toWarehouseId, transportId: transportId.trim(), lines: valid.map((l) => ({ itemId: l.itemId, qty: Number(l.qty) })) });
      } else {
        await api.createStockAdjustment({ warehouseId, reasonCode, lines: valid.map((l) => ({ itemId: l.itemId, qtyDelta: Number(l.qty), unitCost: l.unitCost ? Number(l.unitCost) : undefined })) });
      }
      props.onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to post transaction.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit} style={{ maxWidth: 640 }}>
        <div className="modal-head">
          <h3>{title}</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          <div className="row2">
            <div className="field"><label>{kind === "transfer" ? "From Warehouse *" : "Warehouse *"}</label>
              <select value={warehouseId} onChange={(e) => setWarehouseId(e.target.value)} required>
                {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
              </select>
            </div>
            {kind === "transfer" && (
              <div className="field"><label>To Warehouse *</label>
                <select value={toWarehouseId} onChange={(e) => setToWarehouseId(e.target.value)} required>
                  {props.warehouses.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                </select>
              </div>
            )}
            {kind === "adjust" && (
              <div className="field"><label>Reason *</label>
                <select value={reasonCode} onChange={(e) => setReasonCode(e.target.value)}>
                  <option value="DAMAGE">Damage</option>
                  <option value="WASTAGE">Wastage</option>
                  <option value="COUNT_CORRECTION">Count Correction</option>
                  <option value="OPENING_BALANCE">Opening Balance</option>
                </select>
              </div>
            )}
          </div>

          {kind === "transfer" && (
            <div className="field"><label>Transport ID *</label>
              <input value={transportId} onChange={(e) => setTransportId(e.target.value)} required placeholder="Vehicle / LR / e-way bill no." />
            </div>
          )}

          <label style={{ fontSize: 13, fontWeight: 600 }}>Lines</label>
          {lines.map((l, i) => (
            <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr 90px 28px", gap: 8, alignItems: "center", marginTop: 6 }}>
              <select value={l.itemId} onChange={(e) => setLine(i, "itemId", e.target.value)}>
                <option value="">Select item…</option>
                {props.items.map((it) => <option key={it.id} value={it.id}>{it.code} — {it.name}</option>)}
              </select>
              <input placeholder={kind === "adjust" ? "±Qty" : "Qty"} type="number" step="0.01" value={l.qty} onChange={(e) => setLine(i, "qty", e.target.value)} />
              {kind === "adjust" && <input placeholder="Cost" type="number" step="0.01" value={l.unitCost} onChange={(e) => setLine(i, "unitCost", e.target.value)} />}
              <button type="button" className="icon-btn" onClick={() => removeLine(i)} disabled={lines.length === 1}>✕</button>
            </div>
          ))}
          <button type="button" className="btn btn-sm" style={{ marginTop: 8 }} onClick={addLine}>+ Add line</button>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : `Post ${title}`}</button>
        </div>
      </form>
    </div>
  );
}
