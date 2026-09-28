import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export default function AccessRoute({ children, anyPermission = [], anyRole = [] }: {
  children: ReactNode;
  anyPermission?: string[];
  anyRole?: string[];
}) {
  const { user } = useAuth();
  const location = useLocation();
  const hasPermission = anyPermission.length === 0 || anyPermission.some((permission) => user?.permissions.includes(permission));
  const hasRole = anyRole.length === 0 || anyRole.some((role) => user?.roles.includes(role));

  if (!hasPermission || !hasRole) {
    return <Navigate to="/dashboard" replace state={{ deniedPath: location.pathname }} />;
  }
  return <>{children}</>;
}