import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./auth/AuthContext";
import ProtectedRoute from "./components/ProtectedRoute";
import AccessRoute from "./components/AccessRoute";
import AppShell from "./components/AppShell";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import Projects from "./pages/Projects";
import ProjectDetail from "./pages/ProjectDetail";
import Requirements from "./pages/Requirements";
import Clients from "./pages/Clients";
import Employees from "./pages/Employees";
import Profile from "./pages/Profile";
import InventoryItems from "./pages/InventoryItems";
import InventoryStock from "./pages/InventoryStock";
import InventoryWarehouses from "./pages/InventoryWarehouses";
import InventorySuppliers from "./pages/InventorySuppliers";
import Production from "./pages/Production";
import Planning from "./pages/Planning";
import InventoryRequests from "./pages/InventoryRequests";
import Shipment from "./pages/Shipment";
import Placeholder from "./pages/Placeholder";

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <AppShell />
              </ProtectedRoute>
            }
          >
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="projects" element={<AccessRoute anyPermission={["project.read"]}><Projects /></AccessRoute>} />
            <Route path="projects/:projectId" element={<AccessRoute anyPermission={["project.read"]}><ProjectDetail /></AccessRoute>} />
            <Route path="requirements" element={<AccessRoute anyPermission={["project.create"]}><Requirements /></AccessRoute>} />
            <Route path="planning" element={<AccessRoute anyPermission={["materialrequirement.create", "materialrequirement.read"]}><Planning /></AccessRoute>} />
            <Route path="clients" element={<AccessRoute anyPermission={["project.read"]}><Clients /></AccessRoute>} />
            <Route path="employees" element={<AccessRoute anyRole={["SUPER_ADMIN", "Administrator", "Employee Manager"]}><Employees /></AccessRoute>} />
            <Route path="inventory/items" element={<AccessRoute anyPermission={["item.read"]}><InventoryItems /></AccessRoute>} />
            <Route path="inventory/stock" element={<AccessRoute anyPermission={["stock.read"]}><InventoryStock /></AccessRoute>} />
            <Route path="inventory/warehouses" element={<AccessRoute anyPermission={["warehouse.read"]}><InventoryWarehouses /></AccessRoute>} />
            <Route path="inventory/suppliers" element={<AccessRoute anyPermission={["supplier.read"]}><InventorySuppliers /></AccessRoute>} />
            <Route path="inventory/production" element={<AccessRoute anyPermission={["bom.read", "workorder.read"]}><Production /></AccessRoute>} />
            <Route path="inventory/requests" element={<AccessRoute anyPermission={["materialrequirement.approve", "materialrequirement.read"]}><InventoryRequests /></AccessRoute>} />
            <Route path="shipment" element={<AccessRoute anyPermission={["transfer.dispatch", "transfer.receive"]}><Shipment /></AccessRoute>} />
            <Route path="profile" element={<Profile />} />
            <Route path="core" element={<AccessRoute anyRole={["SUPER_ADMIN", "Administrator"]}><Placeholder title="Core" /></AccessRoute>} />
            <Route path="settings" element={<AccessRoute anyRole={["SUPER_ADMIN", "Administrator"]}><Placeholder title="Settings" /></AccessRoute>} />
          </Route>
          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
