import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, clearToken, clearRefCache, getToken, prefetchReferenceData, setToken, type LoginResponse } from "../api/client";

type AuthUser = LoginResponse["user"];

interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  login: (organizationCode: string, email: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

const USER_KEY = "erp_user";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  });

  useEffect(() => {
    // If token is gone but user cached, clear.
    if (!getToken()) setUser(null);
    // Already signed in on reload: warm the cache so pages load instantly.
    else prefetchReferenceData();
  }, []);

  async function login(organizationCode: string, email: string) {
    const res = await api.login(organizationCode, email);
    clearRefCache(); // drop any cached reference data from a previous session/org
    setToken(res.accessToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    setUser(res.user);
    prefetchReferenceData(); // preload lookups so the first page doesn't fetch them
  }

  function logout() {
    clearRefCache();
    clearToken();
    localStorage.removeItem(USER_KEY);
    setUser(null);
  }

  const value = useMemo<AuthState>(
    () => ({ user, isAuthenticated: !!user && !!getToken(), login, logout }),
    [user]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
