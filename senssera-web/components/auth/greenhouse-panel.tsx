"use client";

import * as React from "react";
import { Leaf, Thermometer, Droplets, Wind, Sprout } from "lucide-react";
import { cn } from "@/lib/utils";

type Metric = {
  icon: React.ElementType;
  label: string;
  unit: string;
  value: number;
  min: number;
  max: number;
  decimals: number;
  position: string;
  floatDuration: string;
};

const INITIAL_METRICS: Metric[] = [
  { icon: Thermometer, label: "Temp", unit: "°C", value: 24.8, min: 22, max: 28, decimals: 1, position: "top-6 left-4", floatDuration: "6s" },
  { icon: Droplets, label: "Humidity", unit: "%", value: 68, min: 60, max: 75, decimals: 0, position: "top-16 right-2", floatDuration: "7s" },
  { icon: Wind, label: "CO₂", unit: "ppm", value: 820, min: 700, max: 950, decimals: 0, position: "bottom-16 left-2", floatDuration: "6.5s" },
  { icon: Sprout, label: "Soil", unit: "%", value: 42, min: 35, max: 55, decimals: 0, position: "bottom-8 right-6", floatDuration: "7.5s" },
];

function jitter(m: Metric): number {
  const span = (m.max - m.min) * 0.06;
  const next = m.value + (Math.random() - 0.5) * 2 * span;
  return Math.min(m.max, Math.max(m.min, next));
}

export function GreenhousePanel() {
  const [metrics, setMetrics] = React.useState(INITIAL_METRICS);

  React.useEffect(() => {
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduce) return;
    const id = setInterval(() => {
      setMetrics((prev) => prev.map((m) => ({ ...m, value: jitter(m) })));
    }, 2500);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="relative hidden lg:flex flex-col justify-between overflow-hidden bg-gradient-to-br from-green-900 via-green-800 to-green-950 p-12 text-white">
      {/* Dotted texture + soft glows */}
      <div
        className="absolute inset-0 opacity-10"
        style={{
          backgroundImage:
            "radial-gradient(circle at 2px 2px, currentColor 1px, transparent 0)",
          backgroundSize: "32px 32px",
        }}
      />
      <div className="absolute top-20 right-20 h-64 w-64 rounded-full bg-green-600/20 blur-3xl" />
      <div className="absolute bottom-32 left-16 h-96 w-96 rounded-full bg-green-500/10 blur-3xl" />

      {/* Brand */}
      <div className="relative z-10">
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-xl border border-white/20 bg-white/10 backdrop-blur-sm">
            <Leaf className="h-7 w-7 text-green-300" />
          </div>
          <h1 className="text-3xl font-bold tracking-tight">SensSera</h1>
        </div>
        <p className="max-w-md text-lg font-light text-green-100">
          Real-time greenhouse monitoring, threshold alerts, and analytics —
          across every site, in one dashboard.
        </p>
      </div>

      {/* Greenhouse illustration with live sensor chips */}
      <div className="relative z-10 flex items-center justify-center">
        <div className="relative w-full max-w-md">
          <svg
            viewBox="0 0 400 300"
            className="h-auto w-full drop-shadow-2xl"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
          >
            <rect x="80" y="180" width="240" height="100" fill="rgba(255,255,255,0.05)" stroke="rgba(255,255,255,0.3)" strokeWidth="2" />
            <path d="M 70 180 L 200 80 L 330 180 Z" fill="rgba(255,255,255,0.08)" stroke="rgba(255,255,255,0.3)" strokeWidth="2" />
            <line x1="140" y1="130" x2="140" y2="180" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <line x1="200" y1="80" x2="200" y2="180" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <line x1="260" y1="130" x2="260" y2="180" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <line x1="140" y1="180" x2="140" y2="280" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <line x1="200" y1="180" x2="200" y2="280" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <line x1="260" y1="180" x2="260" y2="280" stroke="rgba(255,255,255,0.2)" strokeWidth="1" />
            <circle cx="120" cy="250" r="15" fill="rgba(34,197,94,0.4)" />
            <circle cx="180" cy="260" r="18" fill="rgba(34,197,94,0.5)" />
            <circle cx="240" cy="255" r="16" fill="rgba(34,197,94,0.45)" />
            <circle cx="280" cy="250" r="14" fill="rgba(34,197,94,0.4)" />
            {/* Sensor nodes */}
            <circle cx="150" cy="140" r="6" fill="#22c55e" className="animate-pulse" />
            <circle cx="250" cy="140" r="6" fill="#22c55e" className="animate-pulse" style={{ animationDelay: "0.5s" }} />
            <circle cx="200" cy="220" r="6" fill="#22c55e" className="animate-pulse" style={{ animationDelay: "1s" }} />
            <line x1="150" y1="140" x2="200" y2="100" stroke="#22c55e" strokeWidth="1" strokeDasharray="3,3" opacity="0.5" />
            <line x1="250" y1="140" x2="200" y2="100" stroke="#22c55e" strokeWidth="1" strokeDasharray="3,3" opacity="0.5" />
            <line x1="200" y1="220" x2="200" y2="100" stroke="#22c55e" strokeWidth="1" strokeDasharray="3,3" opacity="0.5" />
          </svg>

          {/* Live metric chips */}
          {metrics.map((m) => {
            const Icon = m.icon;
            return (
              <div
                key={m.label}
                className={cn(
                  "animate-float absolute flex items-center gap-2.5 rounded-xl border border-white/20 bg-white/10 px-3 py-2 backdrop-blur-md",
                  m.position,
                )}
                style={{ animation: `float ${m.floatDuration} ease-in-out infinite` }}
              >
                <Icon className="h-4 w-4 shrink-0 text-green-300" />
                <div className="leading-tight">
                  <div className="font-mono text-sm font-semibold tabular-nums text-white">
                    {m.value.toFixed(m.decimals)}
                    <span className="ml-0.5 text-xs text-green-300">{m.unit}</span>
                  </div>
                  <div className="text-[10px] uppercase tracking-wide text-green-200/70">
                    {m.label}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Footer */}
      <div className="relative z-10 flex items-center gap-2 text-sm text-green-200/70">
        <span className="inline-block h-2 w-2 rounded-full bg-green-400 animate-pulse" />
        <span>Live sensor feed · simulated demo data</span>
      </div>
    </div>
  );
}
