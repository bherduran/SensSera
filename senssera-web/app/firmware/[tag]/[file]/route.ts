import { FIRMWARE_FILES, FIRMWARE_TAG } from "@/lib/firmware";

// Same-origin proxy for the firmware releases CI publishes: GitHub's release downloads send no
// CORS headers, so the install page can't fetch them directly. Only the latest manifest and the
// four images of a fw-<sha> release are served; anything else is a 404.
const RELEASES = "https://github.com/bherduran/SensSera/releases";
const IMAGES: readonly string[] = FIRMWARE_FILES;

export async function GET(_request: Request, { params }: { params: Promise<{ tag: string; file: string }> }) {
  const { tag, file } = await params;
  const latest = tag === "latest" && file === "manifest.json";
  const pinned = FIRMWARE_TAG.test(tag) && (file === "manifest.json" || IMAGES.includes(file));
  if (!latest && !pinned) return new Response("Not found", { status: 404 });

  const upstream = await fetch(
    latest ? `${RELEASES}/latest/download/manifest.json` : `${RELEASES}/download/${tag}/${file}`,
    { cache: "no-store" },
  );
  if (!upstream.ok) {
    return new Response("Firmware not available", { status: upstream.status === 404 ? 404 : 502 });
  }

  return new Response(upstream.body, {
    headers: {
      "Content-Type": file.endsWith(".json") ? "application/json" : "application/octet-stream",
      // A tagged release never changes; "latest" moves with every firmware merge.
      "Cache-Control": latest ? "public, max-age=0, s-maxage=60" : "public, max-age=31536000, immutable",
    },
  });
}
