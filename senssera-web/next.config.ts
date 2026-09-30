import type { NextConfig } from "next";

// In production (Vercel) the browser only ever talks to the frontend's own origin: /api and /hubs
// are proxied to the Azure API. That keeps the refresh-token cookie first-party (browsers block
// third-party cookies between vercel.app and azurewebsites.net) and removes the need for CORS.
// Locally and in docker compose API_ORIGIN is unset and the client calls NEXT_PUBLIC_API_URL directly.
const apiOrigin = process.env.API_ORIGIN;

const nextConfig: NextConfig = {
  // Self-contained server bundle for the Docker image (no node_modules copy needed).
  output: "standalone",
  async rewrites() {
    if (!apiOrigin) return [];
    return [
      { source: "/api/:path*", destination: `${apiOrigin}/api/:path*` },
      { source: "/hubs/:path*", destination: `${apiOrigin}/hubs/:path*` },
    ];
  },
};

export default nextConfig;
