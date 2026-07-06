"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import { apiFetch, setAccessToken, tryRefresh } from "./api";

export type User = { id: string; email: string; role: string };
type LoginResponse = { accessToken: string; expiresIn: number; user: User };
type MeResponse = User & { organizationId: string };

// --- API calls (auth endpoints never auto-refresh) ---

async function apiLogin(email: string, password: string): Promise<User> {
  const data = await apiFetch<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
    retryOn401: false,
  });
  setAccessToken(data.accessToken);
  return data.user;
}

async function apiRegister(
  organizationName: string,
  email: string,
  password: string,
): Promise<User> {
  const data = await apiFetch<LoginResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ organizationName, email, password }),
    retryOn401: false,
  });
  setAccessToken(data.accessToken);
  return data.user;
}

async function apiLogout(): Promise<void> {
  try {
    await apiFetch("/api/auth/logout", { method: "POST", retryOn401: false });
  } finally {
    setAccessToken(null);
  }
}

async function apiMe(): Promise<MeResponse> {
  return apiFetch<MeResponse>("/api/auth/me");
}

// --- Auth context ---

type AuthStatus = "loading" | "authenticated" | "guest";

type AuthContextValue = {
  user: User | null;
  status: AuthStatus;
  login: (email: string, password: string) => Promise<void>;
  register: (
    organizationName: string,
    email: string,
    password: string,
  ) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");

  // Bootstrap on load: the access token is gone after a refresh, so try the
  // refresh cookie once and rehydrate the session from /me if it works.
  useEffect(() => {
    let active = true;
    (async () => {
      const refreshed = await tryRefresh();
      if (!active) return;
      if (!refreshed) {
        setStatus("guest");
        return;
      }
      try {
        const me = await apiMe();
        if (!active) return;
        setUser({ id: me.id, email: me.email, role: me.role });
        setStatus("authenticated");
      } catch {
        if (active) setStatus("guest");
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const u = await apiLogin(email, password);
    setUser(u);
    setStatus("authenticated");
  }, []);

  const register = useCallback(
    async (organizationName: string, email: string, password: string) => {
      const u = await apiRegister(organizationName, email, password);
      setUser(u);
      setStatus("authenticated");
    },
    [],
  );

  const logout = useCallback(async () => {
    await apiLogout();
    setUser(null);
    setStatus("guest");
  }, []);

  const value = useMemo(
    () => ({ user, status, login, register, logout }),
    [user, status, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within <AuthProvider>");
  return ctx;
}
