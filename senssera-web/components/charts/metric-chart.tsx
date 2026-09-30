"use client";

import { useEffect, useState } from "react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceArea,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

export type ChartPoint = { t: number; value: number };
export type ChartBand = { min: number | null; max: number | null };

function useReducedMotion() {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    const mq = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReduced(mq.matches);
    update();
    mq.addEventListener("change", update);
    return () => mq.removeEventListener("change", update);
  }, []);
  return reduced;
}

const timeFmt = (t: number) =>
  new Date(t).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });

const tick = { fontSize: 10, fill: "var(--muted-foreground)", fontFamily: "var(--font-jetbrains)" };

const axisFmt = new Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 1 });

const isOut = (v: number, band?: ChartBand) =>
  !!band && ((band.max != null && v > band.max) || (band.min != null && v < band.min));

/**
 * Single-series trend drawn like a plotted notebook line: ink stroke, the safe band as a
 * shaded sage strip, and terracotta marks on the points that left the band.
 */
export function MetricChart({
  data,
  unit,
  band,
  height = 170,
}: {
  data: ChartPoint[];
  unit?: string;
  band?: ChartBand;
  height?: number;
}) {
  const reduced = useReducedMotion();

  if (data.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-[2px] border border-dashed font-serif text-sm italic text-muted-foreground"
        style={{ height }}
      >
        Waiting for the first reading…
      </div>
    );
  }

  const values = data.map((d) => d.value);
  const lo = Math.min(...values, band?.min ?? Infinity);
  const hi = Math.max(...values, band?.max ?? -Infinity);
  const pad = Math.max((hi - lo) * 0.08, 0.5);
  // Physical metrics here are never negative when the data isn't; don't pad the axis below zero.
  const domain: [number, number] = [lo >= 0 ? Math.max(0, lo - pad) : lo - pad, hi + pad];

  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="2 4" />
        {band && (band.min != null || band.max != null) && (
          <ReferenceArea
            y1={band.min ?? domain[0]}
            y2={band.max ?? domain[1]}
            fill="var(--band)"
            fillOpacity={0.55}
            stroke="none"
            ifOverflow="hidden"
          />
        )}
        <XAxis
          dataKey="t"
          type="number"
          scale="time"
          domain={["dataMin", "dataMax"]}
          tickFormatter={timeFmt}
          tick={tick}
          tickLine={false}
          axisLine={{ stroke: "var(--foreground)", strokeWidth: 1 }}
          minTickGap={48}
        />
        <YAxis
          width={40}
          tick={tick}
          tickLine={false}
          axisLine={false}
          domain={domain}
          tickCount={4}
          tickFormatter={(v: number) => axisFmt.format(v)}
        />
        <Tooltip
          cursor={{ stroke: "var(--foreground)", strokeDasharray: "2 3" }}
          content={({ active, payload }) => {
            if (!active || !payload?.length) return null;
            const point = payload[0].payload as ChartPoint;
            return (
              <div className="rounded-[2px] border bg-popover px-2.5 py-1.5 paper-shadow">
                <div className={isOut(point.value, band) ? "font-mono text-sm text-alert-text" : "font-mono text-sm"}>
                  {point.value}
                  {unit ? <span className="ml-1 text-muted-foreground">{unit}</span> : null}
                </div>
                <div className="font-mono text-[10px] text-muted-foreground">{timeFmt(point.t)}</div>
              </div>
            );
          }}
        />
        <Area
          type="monotone"
          dataKey="value"
          stroke="var(--foreground)"
          strokeWidth={1.5}
          fill="transparent"
          isAnimationActive={!reduced}
          animationDuration={300}
          dot={(props: { cx?: number; cy?: number; payload?: ChartPoint; index?: number }) => {
            const { cx, cy, payload, index } = props;
            if (cx == null || cy == null || !payload || !isOut(payload.value, band)) {
              return <g key={`d-${index}`} />;
            }
            return <circle key={`d-${index}`} cx={cx} cy={cy} r={2.5} fill="var(--alert)" />;
          }}
          activeDot={{ r: 3.5, strokeWidth: 0, fill: "var(--foreground)" }}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
