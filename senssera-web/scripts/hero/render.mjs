// Renders the clay hero scene (scripts/hero/render.html) to public/hero/{light,dark}.webp.
// Usage: npm run hero            (uses the locally installed Google Chrome via playwright-core)
import { createServer } from "node:http";
import { readFile, mkdir } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright-core";
import sharp from "sharp";

const root = fileURLToPath(new URL("../../", import.meta.url));
const outDir = join(root, "public", "hero");
const W = 1600;
const H = 1200;
const BG = { light: "#F3EFE7", dark: "#171512" };
const types = { ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript" };

// Minimal static server rooted at senssera-web/ so the import map can reach node_modules.
const server = createServer(async (req, res) => {
  const path = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname)).replace(/^([/\\])+/, "");
  const file = join(root, path);
  if (!file.startsWith(root)) return res.writeHead(403).end();
  try {
    const body = await readFile(file);
    res.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream" }).end(body);
  } catch {
    res.writeHead(404).end();
  }
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const { port } = server.address();

const browser = await chromium.launch({
  channel: "chrome",
  args: ["--use-angle=metal", "--enable-gpu", "--ignore-gpu-blocklist"],
});
try {
  await mkdir(outDir, { recursive: true });
  // The scene renders at 2x; capture at 2x device pixels, then downsample for crisp edges.
  const page = await browser.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: 2 });
  page.on("pageerror", (e) => console.error("page error:", e.message));
  for (const theme of process.argv.slice(2).length ? process.argv.slice(2) : ["light", "dark"]) {
    await page.goto(`http://127.0.0.1:${port}/scripts/hero/render.html?theme=${theme}&w=${W}&h=${H}`);
    await page.waitForFunction(() => window.__done === true, null, { timeout: 60_000 });
    const png = await page.locator("canvas").screenshot({ type: "png", omitBackground: true });
    const out = join(outDir, `${theme}.webp`);
    const flat = await sharp(png).flatten({ background: BG[theme] }).resize(W).png().toBuffer();
    await sharp(flat).webp({ quality: 88 }).toFile(out);
    console.log("wrote", out);
  }
} finally {
  await browser.close();
  server.close();
}
