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

export type BandInput = {
  value: number;
  min: number | null;
  max: number | null;
  metric: Metric;
};

export type BandLayout = {
  domain: [number, number];
  /** Band edges and needle as % of the track. */
  bandStart: number;
  bandEnd: number;
  needle: number;
  status: "in" | "over" | "under" | "none";
  /** Signed distance outside the band; 0 when inside or without a threshold. */
  delta: number;
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
  // The track hugs the band with padding, widened just enough to keep moderate
  // outliers visible; the physical range caps it so extreme spikes pin to the edge.
  const domain: [number, number] = [
    min == null ? physLo : Math.max(physLo, Math.min(lo - pad, value - pad * 0.25)),
    max == null ? physHi : Math.min(physHi, Math.max(hi + pad, value + pad * 0.25)),
  ];

  const status = max != null && value > max ? "over" : min != null && value < min ? "under" : "in";
  const delta = status === "over" ? value - hi : status === "under" ? value - lo : 0;

  return {
    domain,
    bandStart: min == null ? 0 : pct(lo, domain),
    bandEnd: max == null ? 100 : pct(hi, domain),
    needle: pct(value, domain),
    status,
    delta: Math.round(delta * 10) / 10,
  };
}
