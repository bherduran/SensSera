import { bandLayout } from "@/lib/band";
import { METRIC_META } from "@/lib/metrics";
import type { Metric } from "@/lib/types";
import { cn } from "@/lib/utils";

type BandGaugeProps = {
  metric: Metric;
  value: number;
  min: number | null;
  max: number | null;
  size?: "lg" | "sm";
  className?: string;
};

const fmt = (n: number) =>
  Math.abs(n) >= 1000 ? Math.round(n).toLocaleString("en-US") : String(Math.round(n * 10) / 10);

/**
 * A reading against its safe band: the number, the band (threshold min–max) and a needle.
 * Answers "is this value bad?" at a glance; out-of-band turns the needle terracotta.
 */
export function BandGauge({ metric, value, min, max, size = "lg", className }: BandGaugeProps) {
  const meta = METRIC_META[metric];
  const band = bandLayout({ value, min, max, metric });
  const out = band.status === "over" || band.status === "under";

  const note =
    band.status === "none"
      ? "no threshold"
      : band.status === "in"
        ? "in band"
        : `${band.delta > 0 ? "+" : "−"}${fmt(Math.abs(band.delta))} ${band.status} band`;

  return (
    <div className={cn("min-w-0", className)}>
      <div className="flex items-baseline justify-between gap-2">
        <span className="label-caps truncate">{meta.label}</span>
        <span className={cn("label-caps shrink-0", out && "text-alert-text")}>{note}</span>
      </div>

      <div
        className={cn(
          "font-mono leading-[1.05] text-foreground",
          size === "lg" ? "mt-1 text-[44px] tracking-[-0.04em]" : "mt-0.5 text-[30px] tracking-[-0.03em]",
        )}
      >
        {fmt(value)}
        <span className={cn("ml-1 tracking-normal text-muted-foreground", size === "lg" ? "text-base" : "text-sm")}>
          {meta.unit}
        </span>
      </div>

      <div
        role="meter"
        aria-label={`${meta.label} ${fmt(value)} ${meta.unit}, ${note}`}
        aria-valuenow={value}
        aria-valuemin={band.domain[0]}
        aria-valuemax={band.domain[1]}
        className="relative mt-2 h-[26px]"
      >
        <div className="absolute inset-x-0 top-[11px] h-1 rounded-full bg-border" />
        {band.status !== "none" && (
          <div
            className="absolute top-[7px] h-3 rounded-[2px] bg-band"
            style={{ left: `${band.bandStart}%`, width: `${band.bandEnd - band.bandStart}%` }}
          />
        )}
        <div
          className={cn(
            "absolute top-0 h-[26px] w-0.5 -translate-x-1/2 transition-[left] duration-500 ease-out motion-reduce:transition-none",
            out ? "bg-alert" : "bg-foreground",
          )}
          style={{ left: `${band.needle}%` }}
        />
      </div>

      <div className="mt-1 flex justify-between font-mono text-[10px] text-muted-foreground">
        <span>{fmt(band.domain[0])}</span>
        {band.status !== "none" && (
          <span>
            {min == null ? "…" : fmt(min)} ⟷ {max == null ? "…" : fmt(max)} safe
          </span>
        )}
        <span>{fmt(band.domain[1])}</span>
      </div>
    </div>
  );
}
