import { describe, expect, it } from "vitest";
import {
  calibrationCommand,
  overlapsNvs,
  parseManifest,
  parseRawSoilLine,
  tokenCommand,
  wifiCommand,
} from "./firmware";

// Shape CI writes (firmware.yml "Collect images").
const manifest = {
  version: "0.1.0+b8cc1f4f",
  tag: "fw-b8cc1f4f",
  chipFamily: "ESP32-S3",
  parts: [
    { file: "bootloader.bin", offset: 0 },
    { file: "partitions.bin", offset: 32768 },
    { file: "boot_app0.bin", offset: 57344 },
    { file: "firmware.bin", offset: 65536 },
  ],
};

describe("parseManifest", () => {
  it("accepts the manifest CI publishes", () => {
    expect(parseManifest(manifest).tag).toBe("fw-b8cc1f4f");
  });

  it("rejects an unknown file, so the proxy can't be pointed at other assets", () => {
    const bad = { ...manifest, parts: [...manifest.parts.slice(0, 3), { file: "spiffs.bin", offset: 65536 }] };
    expect(() => parseManifest(bad)).toThrow();
  });

  it("rejects a tag that isn't fw-<sha8>", () => {
    expect(() => parseManifest({ ...manifest, tag: "../../etc" })).toThrow();
  });
});

describe("overlapsNvs", () => {
  it("leaves NVS alone for the real layout", () => {
    // Sizes from the fw-b8cc1f4f release.
    expect(overlapsNvs(0x0, 15104)).toBe(false);
    expect(overlapsNvs(0x8000, 3072)).toBe(false);
    expect(overlapsNvs(0xe000, 8192)).toBe(false);
    expect(overlapsNvs(0x10000, 922576)).toBe(false);
  });

  it("flags a part that runs into 0x9000–0xE000", () => {
    expect(overlapsNvs(0x8000, 0x1001)).toBe(true);
    expect(overlapsNvs(0x0, 0x10000)).toBe(true);
  });
});

describe("wifiCommand", () => {
  it("quotes names with spaces", () => {
    expect(wifiCommand("Ev Agi 2.4", "s3cretpass")).toEqual({ ok: true, command: 'wifi "Ev Agi 2.4" "s3cretpass"' });
  });

  it("sends an open network without a password argument", () => {
    expect(wifiCommand("Cafe", "")).toEqual({ ok: true, command: 'wifi "Cafe"' });
  });

  it.each([
    ["", "password1"],
    ['Ev "Agi"', "password1"],
    ["Ev", 'pass"word1'],
    ["Ev", "short"],
    ["a".repeat(33), "password1"],
  ])("rejects ssid %j / password %j", (ssid, password) => {
    expect(wifiCommand(ssid, password).ok).toBe(false);
  });
});

describe("tokenCommand", () => {
  const token = "0b9f6c1e-3a54-4f0e-9d2b-6f0f1c2a3b4c.q1w2e3r4t5y6u7i8o9p0a1s2d3f4g5h6j7k8l9z0x1c=";

  it("trims what was pasted", () => {
    expect(tokenCommand(`  ${token}\n`)).toEqual({ ok: true, command: `token ${token}` });
  });

  it("rejects a token without its secret part", () => {
    expect(tokenCommand("0b9f6c1e-3a54-4f0e-9d2b-6f0f1c2a3b4c").ok).toBe(false);
  });
});

describe("calibrationCommand", () => {
  it("builds the console command", () => {
    expect(calibrationCommand("soil1", 2850, 1290)).toEqual({ ok: true, command: "cal soil1 2850 1290" });
  });

  it.each([
    ["soil1", 1290, 2850],
    ["soil1", 3400, 1290],
    ["air_temp", 2850, 1290],
  ])("rejects %s %d/%d", (channel, dry, wet) => {
    expect(calibrationCommand(channel, dry, wet).ok).toBe(false);
  });
});

describe("parseRawSoilLine", () => {
  it("reads the millivolts of a soil line", () => {
    // printf("%-13s %6.0f mV  -> %s (cal dry %u / wet %u)")
    expect(parseRawSoilLine("soil1           2834 mV  -> 0.0 (cal dry 2800 / wet 1200)")).toEqual({
      channel: "soil1",
      mv: 2834,
    });
  });

  it("ignores other lines", () => {
    expect(parseRawSoilLine("air_temp      23.41")).toBeNull();
  });
});
