import { z } from "zod";

// Board firmware published by CI as a GitHub release (.github/workflows/firmware.yml).
// The browser can't fetch release assets directly (no CORS headers), so app/firmware/ proxies them.

export const FIRMWARE_FILES = ["bootloader.bin", "partitions.bin", "boot_app0.bin", "firmware.bin"] as const;
export type FirmwareFile = (typeof FIRMWARE_FILES)[number];

export const FIRMWARE_TAG = /^fw-[0-9a-f]{8}$/;

// NVS (Wi-Fi, token, calibration) lives at 0x9000–0xE000 in the default_16MB partition table.
// A part that reaches into it would wipe the board's settings, so the manifest is rejected.
const NVS_START = 0x9000;
const NVS_END = 0xe000;

const manifestSchema = z.object({
  version: z.string().min(1).max(50),
  tag: z.string().regex(FIRMWARE_TAG),
  chipFamily: z.literal("ESP32-S3"),
  parts: z
    .array(z.object({ file: z.enum(FIRMWARE_FILES), offset: z.number().int().nonnegative() }))
    .length(FIRMWARE_FILES.length),
});
export type FirmwareManifest = z.infer<typeof manifestSchema>;

export function parseManifest(json: unknown): FirmwareManifest {
  return manifestSchema.parse(json);
}

/** True when a part written at `offset` with `size` bytes would overwrite NVS. */
export function overlapsNvs(offset: number, size: number): boolean {
  return offset < NVS_END && offset + size > NVS_START;
}

// --- Serial console commands (firmware/src/console.cpp) ---------------------------------------

// The console reads lines of at most 255 characters and splits on spaces; double quotes keep
// spaces together but can't be escaped, so a value containing `"` can't be sent at all.
const CONSOLE_LINE_MAX = 255;
const NODE_TOKEN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.[A-Za-z0-9+/]{43}=$/i;

type CommandResult = { ok: true; command: string } | { ok: false; error: string };

const utf8Length = (s: string) => new TextEncoder().encode(s).length;

function hasUnsendable(s: string) {
  return s.includes('"') || /[\r\n]/.test(s);
}

export function wifiCommand(ssid: string, password: string): CommandResult {
  if (ssid.length === 0) return { ok: false, error: "Ağ adı boş olamaz." };
  if (utf8Length(ssid) > 32) return { ok: false, error: "Ağ adı en fazla 32 karakter olabilir." };
  // WPA2 passphrases are 8–63 characters; an empty one means an open network.
  if (password.length > 0 && (password.length < 8 || password.length > 63)) {
    return { ok: false, error: "Şifre 8–63 karakter olmalı (şifresiz ağ için boş bırakın)." };
  }
  if (hasUnsendable(ssid) || hasUnsendable(password)) {
    return { ok: false, error: 'Ağ adı ve şifre çift tırnak (") içeremez.' };
  }
  const command = password ? `wifi "${ssid}" "${password}"` : `wifi "${ssid}"`;
  return utf8Length(command) > CONSOLE_LINE_MAX
    ? { ok: false, error: "Ağ adı ve şifre çok uzun." }
    : { ok: true, command };
}

export function tokenCommand(token: string): CommandResult {
  const value = token.trim();
  if (!NODE_TOKEN.test(value)) {
    return { ok: false, error: "Token geçersiz. Web uygulamasında kart eklerken gösterilen değerin tamamını yapıştırın." };
  }
  return { ok: true, command: `token ${value}` };
}

export function calibrationCommand(channel: string, dryMv: number, wetMv: number): CommandResult {
  if (!/^soil[1-4]$/.test(channel)) return { ok: false, error: "Kanal soil1–soil4 olmalı." };
  if (!Number.isInteger(dryMv) || !Number.isInteger(wetMv)) {
    return { ok: false, error: "Değerler tam sayı (mV) olmalı." };
  }
  // Same rule the firmware enforces: 0 < wet < dry <= 3300 mV.
  if (wetMv <= 0 || dryMv > 3300 || wetMv >= dryMv) {
    return { ok: false, error: "Islak değer kurudan küçük olmalı (0 < ıslak < kuru ≤ 3300 mV)." };
  }
  return { ok: true, command: `cal ${channel} ${dryMv} ${wetMv}` };
}

/** Pulls a soil channel's millivolts out of a `raw` line, e.g. "soil1   2834 mV  -> 2.1 (...)". */
export function parseRawSoilLine(line: string): { channel: string; mv: number } | null {
  const m = /^(soil[1-4])\s+(\d+)\s+mV\b/.exec(line.trim());
  return m ? { channel: m[1], mv: Number(m[2]) } : null;
}
