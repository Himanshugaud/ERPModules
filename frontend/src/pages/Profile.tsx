import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

export default function Profile() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [department, setDepartment] = useState<string>("—");
  const [passwordModal, setPasswordModal] = useState(false);

  useEffect(() => {
    if (!user) return;
    (async () => {
      try {
        const [me, depts] = await Promise.all([
          api.user(user.userId),
          api.departments().catch(() => [])
        ]);
        const dept = depts.find((d) => d.id === me.departmentId);
        setDepartment(dept?.name ?? "—");
      } catch {
        setDepartment("—");
      }
    })();
  }, [user]);

  function onLogout() {
    logout();
    navigate("/login");
  }

  if (!user) return <div className="muted">Not signed in.</div>;

  const rows: { label: string; value: string }[] = [
    { label: "Full Name", value: user.displayName ?? "—" },
    { label: "Email", value: user.email ?? "—" },
    { label: "Organization", value: user.organizationName ?? "—" },
    { label: "Department", value: department },
    { label: "User ID", value: user.userId }
  ];

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">My Profile</h1>
          <div className="page-sub">Your account details and access.</div>
        </div>
        <div className="head-actions">
          <button className="btn danger" onClick={onLogout}>Sign out</button>
        </div>
      </div>

      <div className="card">
        <div className="card-pad" style={{ display: "flex", gap: 18, alignItems: "center", borderBottom: "1px solid var(--border)" }}>
          <div className="avatar" style={{ width: 64, height: 64, fontSize: 24 }}>{initials(user.displayName ?? user.email)}</div>
          <div>
            <div style={{ fontSize: 20, fontWeight: 600 }}>{user.displayName ?? user.email}</div>
            <div className="muted">{(user.roles ?? []).join(" · ") || "No role"}</div>
          </div>
        </div>

        <div className="card-pad">
          <div className="meta-grid">
            {rows.map((r) => (
              <div key={r.label} className="meta-item">
                <div className="meta-label">{r.label}</div>
                <div className="meta-value">{r.value}</div>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 18 }}>
        <div className="card-pad" style={{ borderBottom: "1px solid var(--border)" }}>
          <strong>Roles</strong>
        </div>
        <div className="card-pad" style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
          {(user.roles ?? []).length === 0 ? <span className="muted">No roles assigned.</span> :
            (user.roles ?? []).map((r) => <span key={r} className="badge blue">{r}</span>)}
        </div>
      </div>

      <div className="card" style={{ marginTop: 18 }}>
        <div className="card-pad" style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <div>
            <strong>Password</strong>
            <div className="muted">Reset your account password.</div>
          </div>
          <button className="btn" onClick={() => setPasswordModal(true)}>Reset Password</button>
        </div>
      </div>

      {passwordModal && <ChangePasswordModal onClose={() => setPasswordModal(false)} />}
    </>
  );
}

function ChangePasswordModal(props: { onClose: () => void }) {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setMessage(null);
    if (newPassword !== confirmPassword) {
      setError("New passwords do not match.");
      return;
    }
    setSaving(true);
    try {
      await api.changeMyPassword(currentPassword || undefined, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setMessage("Password updated.");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to update password.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={props.onClose}>
      <form className="modal" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <div className="modal-head">
          <h3>Reset Password</h3>
          <button type="button" className="icon-btn" onClick={props.onClose}>✕</button>
        </div>
        <div className="modal-body">
          {error && <div className="form-error">{error}</div>}
          {message && <div className="badge green" style={{ marginBottom: 10 }}>{message}</div>}
          <div className="field">
            <label>Current password</label>
            <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} placeholder="Leave blank if none set yet" autoComplete="current-password" />
          </div>
          <div className="row2">
            <div className="field">
              <label>New password</label>
              <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} minLength={10} required autoComplete="new-password" />
            </div>
            <div className="field">
              <label>Confirm new password</label>
              <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} minLength={10} required autoComplete="new-password" />
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn" onClick={props.onClose}>Cancel</button>
          <button className="btn primary" disabled={saving}>{saving ? <span className="spinner" /> : "Update Password"}</button>
        </div>
      </form>
    </div>
  );
}
