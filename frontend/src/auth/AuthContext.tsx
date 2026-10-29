import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, clearToken, clearRefCache, getToken, getTokenExpiry, prefetchReferenceData, SESSION_EXPIRED_EVENT, setToken, type LoginResponse } from "../api/client";

type AuthUser = LoginResponse["user"];

interface AuthState {
  user: AuthUser | null;
  isAuthenticated: boolean;
  login: (organizationCode: string, email: string, password?: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

const USER_KEY = "erp_user";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    try {
      const raw = localStorage.getItem(USER_KEY);
      if (getToken() && raw) return JSON.parse(raw) as AuthUser;
    } catch {}
    clearToken();
    localStorage.removeItem(USER_KEY);
    return null;
  });

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    function checkSession() {
      clearTimeout(timer);
      if (!getToken()) {
        logout();
        return;
      }
      timer = setTimeout(checkSession, Math.min(getTokenExpiry(getToken()) - Date.now(), 2147483647));
    }
    window.addEventListener(SESSION_EXPIRED_EVENT, logout);
    window.addEventListener("focus", checkSession);
    document.addEventListener("visibilitychange", checkSession);
    if (user) {
      checkSession();
      prefetchReferenceData(user.permissions);
    }
    return () => {
      clearTimeout(timer);
      window.removeEventListener(SESSION_EXPIRED_EVENT, logout);
      window.removeEventListener("focus", checkSession);
      document.removeEventListener("visibilitychange", checkSession);
    };
  }, [user]);

  async function login(organizationCode: string, email: string, password?: string) {
    const res = await api.login(organizationCode, email, password);
    clearRefCache(); // drop any cached reference data from a previous session/org
    setToken(res.accessToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    setUser(res.user);
    prefetchReferenceData(res.user.permissions); // preload only lookups the user may access
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
