import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Self-contained server bundle for the Docker image (no node_modules copy needed).
  output: "standalone",
};

export default nextConfig;
