import { describe, expect, it, vi } from "vitest";
import { BoardConsole } from "./board-serial";

const VERSION = "0.1.0+c595cda6";
const BANNER = `Saksi Pilotu firmware ${VERSION} (reset reason 0). Type help.`;
const FAST = { answerMs: 40, bootMs: 400 };

/**
 * Stands in for the board behind Web Serial. Like Chrome's SerialPort it refuses to close while a
 * stream is locked, and like the ESP32-S3 it reboots when RTS is released: into the firmware, or
 * into the silent ROM bootloader if DTR is still asserted at that moment.
 */
class FakeBoard {
  mode: "firmware" | "bootloader" = "firmware";
  signals: string[] = [];
  commands: string[] = [];
  closed = false;
  readable: ReadableStream<Uint8Array> | null = null;
  writable: WritableStream<Uint8Array> | null = null;

  private out!: ReadableStreamDefaultController<Uint8Array>;
  private dtr = true; // Chrome opens the port with both lines asserted
  private rts = true;

  async open() {
    this.readable = new ReadableStream({
      start: (c) => void (this.out = c),
      // Chrome flushes the port before a cancel settles; the lock is only gone after that.
      cancel: () => new Promise((resolve) => setTimeout(resolve, 20)),
    });
    this.writable = new WritableStream({
      write: (chunk) => {
        const command = new TextDecoder().decode(chunk).trim();
        this.commands.push(command);
        if (this.mode === "firmware" && command === "status") {
          this.print(`firmware   ${VERSION}`, "token      (not set)");
        }
      },
    });
  }

  async close() {
    if (this.readable?.locked || this.writable?.locked) {
      throw new DOMException("Cannot close while a stream is locked.", "InvalidStateError");
    }
    this.readable = this.writable = null;
    this.closed = true;
  }

  async setSignals(s: SerialOutputSignals) {
    if (s.dataTerminalReady !== undefined) {
      this.dtr = s.dataTerminalReady;
      this.signals.push(`DTR=${Number(this.dtr)}`);
    }
    if (s.requestToSend !== undefined) {
      const released = this.rts && !s.requestToSend;
      this.rts = s.requestToSend;
      this.signals.push(`RTS=${Number(this.rts)}`);
      if (released && this.signals.includes("RTS=1")) this.reboot();
    }
  }

  print(...lines: string[]) {
    for (const line of lines) this.out.enqueue(new TextEncoder().encode(`${line}\r\n`));
  }

  unplug() {
    this.out.error(new Error("The device has been lost."));
  }

  private reboot() {
    this.mode = this.dtr ? "bootloader" : "firmware";
    if (this.mode === "firmware") this.print(BANNER);
  }
}

function connect(board: FakeBoard) {
  const lines: string[] = [];
  const onClosed = vi.fn();
  const serial = new BoardConsole(board as unknown as SerialPort, (line) => lines.push(line), onClosed, FAST);
  return { serial, lines, onClosed };
}

describe("BoardConsole", () => {
  it("lowers RTS before DTR once the port is open, so closing it can't reset the board", async () => {
    const board = new FakeBoard();
    const { serial } = connect(board);

    await serial.open();

    expect(board.signals).toEqual(["RTS=0", "DTR=0"]);
    expect(board.mode).toBe("firmware");
    await serial.close();
  });

  it("releases the port on close while a read is still waiting", async () => {
    const board = new FakeBoard();
    const { serial, onClosed } = connect(board);
    await serial.open();

    await serial.close();

    expect(board.closed).toBe(true);
    expect(onClosed).not.toHaveBeenCalled();
  });

  it("passes complete lines on, without the carriage return", async () => {
    const board = new FakeBoard();
    const { serial, lines } = connect(board);
    await serial.open();

    board.print("soil1           2834 mV", "air_temp      no answer");
    await vi.waitFor(() => expect(lines).toEqual(["soil1           2834 mV", "air_temp      no answer"]));
    await serial.close();
  });

  it("reports the firmware version without resetting a board that answers", async () => {
    const board = new FakeBoard();
    const { serial } = connect(board);
    await serial.open();

    await expect(serial.ensureFirmware()).resolves.toBe(VERSION);

    expect(board.signals).toEqual(["RTS=0", "DTR=0"]);
    await serial.close();
  });

  it("restarts a board stuck in the bootloader: DTR pulse first, then the RTS pulse", async () => {
    const board = new FakeBoard();
    board.mode = "bootloader";
    const { serial } = connect(board);
    await serial.open();

    await expect(serial.ensureFirmware()).resolves.toBe(VERSION);

    const restart = board.signals.slice(2).join(" ");
    expect(restart.indexOf("DTR=1")).toBeLessThan(restart.indexOf("RTS=1"));
    expect(board.mode).toBe("firmware");
    expect(board.signals.slice(-2)).toEqual(["RTS=0", "DTR=0"]);
    await serial.close();
  });

  it("waits for the boot banner first when the board has just been reset", async () => {
    const board = new FakeBoard();
    board.mode = "bootloader"; // still booting: silent until the banner
    const { serial } = connect(board);
    await serial.open();

    const version = serial.ensureFirmware({ booting: true });
    setTimeout(() => {
      board.mode = "firmware";
      board.print(BANNER);
    }, 100);

    await expect(version).resolves.toBe(VERSION);
    expect(board.signals).toEqual(["RTS=0", "DTR=0"]);
    await serial.close();
  });

  it("fails when the board stays silent even after a restart", async () => {
    const board = new FakeBoard();
    board.mode = "bootloader";
    board.setSignals = async () => {}; // lines not wired: nothing can restart it
    const { serial } = connect(board);
    await serial.open();

    await expect(serial.ensureFirmware()).rejects.toThrow(/cevap vermiyor/);
    await serial.close();
  });

  it("reports an unplugged board once and lets the port go", async () => {
    const board = new FakeBoard();
    const { serial, onClosed } = connect(board);
    await serial.open();

    board.unplug();

    await vi.waitFor(() => expect(onClosed).toHaveBeenCalledTimes(1));
    expect(board.readable?.locked).toBe(false);
  });
});
