# Frontend Redesign — "Research Notebook"

Date: 2026-09-30 · Scope: `senssera-web/` only (no API or data-model changes)

## Problem

The current UI reads as a stock shadcn template: dark green everywhere, identical metric cards, ordinary type, numbers that don't stand out. The goal is a calm, distinctive interface with some 3D character that doesn't look AI-generated.

## Direction

A greenhouse **research notebook**: paper, ink, one warm accent. Identity comes from a few signature elements, not from color alone.

### Visual system

| Token | Light ("paper") | Dark ("night notebook") |
|---|---|---|
| background | warm paper `#F3EFE7` | warm charcoal `#171512` |
| surface (sheet) | `#FFFDF9`, 1px `#E1D8C8` border | `#1F1C18`, 1px `#34302A` border |
| ink (text) | `#221F1A` | cream `#EFE9DD` |
| muted text | `#8A7F6E` | `#9A9183` |
| accent (alert / out-of-band only) | terracotta `#C0765A` (text-safe variant `#9A3F22`) | `#D98A6C` |
| safe band | sage `#D7E0CB` | `#39422F` |

- Terracotta is reserved for alerts and out-of-band values. Sage appears only inside gauges as the safe range. No other hues.
- Background carries a faint 24px graph-paper grid (disabled on dense tables).
- Rules: 1.5px ink lines for section headers; radius 4px; soft paper shadow `0 12px 24px -18px`.
- Type: **Newsreader** (headings, AI notes, italic accents) · **Schibsted Grotesk** (UI text) · **JetBrains Mono** (all numbers, `tabular-nums`). Fira is removed. Small uppercase labels are tracked at 0.14em.
- Both themes are first-class: every screen is checked in both. The theme toggle stays.

### Signature components (`components/notebook/`)

- **`BandGauge`** — value, safe range (threshold min/max), and a needle. Out of band → needle and delta label turn terracotta ("+4.6 over band"). With no threshold, the gauge shows only the needle on the metric's physical range.
- **`FieldNote`** — italic serif block with a terracotta left rule and a "Field note — AI" label. Used for Ask answers and alert explanations.
- **`SpecimenHeader`** — label line (`GH-01 · Antalya · day N`), large serif title, 1.5px rule, optional live stamp on the right. "Day N" counts from the greenhouse's `createdAt`.
- **`LiveStamp`** — dot + monospace clock driven by the existing telemetry connection state.
- **`GridPaper`** — background utility.

The existing shadcn primitives (button, input, select, dialog, table, badge, switch, dropdown) are restyled through tokens plus small class changes. They are not replaced.

### Screens

- **Login / register** — split layout: the clay hero render on one side, form on a paper sheet. Serif headline.
- **Dashboard** — one notebook sheet per greenhouse: `SpecimenHeader`, a grid of `BandGauge`s (one per metric, out-of-band ones first), active-alert count. The Ask panel becomes a question line whose answers render as `FieldNote`.
- **Greenhouse detail** — `SpecimenHeader`; per-metric charts restyled (ink line, threshold band as a shaded sage area, terracotta points where out of band); devices and thresholds sections on sheets.
- **Alerts** — a ledger: dense rows with a monospace timestamp, severity as a terracotta mark (critical filled, warning outline), status text. Explain opens a `FieldNote`.
- **Greenhouses list, devices, thresholds, dialogs, app shell** — same tokens and components; the sidebar becomes a slim notebook index.

## Hero render (the 3D element)

A matte-clay greenhouse scene: small glass greenhouse, a few clay plants, a terracotta pot, and a sensor. Soft studio light on a seamless paper-colored backdrop. Light and night variants come from the same scene.

- **Path A — Three.js (in repo):** a script under `senssera-web/scripts/hero/` builds the scene (matte `MeshStandardMaterial`, rounded geometry, soft shadows) and renders PNG/WebP at 2× for both themes. Reproducible and free.
- **Path B — Higgsfield:** generate alternatives from a prompt matching the same description. Needs a one-time login by the user.
- Both go side by side; one is picked. The chosen images ship as static assets (`public/hero/`, WebP, width ≤ 1600). The page uses `next/image`, plus a single Motion fade/rise on entry that is disabled under `prefers-reduced-motion`.

## Dependencies

- Add `motion` (entry animation). `three` becomes a dev dependency, used only by the hero render script, never shipped to the browser.
- Fonts via `next/font/google`.
- No Lenis: an authenticated dashboard (user rule: skip for admin dashboards behind auth).

## Out of scope

- The floor-plan view (rejected), device position data, and any API changes.
- New features. This is a visual redesign. Behavior, data flow, SignalR and auth logic stay as they are.

## Verification

- `npm run lint`, `npx tsc --noEmit`, `npm run build` stay green.
- Every screen is walked in the browser against the running stack in both themes, with screenshots. The flows checked: login, dashboard live updates, greenhouse detail charts, alert explain, and Ask.
- Reduced-motion check. Text-contrast check on terracotta (text uses `#9A3F22` in light mode).
