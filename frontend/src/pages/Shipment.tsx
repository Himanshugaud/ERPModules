import { useEffect, useState } from "react";
import { api, ApiError, type InventoryDocument, type Warehouse } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDate } from "../lib/ui";

export default function Shipment() {
  const { user } = useAuth();
  const canDispatch = (user?.permissions ?? []).includes("transfer.dispatch");
  const canReceive = (user?.permissions ?? []).includes("transfer.receive");
  const [transfers, setTransfers] = useState<InventoryDocument[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [tab, setTab] = useState<"APPROVED" | "DISPATCHED" | "RECEIVED">("APPROVED");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const warehouseName = (id?: string) => (id ? warehouses.find((w) => w.id === id)?.name ?? "—" : "—");

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const res = await api.stockTransfers({ pageSize: 100 });
      setTransfers(res.data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load shipments.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    api.warehouses().then(setWarehouses).catch(() => setWarehouses([]));
    load();
  }, []);

  const filtered = transfers.filter((t) => t.status === tab);

  async function dispatch(t: InventoryDocument) {
    setBusyId(t.id);
    setError(null);
    try {
      await api.dispatchStockTransfer(t.id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to dispatch the shipment.");
    } finally {
      setBusyId(null);
    }
  }

  async function receive(t: InventoryDocument) {
    setBusyId(t.id);
    setError(null);
    try {
      await api.receiveStockTransfer(t.id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to confirm receipt.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Shipment</h1>
          <div className="page-sub">Dispatch materials arranged by Inventory to their site destination.</div>
        </div>
        <div className="head-actions">
          <div className="tabs">
            <span className={`tab ${tab === "APPROVED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("APPROVED")}>Ready to Dispatch</span>
            <span className={`tab ${tab === "DISPATCHED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("DISPATCHED")}>In Transit</span>
            <span className={`tab ${tab === "RECEIVED" ? "active" : ""}`} style={{ cursor: "pointer" }} onClick={() => setTab("RECEIVED")}>Delivered</span>
          </div>
        </div>
      </div>

      {error && <div className="form-error" style={{ marginBottom: 12 }}>{error}</div>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr><th>Transfer #</th><th>From</th><th>To (Destination)</th><th>Transport</th><th>Lines</th><th>Status</th><th></th></tr>
          </thead>
          <tbody>
            {loading ? (
              [...Array(3)].map((_, i) => (
                <tr key={i}>{[...Array(7)].map((__, j) => <td key={j}><div className="skeleton" style={{ height: 14, width: j === 0 ? 120 : 90 }} /></td>)}</tr>
              ))
            ) : filtered.length === 0 ? (
              <tr><td colSpan={7}><div className="empty">Nothing here right now.</div></td></tr>
            ) : (
              filtered.map((t) => (
                <tr key={t.id}>
                  <td>{t.number}</td>
                  <td>{t.warehouseId ? warehouseName(t.warehouseId) : (t.sourceAddress || "—")}</td>
                  <td>{warehouseName(t.toWarehouseId)}</td>
                  <td>{t.documentDate ? formatDate(t.documentDate) : "—"}</td>
                  <td>{t.lineCount} item{t.lineCount === 1 ? "" : "s"}</td>
                  <td><span className={`badge ${t.status === "APPROVED" ? "blue" : t.status === "DISPATCHED" ? "amber" : t.status === "RECEIVED" ? "green" : "gray"}`}>{t.status}</span></td>
                  <td>
                    {t.status === "APPROVED" && canDispatch && (
                      <button className="btn btn-sm primary" disabled={busyId === t.id} onClick={() => dispatch(t)}>Dispatch</button>
                    )}
                    {t.status === "DISPATCHED" && canReceive && (
                      <button className="btn btn-sm primary" disabled={busyId === t.id} onClick={() => receive(t)}>Confirm Delivery</button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}
