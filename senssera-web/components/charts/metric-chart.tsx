"use client";

import { useEffect, useId, useState } from "react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

export type ChartPoint = { t: number; value: number };

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
  new Date(t).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

/**
 * Single-series metric trend: one brand hue, gradient area + smooth line,
 * recessive grid/axes, crosshair tooltip. Identity comes from the card title,
 * so the chart needs no legend (dataviz: single series).
 */
export function MetricChart({
  data,
  unit,
  color = "var(--chart-1)",
  height = 160,
}: {
  data: ChartPoint[];
  unit?: string;
  color?: string;
  height?: number;
}) {
  const gradientId = useId().replace(/:/g, "");
  const reduced = useReducedMotion();

  if (data.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-lg border border-dashed text-xs text-muted-foreground"
        style={{ height }}
      >
        Waiting for readings…
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={height}>
      <AreaChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: -12 }}>
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity={0.28} />
            <stop offset="100%" stopColor={color} stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid
          vertical={false}
          stroke="var(--border)"
          strokeOpacity={0.6}
        />
        <XAxis
          dataKey="t"
          type="number"
          scale="time"
          domain={["dataMin", "dataMax"]}
          tickFormatter={timeFmt}
          tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
          tickLine={false}
          axisLine={false}
          minTickGap={40}
        />
        <YAxis
          width={44}
          tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
          tickLine={false}
          axisLine={false}
          domain={["auto", "auto"]}
        />
        <Tooltip
          cursor={{ stroke: "var(--muted-foreground)", strokeDasharray: "3 3" }}
          content={({ active, payload }) => {
            if (!active || !payload?.length) return null;
            const point = payload[0].payload as ChartPoint;
            return (
              <div className="rounded-md border bg-popover px-2.5 py-1.5 text-xs text-popover-foreground shadow-md">
                <div className="font-semibold tabular-nums">
                  {point.value}
                  {unit ? <span className="ml-0.5 font-normal">{unit}</span> : null}
                </div>
                <div className="text-muted-foreground">{timeFmt(point.t)}</div>
              </div>
            );
          }}
        />
        <Area
          type="monotone"
          dataKey="value"
          stroke={color}
          strokeWidth={2}
          fill={`url(#${gradientId})`}
          isAnimationActive={!reduced}
          animationDuration={300}
          dot={false}
          activeDot={{ r: 4, strokeWidth: 0 }}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}
