"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { AlertTriangle, Activity } from "lucide-react";
import { useGreenhouseDetail } from "@/hooks/use-dashboard";
import { useGreenhouseReadings } from "@/hooks/use-readings";
import { useTelemetry } from "@/hooks/use-telemetry";
import { METRIC_META } from "@/lib/metrics";
import type { Alert, Metric, MetricSummary } from "@/lib/types";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { MetricChart, type ChartPoint } from "@/components/charts/metric-chart";

const round2 = (v: number) => Math.round(v * 100) / 100;
const LIVE_CAP = 120;

export function GreenhouseMonitoring({ greenhouseId }: { greenhouseId: string }) {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useGreenhouseDetail(greenhouseId);
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
      <div className="grid gap-4 md:grid-cols-2">
        {Array.from({ length: 2 }).map((_, i) => (
          <Skeleton key={i} className="h-64 w-full rounded-xl" />
        ))}
      </div>
    );
  }

  if (isError || !data) {
    return (
      <Card className="p-6 text-center text-sm text-muted-foreground">
        Could not load monitoring data. Please refresh.
      </Card>
    );
  }

  if (data.metrics.length === 0) {
    return (
      <Card className="flex flex-col items-center gap-3 p-10 text-center">
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
          <Activity className="h-6 w-6 text-primary" />
        </div>
        <div>
          <p className="font-medium">No readings yet</p>
          <p className="text-sm text-muted-foreground">
            Once devices start ingesting, live charts appear here.
          </p>
        </div>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {data.activeAlerts.length > 0 && <AlertStrip alerts={data.activeAlerts} />}
      <div className="grid gap-4 md:grid-cols-2">
        {data.metrics.map((summary) => (
          <MetricCard
            key={summary.metric}
            greenhouseId={greenhouseId}
            summary={summary}
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
  live,
}: {
  greenhouseId: string;
  summary: MetricSummary;
  live: ChartPoint[];
}) {
  const metric = summary.metric as Metric;
  const meta = METRIC_META[metric];
  const Icon = meta?.icon;
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
    <Card className="flex flex-col gap-3 p-5">
      <div className="flex items-center gap-2">
        {Icon && (
          <div className="flex h-8 w-8 items-center justify-center rounded-md bg-primary/10">
            <Icon className="h-4 w-4 text-primary" />
          </div>
        )}
        <div className="min-w-0">
          <p className="text-sm font-medium">{meta?.label ?? metric}</p>
          <p className="text-xs text-muted-foreground">Last 24 hours</p>
        </div>
        <div className="ml-auto text-right">
          <p className="text-2xl font-bold tabular-nums leading-none">
            {current}
            <span className="ml-0.5 text-sm font-normal text-muted-foreground">
              {meta?.unit}
            </span>
          </p>
        </div>
      </div>

      <MetricChart data={series} unit={meta?.unit} />

      <div className="grid grid-cols-3 gap-2 border-t pt-3 text-center">
        <Stat label="Min" value={summary.min24h} unit={meta?.unit} />
        <Stat label="Avg" value={summary.avg24h} unit={meta?.unit} />
        <Stat label="Max" value={summary.max24h} unit={meta?.unit} />
      </div>
    </Card>
  );
}

function Stat({
  label,
  value,
  unit,
}: {
  label: string;
  value: number;
  unit?: string;
}) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-semibold tabular-nums">
        {value}
        <span className="ml-0.5 text-xs font-normal text-muted-foreground">
          {unit}
        </span>
      </p>
    </div>
  );
}

function AlertStrip({ alerts }: { alerts: Alert[] }) {
  return (
    <Card className="border-destructive/30 bg-destructive/5 p-4">
      <div className="mb-2 flex items-center gap-2 text-sm font-medium text-destructive">
        <AlertTriangle className="h-4 w-4" />
        {alerts.length} active alert{alerts.length === 1 ? "" : "s"}
      </div>
      <div className="flex flex-wrap gap-2">
        {alerts.map((a) => {
          const meta = METRIC_META[a.metric];
          return (
            <Badge
              key={a.id}
              variant="outline"
              className="gap-1 border-destructive/30 bg-background font-normal"
            >
              {meta?.label ?? a.metric}
              <span className="tabular-nums">
                {a.triggeredValue}
                {meta?.unit}
              </span>
              <span className="text-muted-foreground">· {a.severity}</span>
            </Badge>
          );
        })}
      </div>
    </Card>
  );
}
