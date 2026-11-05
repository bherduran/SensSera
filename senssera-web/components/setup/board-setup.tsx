"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Loader2 } from "lucide-react";
import {
  BoardConsole,
  downloadFirmware,
  flashFirmware,
  pickPort,
  serialSupported,
} from "@/lib/board-serial";
import { calibrationCommand, parseRawSoilLine, tokenCommand, wifiCommand } from "@/lib/firmware";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const MAX_LINES = 400;
const SOIL_CHANNELS = ["soil1", "soil2", "soil3", "soil4"];

type Install =
  | { state: "idle" }
  | { state: "working"; step: string; percent?: number }
  | { state: "done"; version: string }
  | { state: "error"; message: string };

// Support never changes while the page is open, so there is nothing to subscribe to.
const noSubscription = () => () => {};

// A user closing the browser's port picker isn't an error worth showing.
const cancelledPicker = (e: unknown) => e instanceof DOMException && e.name === "NotFoundError";
const messageOf = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function BoardSetup() {
  // navigator only exists in the browser: null on the server, so both renders match.
  const supported = useSyncExternalStore(noSubscription, serialSupported, () => null);
  const [install, setInstall] = useState<Install>({ state: "idle" });
  const [connected, setConnected] = useState(false);
  const [lines, setLines] = useState<string[]>([]);
  const [rawMv, setRawMv] = useState<Record<string, number>>({});
  const consoleRef = useRef<BoardConsole | null>(null);
  const logRef = useRef<HTMLPreElement>(null);

  useEffect(() => () => void consoleRef.current?.close(), []);
  useEffect(() => {
    logRef.current?.scrollTo({ top: logRef.current.scrollHeight });
  }, [lines]);

  const addLine = (line: string) => setLines((prev) => [...prev, line].slice(-MAX_LINES));

  function onBoardLine(line: string) {
    addLine(line);
    const soil = parseRawSoilLine(line);
    if (soil) setRawMv((prev) => ({ ...prev, [soil.channel]: soil.mv }));
  }

  async function disconnect() {
    const c = consoleRef.current;
    consoleRef.current = null;
    setConnected(false);
    await c?.close();
  }

  async function connect() {
    try {
      const port = await pickPort();
      await disconnect();
      const c = new BoardConsole(port, onBoardLine, () => {
        consoleRef.current = null;
        setConnected(false);
        addLine("— bağlantı koptu —");
      });
      await c.open();
      consoleRef.current = c;
      setConnected(true);
      addLine("— karta bağlandı —");
      await c.send("status");
    } catch (e) {
      if (!cancelledPicker(e)) addLine(`Bağlanılamadı: ${messageOf(e)}`);
    }
  }

  /** Sends a console command; `shown` replaces it in the log so secrets never appear there. */
  async function send(command: string, shown = command) {
    const c = consoleRef.current;
    if (!c) return;
    addLine(`> ${shown}`);
    try {
      await c.send(command);
    } catch (e) {
      addLine(`Gönderilemedi: ${messageOf(e)}`);
    }
  }

  async function installFirmware() {
    try {
      // requestPort needs the click's user activation, so it comes before any download.
      const port = await pickPort();
      await disconnect();
      setInstall({ state: "working", step: "Firmware indiriliyor…" });
      const image = await downloadFirmware();
      setInstall({ state: "working", step: "Karta bağlanılıyor…" });
      await flashFirmware(
        port,
        image,
        (percent) => setInstall({ state: "working", step: "Yazılıyor", percent }),
        () => {},
      );
      setInstall({ state: "done", version: image.manifest.version });
    } catch (e) {
      if (cancelledPicker(e)) return setInstall({ state: "idle" });
      setInstall({ state: "error", message: messageOf(e) });
    }
  }

  if (supported === null) return null;
  if (!supported) {
    return (
      <Card className="px-6">
        <p className="font-serif text-lg">Bu tarayıcı karta bağlanamıyor.</p>
        <p className="text-muted-foreground">
          Kurulum Web Serial ister: masaüstünde Chrome veya Edge ile açın. Safari, Firefox ve telefonlar desteklemiyor.
        </p>
      </Card>
    );
  }

  const busy = install.state === "working";

  return (
    <div className="space-y-6">
      <Step number={1} title="Firmware'i yükle">
        <p className="text-muted-foreground">
          En son sürümü karta yazar. Wi-Fi, token ve kalibrasyon silinmez; güncellemeden sonra kart kendiliğinden
          bağlanır.
        </p>
        <Button onClick={installFirmware} disabled={busy} className="self-start">
          {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          Firmware&apos;i yükle
        </Button>
        {install.state === "working" && (
          <div className="space-y-1.5" aria-live="polite">
            <p className="font-mono text-xs">
              {install.step}
              {install.percent !== undefined && ` · %${install.percent}`}
            </p>
            {install.percent !== undefined && (
              <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
                <div className="h-full bg-foreground transition-[width]" style={{ width: `${install.percent}%` }} />
              </div>
            )}
          </div>
        )}
        {install.state === "done" && (
          <p aria-live="polite">
            Yüklendi: <span className="font-mono">{install.version}</span>. Kart yeniden başladı. Ayar gerekiyorsa
            2. adımda <strong>Karta bağlan</strong>&apos;a basın.
          </p>
        )}
        {install.state === "error" && (
          <div className="space-y-1" role="alert">
            <p className="text-destructive">{install.message}</p>
            <p className="text-muted-foreground">
              Kart bağlanmıyorsa: <strong>BOOT</strong> tuşunu basılı tutun, <strong>RESET</strong>&apos;e basıp
              bırakın, sonra BOOT&apos;u bırakıp tekrar deneyin. Bittikten sonra RESET&apos;e bir kez basın.
            </p>
          </div>
        )}
      </Step>

      <Step number={2} title="Karta bağlan">
        <p className="text-muted-foreground">
          Ayar ve kalibrasyon için kartın konsoluna bağlanır. Listeden kartı seçin.
        </p>
        <div className="flex flex-wrap gap-2">
          {connected ? (
            <>
              <Button variant="outline" onClick={() => send("status")}>Durum</Button>
              <Button variant="outline" onClick={() => send("raw")}>Sensörleri oku</Button>
              <Button variant="outline" onClick={disconnect}>Bağlantıyı kes</Button>
            </>
          ) : (
            <Button onClick={connect} disabled={busy}>Karta bağlan</Button>
          )}
        </div>
        <pre
          ref={logRef}
          className="h-64 overflow-auto rounded-md border bg-muted/40 p-3 font-mono text-xs leading-relaxed whitespace-pre-wrap"
          aria-label="Kart konsolu"
        >
          {lines.length ? lines.join("\n") : "Konsol çıktısı burada görünecek."}
        </pre>
        <FreeCommand disabled={!connected} onSend={(c) => send(c)} />
      </Step>

      <Step number={3} title="Wi-Fi ve token">
        <SettingsForm disabled={!connected} onSend={send} />
      </Step>

      <Step number={4} title="Nem sensörünü kalibre et">
        <CalibrationForm disabled={!connected} rawMv={rawMv} onSend={send} />
      </Step>
    </div>
  );
}

function Step({ number, title, children }: { number: number; title: string; children: React.ReactNode }) {
  return (
    <Card className="gap-4 px-6">
      <div className="border-b border-dashed pb-2">
        <p className="label-caps">Adım {number}</p>
        <h2 className="font-serif text-2xl leading-tight">{title}</h2>
      </div>
      {children}
    </Card>
  );
}

function FreeCommand({ disabled, onSend }: { disabled: boolean; onSend: (command: string) => void }) {
  const [value, setValue] = useState("");
  return (
    <form
      className="flex gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        if (!value.trim()) return;
        onSend(value.trim());
        setValue("");
      }}
    >
      <Input
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder="Komut (ör. help)"
        aria-label="Konsol komutu"
        className="font-mono"
        disabled={disabled}
        autoComplete="off"
      />
      <Button type="submit" variant="outline" disabled={disabled}>Gönder</Button>
    </form>
  );
}

function SettingsForm({
  disabled,
  onSend,
}: {
  disabled: boolean;
  onSend: (command: string, shown?: string) => Promise<void>;
}) {
  const [ssid, setSsid] = useState("");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    // Either part can be updated alone; empty fields are left as they are on the board.
    const wifi = ssid ? wifiCommand(ssid, password) : null;
    const tok = token.trim() ? tokenCommand(token) : null;
    const failed = [wifi, tok].find((r) => r && !r.ok);
    if (failed && !failed.ok) return setError(failed.error);
    if (!wifi && !tok) return setError("Ağ adı veya token girin.");

    if (tok?.ok) await onSend(tok.command, "token ••••••");
    if (wifi?.ok) await onSend(wifi.command, `wifi "${ssid}"${password ? " ••••••" : ""}`);
    setPassword("");
    setToken("");
    // Joining the network takes a few seconds; then the status shows whether it worked.
    setTimeout(() => void onSend("status"), 8000);
  }

  return (
    <form onSubmit={save} className="space-y-4" noValidate>
      <p className="text-muted-foreground">
        Token&apos;ı web uygulamasında sera sayfasında kart eklerken bir kez gösterilen değerden kopyalayın. Kart
        yalnızca 2,4 GHz ağlara bağlanır.
      </p>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="ssid" label="Wi-Fi ağ adı">
          <Input id="ssid" value={ssid} onChange={(e) => setSsid(e.target.value)} autoComplete="off" disabled={disabled} />
        </Field>
        <Field id="wifi-password" label="Wi-Fi şifresi">
          <Input
            id="wifi-password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="off"
            disabled={disabled}
          />
        </Field>
      </div>
      <Field id="token" label="Kart token'ı">
        <Input
          id="token"
          type="password"
          value={token}
          onChange={(e) => setToken(e.target.value)}
          placeholder="nodeId.secret"
          autoComplete="off"
          className="font-mono"
          disabled={disabled}
        />
      </Field>
      {error && <p className="text-destructive" role="alert">{error}</p>}
      <Button type="submit" disabled={disabled}>Karta kaydet</Button>
    </form>
  );
}

function CalibrationForm({
  disabled,
  rawMv,
  onSend,
}: {
  disabled: boolean;
  rawMv: Record<string, number>;
  onSend: (command: string) => Promise<void>;
}) {
  const [channel, setChannel] = useState("soil1");
  const [dry, setDry] = useState("");
  const [wet, setWet] = useState("");
  const [error, setError] = useState<string | null>(null);
  const last = rawMv[channel];

  async function save(e: React.FormEvent) {
    e.preventDefault();
    const result = calibrationCommand(channel, Number(dry), Number(wet));
    if (!result.ok) return setError(result.error);
    setError(null);
    await onSend(result.command);
  }

  return (
    <form onSubmit={save} className="space-y-4" noValidate>
      <ol className="list-decimal space-y-1 pl-5 text-muted-foreground">
        <li>Sensör kuru, havada dururken <strong>Ölç</strong>&apos;e basın ve <strong>Kuru olarak al</strong>.</li>
        <li>Sensörü çizgisine kadar bir bardak suya koyun, tekrar ölçün ve <strong>Islak olarak al</strong>.</li>
        <li><strong>Kalibrasyonu kaydet</strong>.</li>
      </ol>
      <div className="flex flex-wrap items-end gap-3">
        <Field id="cal-channel" label="Kanal">
          <select
            id="cal-channel"
            value={channel}
            onChange={(e) => setChannel(e.target.value)}
            disabled={disabled}
            className="h-8 rounded-lg border border-input bg-transparent px-2.5 text-sm disabled:opacity-50"
          >
            {SOIL_CHANNELS.map((c) => (
              <option key={c} value={c}>{c}</option>
            ))}
          </select>
        </Field>
        <Button type="button" variant="outline" disabled={disabled} onClick={() => onSend("raw")}>Ölç</Button>
        <p className="pb-1.5 font-mono text-sm">{last !== undefined ? `${last} mV` : "—"}</p>
        <Button type="button" variant="outline" disabled={disabled || last === undefined} onClick={() => setDry(String(last))}>
          Kuru olarak al
        </Button>
        <Button type="button" variant="outline" disabled={disabled || last === undefined} onClick={() => setWet(String(last))}>
          Islak olarak al
        </Button>
      </div>
      <div className="grid max-w-sm grid-cols-2 gap-4">
        <Field id="cal-dry" label="Kuru (mV)">
          <Input id="cal-dry" inputMode="numeric" value={dry} onChange={(e) => setDry(e.target.value)} className="font-mono" disabled={disabled} />
        </Field>
        <Field id="cal-wet" label="Islak (mV)">
          <Input id="cal-wet" inputMode="numeric" value={wet} onChange={(e) => setWet(e.target.value)} className="font-mono" disabled={disabled} />
        </Field>
      </div>
      {error && <p className="text-destructive" role="alert">{error}</p>}
      <Button type="submit" disabled={disabled}>Kalibrasyonu kaydet</Button>
    </form>
  );
}

function Field({ id, label, children }: { id: string; label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  );
}
