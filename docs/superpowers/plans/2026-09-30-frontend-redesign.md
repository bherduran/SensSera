# Frontend Redesign ("Research Notebook") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stock shadcn / all-green UI of `senssera-web` with the "research notebook" design from `docs/superpowers/specs/2026-09-30-frontend-redesign-design.md`, including a matte-clay 3D hero on the auth screens.

**Architecture:** Tokens first (`app/globals.css`, both themes), then a small set of signature components under `components/notebook/`, then each screen is rebuilt from those pieces. Data hooks, SignalR, and auth logic are untouched. The only new logic is the band-gauge math in `lib/band.ts`, which is unit-tested with Vitest.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind v4, shadcn/ui, Recharts, TanStack Query, `motion` (new), `three` (new, dev-only for the hero render script), Vitest (new, dev-only).

## Global Constraints

- Scope: `senssera-web/` only. No API or data-model changes.
- Palette, light: background `#F3EFE7`, sheet `#FFFDF9`, sheet border `#E1D8C8`, ink `#221F1A`, muted `#8A7F6E`, terracotta `#C0765A` (text-safe `#9A3F22`), sage band `#D7E0CB`.
- Palette, dark: background `#171512`, sheet `#1F1C18`, border `#34302A`, ink `#EFE9DD`, muted `#9A9183`, terracotta `#D98A6C`, band `#39422F`.
- Terracotta only for alerts and out-of-band values; sage only as the safe band. No other hues.
- Fonts: Newsreader (headings, AI notes), Schibsted Grotesk (UI), JetBrains Mono (numbers, `tabular-nums`). Fira removed.
- Radius 4px; 1.5px ink rules; paper shadow `0 12px 24px -18px rgb(70 45 20 / 0.35)`; 24px graph-paper grid on page background.
- Both themes first-class; theme toggle stays.
- Motion respects `prefers-reduced-motion`. No Lenis (authenticated dashboard).
- Commits: conventional, **no AI attribution trailers**.
- Every task ends green on: `npm run lint && npx tsc --noEmit && npm test` (and `npm run build` in the final task).

---

### Task 1: Foundation — tokens, fonts, primitives, band math

**Files:**
- Modify: `senssera-web/app/globals.css` (replace both theme blocks and base layer)
- Modify: `senssera-web/app/layout.tsx` (fonts)
- Modify: `senssera-web/components/ui/{card,button,badge,input,table,dialog,select}.tsx` (radius/shadow classes only)
- Create: `senssera-web/lib/band.ts`, `senssera-web/lib/band.test.ts`, `senssera-web/vitest.config.ts`
- Modify: `senssera-web/package.json` (scripts `test`; devDeps `vitest`)

**Interfaces:**
- Produces CSS tokens: `--background --foreground --card --border --muted-foreground --primary(=ink) --accent-alert --accent-alert-text --band`, Tailwind colors `bg-alert`, `text-alert`, `bg-band`, utility class `.grid-paper`, `.ink-rule`, `.paper-shadow`, fonts `font-sans`(Schibsted) `font-serif`(Newsreader) `font-mono`(JetBrains).
- Produces `bandLayout(input: BandInput): BandLayout` and `METRIC_RANGE: Record<Metric, [number, number]>`.

- [ ] **Step 1: Install test runner**

Run: `cd senssera-web && npm i -D vitest` and add `"test": "vitest run"` to `scripts`.

- [ ] **Step 2: Write failing tests `lib/band.test.ts`**

```ts
import { describe, expect, it } from "vitest";
import { bandLayout } from "./band";

describe("bandLayout", () => {
  it("places band and needle inside a padded domain", () => {
    const r = bandLayout({ value: 20, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("in");
    expect(r.delta).toBe(0);
    expect(r.bandStart).toBeGreaterThan(0);
    expect(r.bandEnd).toBeLessThan(100);
    expect(r.needle).toBeGreaterThan(r.bandStart);
    expect(r.needle).toBeLessThan(r.bandEnd);
  });

  it("flags values over the band with a positive delta", () => {
    const r = bandLayout({ value: 34.6, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("over");
    expect(r.delta).toBeCloseTo(4.6);
    expect(r.needle).toBeGreaterThan(r.bandEnd);
    expect(r.needle).toBeLessThanOrEqual(100);
  });

  it("flags values under the band with a negative delta", () => {
    const r = bandLayout({ value: 5, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("under");
    expect(r.delta).toBeCloseTo(-5);
  });

  it("supports a one-sided threshold", () => {
    const r = bandLayout({ value: 1500, min: null, max: 1200, metric: "co2" });
    expect(r.status).toBe("over");
    expect(r.bandStart).toBe(0);
  });

  it("falls back to the metric's physical range without a threshold", () => {
    const r = bandLayout({ value: 50, min: null, max: null, metric: "humidity" });
    expect(r.status).toBe("none");
    expect(r.needle).toBeCloseTo(50);
  });

  it("clamps the needle into 0–100", () => {
    const r = bandLayout({ value: 9999, min: 10, max: 30, metric: "temperature" });
    expect(r.needle).toBeLessThanOrEqual(100);
  });
});
```

- [ ] **Step 3: Run — expect FAIL** (`npm test` → "Failed to resolve import ./band").

- [ ] **Step 4: Implement `lib/band.ts`**

```ts
import type { Metric } from "./types";

/** Physical ranges, mirroring the API's ingest validator. */
export const METRIC_RANGE: Record<Metric, [number, number]> = {
  temperature: [-40, 80],
  humidity: [0, 100],
  co2: [0, 5000],
  soilMoisture: [0, 100],
  light: [0, 100_000],
  pressure: [800, 1200],
};

export type BandInput = { value: number; min: number | null; max: number | null; metric: Metric };
export type BandLayout = {
  domain: [number, number];
  bandStart: number; // % of track
  bandEnd: number;   // %
  needle: number;    // %
  status: "in" | "over" | "under" | "none";
  delta: number;     // signed distance outside the band, 0 when inside/none
};

const pct = (v: number, [lo, hi]: [number, number]) =>
  Math.min(100, Math.max(0, ((v - lo) / (hi - lo)) * 100));

export function bandLayout({ value, min, max, metric }: BandInput): BandLayout {
  const [physLo, physHi] = METRIC_RANGE[metric];
  if (min == null && max == null) {
    const domain: [number, number] = [physLo, physHi];
    return { domain, bandStart: 0, bandEnd: 0, needle: pct(value, domain), status: "none", delta: 0 };
  }
  const lo = min ?? physLo;
  const hi = max ?? physHi;
  const pad = Math.max((hi - lo) * 0.5, 1);
  // Domain hugs the band with padding, widened to keep moderate outliers on the track.
  const domain: [number, number] = [
    min == null ? physLo : Math.max(physLo, Math.min(lo - pad, value - pad * 0.25)),
    max == null ? physHi : Math.min(physHi, Math.max(hi + pad, value + pad * 0.25)),
  ];
  const status = max != null && value > max ? "over" : min != null && value < min ? "under" : "in";
  const delta = status === "over" ? value - max! : status === "under" ? value - min! : 0;
  return {
    domain,
    bandStart: min == null ? 0 : pct(lo, domain),
    bandEnd: max == null ? 100 : pct(hi, domain),
    needle: pct(value, domain),
    status,
    delta: Math.round(delta * 10) / 10,
  };
}
```

`vitest.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import path from "node:path";

export default defineConfig({
  resolve: { alias: { "@": path.resolve(__dirname) } },
  test: { include: ["lib/**/*.test.ts"] },
});
```

- [ ] **Step 5: Run — expect PASS** (`npm test`).

- [ ] **Step 6: Fonts** — in `app/layout.tsx` replace Fira with `Newsreader({ variable: "--font-newsreader", subsets:["latin"], style:["normal","italic"] })`, `Schibsted_Grotesk({ variable: "--font-schibsted", subsets:["latin"] })`, `JetBrains_Mono({ variable: "--font-jetbrains", subsets:["latin"] })`; put the three variables on `<html>`.

- [ ] **Step 7: Tokens** — in `globals.css`: map `--font-sans/-serif/-mono/-heading` to those variables; replace `:root` and `.dark` blocks with the Global Constraints palette (primary = ink, primary-foreground = paper, destructive = terracotta, success = band tone, charts: `--chart-1` ink, `--chart-2` terracotta); add `--alert`, `--alert-text`, `--band` + `@theme` colors `alert`, `alert-text`, `band`; `--radius: 0.25rem`; add utilities:

```css
@utility grid-paper {
  background-image:
    linear-gradient(var(--grid-line) 1px, transparent 1px),
    linear-gradient(90deg, var(--grid-line) 1px, transparent 1px);
  background-size: 24px 24px;
  background-position: -1px -1px;
}
@utility ink-rule { border-bottom: 1.5px solid var(--foreground); }
@utility paper-shadow { box-shadow: 0 12px 24px -18px rgb(70 45 20 / 0.35); }
@utility label-caps { font-size: 10.5px; letter-spacing: .14em; text-transform: uppercase; color: var(--muted-foreground); }
```

with `--grid-line: #E6DFD2` (light) / `#221F1B` (dark); body gets `grid-paper`; numbers default `font-variant-numeric: tabular-nums` on `.font-mono`. Remove the old `float` keyframes.

- [ ] **Step 8: Primitives** — Card: `rounded-[4px] border bg-card paper-shadow` (drop ring/shadow-sm); Button default variant ink bg / paper text, outline variant ink border; Badge `rounded-[2px] font-mono text-[11px]`; Input/Select trigger `rounded-[4px] bg-card`; Table header `label-caps`; Dialog content `rounded-[4px]`.

- [ ] **Step 9: Verify** `npm run lint && npx tsc --noEmit && npm test`.

- [ ] **Step 10: Commit** `feat(web): notebook design tokens, fonts, band math`.

---

### Task 2: Signature components

**Files:** Create `senssera-web/components/notebook/{band-gauge,field-note,specimen-header,live-stamp}.tsx`

**Interfaces:**
- Consumes: `bandLayout`, `METRIC_META`, tokens from Task 1.
- Produces:
  - `<BandGauge metric: Metric; value: number; min: number|null; max: number|null; size?: "lg"|"sm" />`
  - `<FieldNote label?: string; children: ReactNode; pending?: boolean />`
  - `<SpecimenHeader code: string; subtitle: string; title: string; aside?: ReactNode />`
  - `<LiveStamp connected: boolean />`

- [ ] **Step 1: `band-gauge.tsx`** — header row: `label-caps` metric label left; right `label-caps` text: "in band" / "+4.6 over band" / "−5 under band" (alert-text when out) / "no threshold". Value: `font-mono`, `text-[44px] tracking-[-0.04em]` for lg, `text-3xl` for sm, unit in muted `text-base`. Track: relative `h-[30px]`; `absolute top-[13px] h-1 bg-border` track; band `absolute top-[9px] h-3 bg-band` at `left: bandStart%` width `bandEnd-bandStart%` (hidden when status none); needle `absolute top-[2px] h-[26px] w-0.5 bg-foreground` (`bg-alert` when out) at `left: needle%`. Tick row: `font-mono text-[10px] text-muted-foreground` with domain lo, "min ⟷ max safe", domain hi. Wrap in `<div role="meter" aria-valuenow aria-valuemin aria-valuemax aria-label>`.
- [ ] **Step 2: `field-note.tsx`** — `border-l-2 border-alert bg-alert/8 px-4 py-3`; `label-caps text-alert-text` label (default "Field note — AI"); body `font-serif italic text-[17px] leading-snug`; `pending` renders a muted italic "Writing…" line.
- [ ] **Step 3: `specimen-header.tsx`** — flex row with `ink-rule pb-2.5`; left: `label-caps` code · subtitle, `font-serif text-4xl leading-none mt-1.5` title; right: `aside`.
- [ ] **Step 4: `live-stamp.tsx`** — `label-caps` "Live"/"Offline"; below `font-mono text-[13px]` clock updated every second via `useEffect` interval; dot `size-[7px] rounded-full` `bg-alert` when connected with a `motion-safe:animate-pulse`, `bg-muted-foreground` when not.
- [ ] **Step 5: Verify** lint/tsc/test. **Commit** `feat(web): notebook components (band gauge, field note, specimen header, live stamp)`.

---

### Task 3: Dashboard

**Files:** Modify `senssera-web/app/(app)/dashboard/page.tsx`, `senssera-web/components/insights/ask-panel.tsx`; delete usage of `components/app/live-indicator.tsx` (file removed if unused).

**Interfaces:** Consumes `useThresholds(greenhouseId)` (existing: returns `Threshold[]` with `metric, minValue, maxValue, isEnabled`), Task 2 components.

- [ ] **Step 1:** Page header → `SpecimenHeader code="Notebook" subtitle="{n} greenhouses" title="Dashboard" aside={<LiveStamp connected />}`.
- [ ] **Step 2:** Replace `GreenhouseCard` with `GreenhouseSheet`: sheet (`bg-card border paper-shadow rounded-[4px] p-5`), header line `label-caps` `GH-{index+1 padded 2} · {deviceCount} devices` + serif `text-2xl` name + terracotta `{activeAlerts} open` mark when > 0; body: `useThresholds(g.greenhouseId)` → map enabled threshold by metric → grid `sm:grid-cols-2 lg:grid-cols-3 gap-3` of `<BandGauge size="sm">` per metric, sorted out-of-band first; footer link "Open notebook →" (`font-serif italic`). Keep `upsertMetric` and telemetry handlers unchanged. Layout: sheets stack full width (`space-y-6`), not a 3-column card grid.
- [ ] **Step 3:** Empty/error states: plain sheet with serif line "Nothing recorded yet." + link; no icon bubble.
- [ ] **Step 4:** `AskPanel` → sheet with `label-caps` "Ask the notebook", borderless underlined input (`border-0 border-b rounded-none bg-transparent font-serif text-lg`), suggestion chips as `font-mono text-xs` underlined links, answer rendered in `<FieldNote>`; errors via existing `insightErrorMessage` shown as `text-alert-text text-sm`; "used: get_active_alerts" meta in `font-mono text-[11px] text-muted-foreground`.
- [ ] **Step 5:** Verify lint/tsc/test; browser check `/dashboard` both themes with the compose stack running (live values move, out-of-band gauges terracotta, Ask answer renders as field note). **Commit** `feat(web): notebook dashboard with band gauges`.

---

### Task 4: Greenhouse detail + charts

**Files:** Modify `senssera-web/app/(app)/greenhouses/[id]/page.tsx`, `components/greenhouses/greenhouse-monitoring.tsx`, `components/charts/metric-chart.tsx`, `components/thresholds/thresholds-section.tsx`.

- [ ] **Step 1:** Page header → `SpecimenHeader code="GH · {location}" subtitle="day {daysSince(createdAt)}" title={name}` with `LiveStamp`. `daysSince` = `Math.floor((Date.now() - Date.parse(createdAt)) / 86_400_000) + 1`, inline in the page.
- [ ] **Step 2:** Active alerts strip → `label-caps text-alert-text` "Open alerts" + `font-mono` list `Temperature 57.5 °C · critical`.
- [ ] **Step 3:** `metric-chart.tsx`: stroke `var(--foreground)` 1.5px, no gradient fill; add prop `band?: { min: number | null; max: number | null }` rendered as Recharts `<ReferenceArea y1={min ?? domainMin} y2={max ?? domainMax} fill="var(--band)" fillOpacity={0.6} />`; grid lines `var(--border)` dashed; axis ticks `font-mono 10px` muted; tooltip as small sheet. Pass the band from the greenhouse's thresholds in `greenhouse-monitoring.tsx`. Each chart card header: `label-caps` metric + `BandGauge size="sm"` for the current value instead of the plain number, and Min/Avg/Max row in `font-mono`.
- [ ] **Step 4:** Devices + thresholds sections: sheets with `label-caps` titles, tables with `label-caps` headers and `font-mono` values; threshold rows show a mini static band (`BandGauge` with `value` = midpoint is misleading, so instead render text `12 ⟷ 30 °C` in mono).
- [ ] **Step 5:** Verify + browser check both themes. **Commit** `feat(web): notebook greenhouse detail and charts with threshold bands`.

---

### Task 5: Alerts ledger + explain

**Files:** Modify `senssera-web/app/(app)/alerts/page.tsx`, `components/insights/explain-alert-dialog.tsx`.

- [ ] **Step 1:** Header `SpecimenHeader code="Ledger" subtitle="{total} entries" title="Alerts"`. Filters: status as text tabs (`label-caps`, active one ink-underlined), greenhouse select kept.
- [ ] **Step 2:** Table → ledger rows: `font-mono text-xs` timestamp `yyyy-MM-dd HH:mm`, severity mark (critical: filled `size-2 bg-alert`, warning: `size-2 border border-alert`), metric label, value `font-mono`, status word, actions as text buttons (Acknowledge / Resolve / Explain). Row separators `border-b border-dashed`. `SeverityBadge` removed.
- [ ] **Step 3:** Explain dialog body → `<FieldNote>` for explanation, second `FieldNote label="Suggested action"` for the action, model + cached info in `font-mono text-[11px]`.
- [ ] **Step 4:** Verify + browser check (explain renders with the Groq key). **Commit** `feat(web): alerts ledger and field-note explanations`.

---

### Task 6: Shell, greenhouses list, dialogs, auth forms

**Files:** Modify `components/app/app-shell.tsx`, `app/(app)/greenhouses/page.tsx`, `components/greenhouses/greenhouse-form-dialog.tsx`, `components/devices/*.tsx`, `components/thresholds/threshold-form-dialog.tsx`, `components/auth/{auth-card,login-form,register-form}.tsx`, `components/app/theme-toggle.tsx`.

- [ ] **Step 1:** Sidebar → notebook index: `SensSera` wordmark in `font-serif text-xl`, nav items as numbered entries `01 Dashboard / 02 Greenhouses / 03 Alerts` (`font-mono text-xs` number, active = ink underline, no filled pill), `bg-transparent border-r`. Top bar: only theme toggle + user menu, no fill.
- [ ] **Step 2:** Greenhouses list → sheets with `label-caps GH-0n · location`, serif name, "Open notebook →"; create/edit/delete actions kept.
- [ ] **Step 3:** Dialogs: title `font-serif text-2xl`, labels `label-caps`, primary button ink. Device-token dialog: token in `font-mono` on a `bg-muted` strip with copy button.
- [ ] **Step 4:** Auth card: paper sheet, serif headline ("Welcome back" → "Open your notebook"), `label-caps` field labels, ink submit button.
- [ ] **Step 5:** Verify + browser walk of every remaining screen both themes. **Commit** `feat(web): notebook shell, lists, dialogs and auth forms`.

---

### Task 7: Clay hero (Three.js + Higgsfield) and auth layout

**Files:** Create `senssera-web/scripts/hero/render.html`, `senssera-web/scripts/hero/render.mjs`, `senssera-web/public/hero/{light,dark}.webp`; modify `app/(auth)/layout.tsx`, delete `components/auth/greenhouse-panel.tsx`; devDeps `three`, `@types/three`, `playwright` (if not present) for headless capture.

- [ ] **Step 1: Scene** `scripts/hero/render.html` (ES module importing `three` from the local `node_modules` via an import map): orthographic-ish `PerspectiveCamera(28°)` looking down 25°; seamless backdrop (large rounded plane curving up, color = page background of the theme); greenhouse = `RoundedBoxGeometry` base + gable roof from `ExtrudeGeometry`, frame in matte clay (`MeshStandardMaterial roughness 0.9, metalness 0`), glass panes `MeshPhysicalMaterial transmission 0.6 roughness 0.35`; three clay plants (capsule stems + sphere/lathe leaves, sage), one terracotta pot, one small sensor puck with a terracotta LED; lights: `HemisphereLight` + soft key `DirectionalLight` with `PCFSoftShadowMap` shadow radius 8 + fill; `ACESFilmicToneMapping`. `?theme=light|dark` switches backdrop/ambient. Renders once at 1600×1200 and sets `window.__done = true`.
- [ ] **Step 2: Capture** `scripts/hero/render.mjs`: serve `senssera-web/` statically (node `http`), open the page with Playwright Chromium for each theme, wait for `__done`, screenshot canvas → `public/hero/{theme}.png`, convert to WebP with `sharp` (already a Next dependency). Script: `"hero": "node scripts/hero/render.mjs"`.
- [ ] **Step 3: Higgsfield alternatives** — install `@higgsfield/cli`, user runs its login once; generate 2 variants per theme from prompt: "matte clay 3D render, small glass greenhouse with clay plants and one terracotta pot, tiny sensor with orange light, soft studio lighting, seamless warm paper-colored background #F3EFE7, minimal, centered, no text" (dark: "…seamless warm charcoal background #171512, soft rim light"). Save to `scripts/hero/candidates/`.
- [ ] **Step 4: Pick** — show both sets side by side in the brainstorm companion; user picks; copy the pick to `public/hero/`.
- [ ] **Step 5: Auth layout** — two columns on `lg`: left `next/image` of `/hero/light.webp` (`dark:hidden`) and `/hero/dark.webp` (`hidden dark:block`), `priority`, `sizes="50vw"`, object-cover; caption `label-caps` "Specimen 01 — clay study" + serif line "Every leaf, measured."; wrap the image in `motion.div` `initial={{opacity:0,y:12}} animate={{opacity:1,y:0}} transition={{duration:.6}}` using `useReducedMotion()` to skip. Right: form sheet. Mobile: image becomes a 160px band on top.
- [ ] **Step 6:** Verify + browser check login/register both themes, reduced motion. **Commit** `feat(web): clay hero render and auth layout`.

---

### Task 8: Final verification

- [ ] `npm run lint && npx tsc --noEmit && npm test && npm run build` green.
- [ ] `docker compose up -d --build web` and walk: login → dashboard (live updates, out-of-band gauges) → greenhouse detail (charts with bands) → alerts (acknowledge, explain) → Ask; both themes; screenshots saved to the scratchpad and shown to the user.
- [ ] Contrast spot-check: terracotta text uses `alert-text` in light mode.
- [ ] Open PR `feat: notebook redesign` (no attribution lines); user merges.
