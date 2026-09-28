import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { initials } from "../lib/ui";

type NavItem = { to: string; label: string; ico: string; permissions?: string[]; roles?: string[] };

const navMain: NavItem[] = [
  { to: "/dashboard", label: "Dashboard", ico: "▦" },
  { to: "/projects", label: "Projects", ico: "▤", permissions: ["project.read"] },
  { to: "/requirements", label: "Requirements", ico: "▣", permissions: ["project.create"] },
  { to: "/planning", label: "Planning", ico: "⚑", permissions: ["materialrequirement.create", "materialrequirement.read"] }
];
const navManage: NavItem[] = [
  { to: "/clients", label: "Clients", ico: "◑", permissions: ["project.read"] },
  { to: "/employees", label: "Employees", ico: "☺", roles: ["SUPER_ADMIN", "Administrator", "Employee Manager"] }
];
const navInventory: NavItem[] = [
  { to: "/inventory/items", label: "Items", ico: "❖", permissions: ["item.read"] },
  { to: "/inventory/stock", label: "Stock", ico: "▥", permissions: ["stock.read"] },
  { to: "/inventory/production", label: "Production", ico: "⚒", permissions: ["bom.read", "workorder.read"] },
  { to: "/inventory/warehouses", label: "Warehouses", ico: "🏬", permissions: ["warehouse.read"] },
  { to: "/inventory/suppliers", label: "Suppliers", ico: "🚚", permissions: ["supplier.read"] },
  { to: "/inventory/requests", label: "Material Requests", ico: "📋", permissions: ["materialrequirement.approve", "materialrequirement.read"] }
];
const navFulfillment: NavItem[] = [
  { to: "/shipment", label: "Shipment", ico: "🚚", permissions: ["transfer.dispatch", "transfer.receive"] }
];
const navCore: NavItem[] = [
  { to: "/core", label: "Core", ico: "◈", roles: ["SUPER_ADMIN", "Administrator"] },
  { to: "/settings", label: "Settings", ico: "⚙", roles: ["SUPER_ADMIN", "Administrator"] }
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const canSee = (item: NavItem) =>
    (!item.permissions || item.permissions.some((permission) => user?.permissions.includes(permission))) &&
    (!item.roles || item.roles.some((role) => user?.roles.includes(role)));
  const visibleMain = navMain.filter(canSee);
  const visibleManage = navManage.filter(canSee);
  const visibleInventory = navInventory.filter(canSee);
  const visibleFulfillment = navFulfillment.filter(canSee);
  const visibleCore = navCore.filter(canSee);

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
          {visibleMain.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          {visibleManage.length > 0 && <div className="nav-label">Management</div>}
          {visibleManage.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          {visibleInventory.length > 0 && <div className="nav-label">Inventory</div>}
          {visibleInventory.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          {visibleFulfillment.length > 0 && <div className="nav-label">Fulfillment</div>}
          {visibleFulfillment.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => `nav-item ${isActive ? "active" : ""}`}>
              <span className="ico">{n.ico}</span>
              {n.label}
            </NavLink>
          ))}
          {visibleCore.length > 0 && <div className="nav-label">Core</div>}
          {visibleCore.map((n) => (
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
