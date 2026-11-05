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

// --- Console ----------------------------------------------------------------------------------

/** Line-based connection to the firmware's serial console (firmware/src/console.cpp). */
export class BoardConsole {
  private abort?: AbortController;
  private reading?: Promise<void>;

  constructor(
    private readonly port: SerialPort,
    private readonly onLine: (line: string) => void,
    private readonly onClosed: () => void,
  ) {}

  async open(): Promise<void> {
    await this.port.open({ baudRate: 115200 });
    const abort = new AbortController();
    this.abort = abort;
    // Unplugging the board ends the stream with an error; either way the console is gone.
    this.reading = this.readLines(abort.signal)
      .catch(() => {})
      .finally(() => {
        if (!abort.signal.aborted) this.onClosed();
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

  async close(): Promise<void> {
    this.abort?.abort();
    await this.reading;
    await this.port.close().catch(() => {});
  }

  private async readLines(signal: AbortSignal): Promise<void> {
    if (!this.port.readable) return;
    let pending = "";
    // The signal goes on the first pipe so aborting unlocks port.readable and close() can run.
    // TS types the decoder's input as BufferSource, the port's output as Uint8Array; they fit.
    const decoder = new TextDecoderStream() as unknown as TransformStream<Uint8Array, string>;
    await this.port.readable.pipeThrough(decoder, { signal }).pipeTo(
      new WritableStream({
        write: (chunk) => {
          pending += chunk;
          const lines = pending.split("\n");
          pending = lines.pop() ?? "";
          for (const line of lines) this.onLine(line.replace(/\r$/, ""));
        },
      }),
    );
  }
}
