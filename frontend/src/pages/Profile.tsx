import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

export default function Profile() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [department, setDepartment] = useState<string>("—");

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
    </>
  );
}
