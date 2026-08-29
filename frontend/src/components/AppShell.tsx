import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

const navMain = [
  { to: "/dashboard", label: "Dashboard", ico: "▦" },
  { to: "/projects", label: "Projects", ico: "▤" },
  { to: "/requirements", label: "Requirements", ico: "▣" }
];
const navManage = [
  { to: "/clients", label: "Clients", ico: "◑" },
  { to: "/employees", label: "Employees", ico: "☺" }
];
const navCore = [
  { to: "/core", label: "Core", ico: "◈" },
  { to: "/settings", label: "Settings", ico: "⚙" }
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function onLogout() {
    logout();
    navigate("/login");
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-logo">{(user?.organizationName ?? "C").charAt(0).toUpperCase()}</div>
          <div>
            <div className="brand-name">{user?.organizationName ?? "Construction ERP"}</div>
            <div className="brand-ver">Construction ERP</div>
          </div>
        </div>

        <nav className="nav">
          {navMain.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          <div className="nav-label">Management</div>
          {navManage.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          <div className="nav-label">Core</div>
          {navCore.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">Help &amp; Support</div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div className="tabs">
            <span className="tab active">Workspace</span>
            <span className="tab">Reporting</span>
          </div>
          <div className="spacer" />
          <div className="search">
            <span>⌕</span>
            <input placeholder="Search..." />
          </div>
          <div className="icon-btn">🔔</div>
          <div className="avatar" title={user?.email ?? "View profile"} onClick={() => navigate("/profile")} style={{ cursor: "pointer" }}>
            {initials(user?.displayName ?? user?.email)}
          </div>
          <button className="btn btn-sm" onClick={onLogout} title="Sign out" style={{ marginLeft: 4 }}>Sign out</button>
        </header>
        <div className="content">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
