// Browser-only: talks to the board over Web Serial (Chrome / Edge on desktop).
import { overlapsNvs, parseManifest, type FirmwareManifest } from "./firmware";

export function serialSupported(): boolean {
  return typeof navigator !== "undefined" && "serial" in navigator;
}

// Espressif's native USB (the port marked "USB") and the WCH bridge on the "COM" port.
const BOARD_FILTERS: SerialPortFilter[] = [{ usbVendorId: 0x303a }, { usbVendorId: 0x1a86 }];

export function pickPort(): Promise<SerialPort> {
  return navigator.serial.requestPort({ filters: BOARD_FILTERS });
}

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

// --- Install ----------------------------------------------------------------------------------

export type FirmwareImage = { manifest: FirmwareManifest; files: { data: Uint8Array; address: number }[] };

/** Downloads the latest release through the app/firmware proxy. */
export async function downloadFirmware(): Promise<FirmwareImage> {
  const res = await fetch("/firmware/latest/manifest.json", { cache: "no-store" });
  if (!res.ok) throw new Error(`Firmware bilgisi alınamadı (${res.status}).`);
  const manifest = parseManifest(await res.json());

  const files = await Promise.all(
    manifest.parts.map(async (part) => {
      const r = await fetch(`/firmware/${manifest.tag}/${part.file}`);
      if (!r.ok) throw new Error(`${part.file} indirilemedi (${r.status}).`);
      const data = new Uint8Array(await r.arrayBuffer());
      if (overlapsNvs(part.offset, data.length)) {
        throw new Error(`${part.file} ayarların durduğu alana (NVS) yazacaktı; yükleme durduruldu.`);
      }
      return { data, address: part.offset };
    }),
  );
  return { manifest, files };
}

/**
 * Writes the images at their offsets. Never erases: ESP Web Tools' install flow erases the whole
 * flash for boards without Improv Serial, which would wipe the Wi-Fi and token stored in NVS.
 * esptool only erases the sectors it writes, and none of them overlap NVS (checked above).
 */
export async function flashFirmware(
  port: SerialPort,
  image: FirmwareImage,
  onProgress: (percent: number) => void,
  onLog: (line: string) => void,
): Promise<void> {
  // esptool-js touches `window` at import time, so it is loaded only when a flash starts.
  const { ESPLoader, Transport } = await import("esptool-js");
  const transport = new Transport(port, false);
  const loader = new ESPLoader({
    transport,
    baudrate: 115200,
    terminal: { clean: () => {}, writeLine: onLog, write: onLog },
  });

  try {
    await loader.main();
    if (loader.chip.CHIP_NAME !== image.manifest.chipFamily) {
      throw new Error(`Bu kart ${loader.chip.CHIP_NAME}; firmware ${image.manifest.chipFamily} için.`);
    }

    const totalBytes = image.files.reduce((sum, f) => sum + f.data.length, 0);
    let doneBytes = 0;
    await loader.writeFlash({
      fileArray: image.files,
      flashMode: "keep",
      flashFreq: "keep",
      flashSize: "keep",
      eraseAll: false,
      compress: true,
      // `written`/`total` count compressed bytes of the current file.
      reportProgress: (fileIndex, written, total) => {
        const fileBytes = image.files[fileIndex].data.length;
        const current = doneBytes + (written / total) * fileBytes;
        onProgress(Math.floor((current / totalBytes) * 100));
        if (written === total) doneBytes += fileBytes;
      },
    });
    onProgress(100);

    // Same release sequence ESP Web Tools uses: hold reset, then let esptool restart the chip.
    await transport.setRTS(true);
    await sleep(100);
    await loader.after();
  } finally {
    await transport.disconnect();
  }
}

/**
 * Opens the console just long enough to see the freshly written firmware answer. Without this a
 * board that stayed in the bootloader looked like a successful install.
 */
export async function confirmFirmware(port: SerialPort, expectedVersion: string): Promise<void> {
  const board = new BoardConsole(port, () => {}, () => {});
  await board.open();
  let running: string;
  try {
    running = await board.ensureFirmware({ booting: true });
  } finally {
    await board.close();
  }
  if (running !== expectedVersion) {
    throw new Error(`Kart ${running} sürümüyle açıldı, ${expectedVersion} bekleniyordu. Yüklemeyi tekrar deneyin.`);
  }
}

// --- Control lines ----------------------------------------------------------------------------

// On the ESP32-S3's native USB the two modem-control lines are the reset circuit: RTS holds the
// chip in reset, and DTR asserted while RTS is released boots it into the ROM bootloader. Chrome
// asserts both when it opens a port and drops both when it closes it. Measured on the pilot board,
// that closing edge reset it on every disconnect (emptying its RAM reading buffer), and the board
// has ended up in the bootloader, where it ignores the console.

/** Lowers RTS, then DTR. In this order neither edge resets the board; the other order can. */
async function releaseControlLines(port: SerialPort): Promise<void> {
  await port.setSignals({ requestToSend: false });
  await port.setSignals({ dataTerminalReady: false });
}

/**
 * Restarts the chip into the firmware, also from the bootloader. The DTR pulse comes first: an RTS
 * pulse alone was seen to land in the bootloader again. Windows' usbser.sys only sends a change on
 * one line along with a call for the other, hence the pairs.
 */
async function restartIntoFirmware(port: SerialPort): Promise<void> {
  await port.setSignals({ dataTerminalReady: true });
  await port.setSignals({ requestToSend: false });
  await sleep(60);
  await port.setSignals({ dataTerminalReady: false });
  await port.setSignals({ requestToSend: false });
  await sleep(60);
  await port.setSignals({ requestToSend: true });
  await port.setSignals({ dataTerminalReady: false });
  await sleep(120);
  await port.setSignals({ requestToSend: false });
  await port.setSignals({ dataTerminalReady: false });
}

// --- Console ----------------------------------------------------------------------------------

const BOOT_BANNER = "Saksi Pilotu firmware"; // setup() in firmware/src/main.cpp
const STATUS_FIRMWARE = /^firmware\s+(\S+)/; // first line of `status`

/** How long the firmware gets to answer `status`, and to print its banner after a reset. */
export type ConsoleTiming = { answerMs: number; bootMs: number };
const DEFAULT_TIMING: ConsoleTiming = { answerMs: 2500, bootMs: 6000 };

/** Line-based connection to the firmware's serial console (firmware/src/console.cpp). */
export class BoardConsole {
  private reader?: ReadableStreamDefaultReader<Uint8Array>;
  private reading?: Promise<void>;
  private closing = false;
  private readonly watchers = new Set<(line: string) => void>();

  constructor(
    private readonly port: SerialPort,
    private readonly onLine: (line: string) => void,
    private readonly onClosed: () => void,
    private readonly timing: ConsoleTiming = DEFAULT_TIMING,
  ) {}

  async open(): Promise<void> {
    await this.port.open({ baudRate: 115200 });
    await releaseControlLines(this.port);
    const reader = this.port.readable!.getReader();
    this.reader = reader;
    // Unplugging the board ends the stream with an error; either way the console is gone.
    this.reading = this.readLines(reader)
      .catch(() => {})
      .finally(() => {
        reader.releaseLock();
        if (!this.closing) this.onClosed();
      });
  }

  async send(command: string): Promise<void> {
    if (!this.port.writable) throw new Error("Kart bağlı değil.");
    const writer = this.port.writable.getWriter();
    try {
      await writer.write(new TextEncoder().encode(`${command}\n`));
    } finally {
      writer.releaseLock();
    }
  }

  /**
   * Resolves with the version of the firmware answering on the console. A board that doesn't
   * answer is restarted once. `booting` is for a board that was just reset: wait for its banner.
   */
  async ensureFirmware({ booting = false } = {}): Promise<string> {
    if (booting) await this.nextLine((line) => line.startsWith(BOOT_BANNER), this.timing.bootMs);
    const version = (await this.askVersion()) ?? (await this.restartAndAsk());
    if (!version) throw new Error("Kart cevap vermiyor. USB kablosunu çıkarıp takın, sonra tekrar deneyin.");
    return version;
  }

  async close(): Promise<void> {
    this.closing = true;
    // port.close() rejects while the reader still holds its lock, so the read loop has to end first.
    await this.reader?.cancel().catch(() => {});
    await this.reading;
    await this.port.close();
  }

  private async askVersion(): Promise<string | null> {
    const answer = this.nextLine((line) => STATUS_FIRMWARE.test(line), this.timing.answerMs);
    await this.send("status");
    return (await answer)?.match(STATUS_FIRMWARE)?.[1] ?? null;
  }

  private async restartAndAsk(): Promise<string | null> {
    const booted = this.nextLine((line) => line.startsWith(BOOT_BANNER), this.timing.bootMs);
    await restartIntoFirmware(this.port);
    return (await booted) ? this.askVersion() : null;
  }

  /** The next line matching `wanted`, or null once `timeoutMs` has passed. */
  private nextLine(wanted: (line: string) => boolean, timeoutMs: number): Promise<string | null> {
    return new Promise((resolve) => {
      const watcher = (line: string) => {
        if (wanted(line)) done(line);
      };
      const done = (line: string | null) => {
        clearTimeout(timer);
        this.watchers.delete(watcher);
        resolve(line);
      };
      const timer = setTimeout(() => done(null), timeoutMs);
      this.watchers.add(watcher);
    });
  }

  private async readLines(reader: ReadableStreamDefaultReader<Uint8Array>): Promise<void> {
    const decoder = new TextDecoder();
    let pending = "";
    for (;;) {
      const { value, done } = await reader.read();
      if (done) return;
      pending += decoder.decode(value, { stream: true });
      const lines = pending.split("\n");
      pending = lines.pop() ?? "";
      for (const raw of lines) {
        const line = raw.replace(/\r$/, "");
        this.onLine(line);
        for (const watcher of [...this.watchers]) watcher(line);
      }
    }
  }
}
