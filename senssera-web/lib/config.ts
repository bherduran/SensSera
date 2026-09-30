// Where the browser sends API and SignalR requests.
// - NEXT_PUBLIC_API_URL wins when set (local dev, docker compose).
// - On Vercel (NEXT_PUBLIC_VERCEL_ENV is injected automatically) requests stay on the frontend's own
//   origin and next.config rewrites proxy them to the Azure API, keeping the auth cookie first-party.
export const API_URL =
  process.env.NEXT_PUBLIC_API_URL ?? (process.env.NEXT_PUBLIC_VERCEL_ENV ? "" : "http://localhost:5010");
