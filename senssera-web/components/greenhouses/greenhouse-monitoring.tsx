"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useGreenhouseDetail } from "@/hooks/use-dashboard";
import { useGreenhouseReadings } from "@/hooks/use-readings";
import { useTelemetry } from "@/hooks/use-telemetry";
import { useThresholds } from "@/hooks/use-thresholds";
import { METRIC_META } from "@/lib/metrics";
import type { Alert, Metric, MetricSummary } from "@/lib/types";
import { Skeleton } from "@/components/ui/skeleton";
import { MetricChart, type ChartBand, type ChartPoint } from "@/components/charts/metric-chart";
import { BandGauge } from "@/components/notebook/band-gauge";

const round2 = (v: number) => Math.round(v * 100) / 100;
const LIVE_CAP = 120;

export function GreenhouseMonitoring({ greenhouseId }: { greenhouseId: string }) {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useGreenhouseDetail(greenhouseId);
  const { data: thresholds } = useThresholds(greenhouseId);
  const bands = new Map<string, ChartBand>(
    (thresholds ?? [])
      .filter((t) => t.isEnabled)
      .map((t) => [t.metric, { min: t.minValue ?? null, max: t.maxValue ?? null }]),
  );
  const [liveByMetric, setLiveByMetric] = useState<
    Record<string, ChartPoint[]>
  >({});

  useTelemetry({
    onReading: (event) => {
      if (event.greenhouseId !== greenhouseId) return;
      setLiveByMetric((prev) => {
        const arr = prev[event.metric] ?? [];
        const next = [
          ...arr,
          { t: new Date(event.recordedAt).getTime(), value: round2(event.value) },
        ].slice(-LIVE_CAP);
        return { ...prev, [event.metric]: next };
      });
    },
    onAlert: (event) => {
      if (event.greenhouseId !== greenhouseId) return;
      const meta = METRIC_META[event.metric];
      toast.warning(`Alert · ${meta?.label ?? event.metric}`, {
        description: `A ${event.severity} threshold was breached.`,
      });
      qc.invalidateQueries({ queryKey: ["dashboard", "greenhouse", greenhouseId] });
    },
  });

  if (isLoading) {
    return (
      <div className="grid gap-5 md:grid-cols-2">
        {Array.from({ length: 2 }).map((_, i) => (
          <Skeleton key={i} className="h-72 w-full rounded-md" />
        ))}
      </div>
    );
  }

  if (isError || !data) {
    return (
      <p className="font-serif text-lg italic text-alert-text">
        Monitoring data couldn’t be loaded. Refresh the page to try again.
      </p>
    );
  }

  if (data.metrics.length === 0) {
    return (
      <div className="rounded-md border bg-card p-10 text-center paper-shadow">
        <p className="font-serif text-2xl">No readings yet.</p>
        <p className="mt-2 text-sm text-muted-foreground">
          Once devices start sending data, live charts appear here.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {data.activeAlerts.length > 0 && <AlertStrip alerts={data.activeAlerts} />}
      <div className="grid gap-5 md:grid-cols-2">
        {data.metrics.map((summary) => (
          <MetricCard
            key={summary.metric}
            greenhouseId={greenhouseId}
            summary={summary}
            band={bands.get(summary.metric)}
            live={liveByMetric[summary.metric] ?? []}
          />
        ))}
      </div>
    </div>
  );
}

function MetricCard({
  greenhouseId,
  summary,
  band,
  live,
}: {
  greenhouseId: string;
  summary: MetricSummary;
  band?: ChartBand;
  live: ChartPoint[];
}) {
  const metric = summary.metric as Metric;
  const meta = METRIC_META[metric];
  const { data } = useGreenhouseReadings(greenhouseId, metric);

  const series = useMemo<ChartPoint[]>(() => {
    const history: ChartPoint[] = (data?.points ?? []).map((p) => ({
      t: new Date(p.periodStart).getTime(),
      value: round2(p.avg),
    }));
    return [...history, ...live].sort((a, b) => a.t - b.t).slice(-LIVE_CAP);
  }, [data, live]);

  // Latest live reading overrides the rollup "current" for a truly live headline.
  const current = live.length > 0 ? live[live.length - 1].value : summary.current;

  return (
    <section className="flex flex-col gap-4 rounded-md border bg-card p-5 paper-shadow">
      <BandGauge metric={metric} value={current} min={band?.min ?? null} max={band?.max ?? null} />
      <div>
        <p className="label-caps mb-1">Last 24 hours</p>
        <MetricChart data={series} unit={meta?.unit} band={band} />
      </div>
      <dl className="grid grid-cols-3 gap-2 border-t border-dashed pt-3">
        <Stat label="Min" value={summary.min24h} unit={meta?.unit} />
        <Stat label="Avg" value={summary.avg24h} unit={meta?.unit} />
        <Stat label="Max" value={summary.max24h} unit={meta?.unit} />
      </dl>
    </section>
  );
}

const statFmt = new Intl.NumberFormat("en-US", { maximumFractionDigits: 1 });

function Stat({ label, value, unit }: { label: string; value: number; unit?: string }) {
  return (
    <div>
      <dt className="label-caps">{label}</dt>
      <dd className="mt-0.5 font-mono text-sm">
        {statFmt.format(value)}
        <span className="ml-1 text-muted-foreground">{unit}</span>
      </dd>
    </div>
  );
}

function AlertStrip({ alerts }: { alerts: Alert[] }) {
  return (
    <section className="border-l-2 border-alert bg-alert/[0.07] px-4 py-3">
      <p className="label-caps text-alert-text">
        {alerts.length} open alert{alerts.length === 1 ? "" : "s"}
      </p>
      <ul className="mt-1.5 flex flex-wrap gap-x-6 gap-y-1 font-mono text-sm">
        {alerts.map((a) => {
          const meta = METRIC_META[a.metric];
          return (
            <li key={a.id} className="flex items-center gap-2">
              <span
                className={a.severity === "critical" ? "size-2 rounded-full bg-alert" : "size-2 rounded-full border border-alert"}
                aria-label={a.severity}
              />
              {meta?.label ?? a.metric} {statFmt.format(a.triggeredValue)} {meta?.unit}
              <span className="text-muted-foreground">· {a.severity}</span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
