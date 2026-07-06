import { API_URL } from "./config";

// The access token lives ONLY in memory (never localStorage — XSS-safe). The
// refresh token is an httpOnly cookie the browser sends automatically.
let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

// Deduplicate concurrent refreshes so a burst of 401s triggers one refresh call.
let refreshPromise: Promise<boolean> | null = null;

async function rawRefresh(): Promise<boolean> {
  try {
    const res = await fetch(`${API_URL}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
    });
    if (!res.ok) return false;
    const data = (await res.json()) as { accessToken: string };
    accessToken = data.accessToken;
    return true;
  } catch {
    return false;
  }
}

export function tryRefresh(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = rawRefresh().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public status: number,
    public problem?: ProblemDetails,
  ) {
    super(problem?.detail ?? problem?.title ?? `Request failed (${status})`);
    this.name = "ApiError";
  }
}

type ApiOptions = RequestInit & { retryOn401?: boolean };

export async function apiFetch<T>(
  path: string,
  options: ApiOptions = {},
): Promise<T> {
  const { retryOn401 = true, ...init } = options;

  const headers = new Headers(init.headers);
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  if (init.body && !headers.has("Content-Type"))
    headers.set("Content-Type", "application/json");

  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    headers,
    credentials: "include",
  });

  if (res.status === 401 && retryOn401) {
    const refreshed = await tryRefresh();
    if (refreshed) return apiFetch<T>(path, { ...options, retryOn401: false });
  }

  if (!res.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await res.json()) as ProblemDetails;
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(res.status, problem);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
