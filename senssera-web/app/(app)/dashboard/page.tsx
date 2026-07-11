"use client";

import Link from "next/link";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";
import { Sprout, Cpu, ChevronRight, Bell } from "lucide-react";
import { useDashboard, dashboardKey } from "@/hooks/use-dashboard";
import { useTelemetry } from "@/hooks/use-telemetry";
import { METRIC_META } from "@/lib/metrics";
import type {
  DashboardSummary,
  GreenhouseSummary,
  MetricCurrent,
  Metric,
} from "@/lib/types";
import { cn } from "@/lib/utils";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

export default function DashboardPage() {
  const qc = useQueryClient();
  const { data, isLoading, isError } = useDashboard();

  const { connected } = useTelemetry({
    onReading: (event) => {
      qc.setQueryData<DashboardSummary>(dashboardKey, (prev) =>
        prev
          ? {
              greenhouses: prev.greenhouses.map((g) =>
                g.greenhouseId === event.greenhouseId
                  ? { ...g, metrics: upsertMetric(g.metrics, event.metric, event.value) }
                  : g,
              ),
            }
          : prev,
      );
    },
    onAlert: (event) => {
      const meta = METRIC_META[event.metric];
      toast.warning(`Alert · ${meta?.label ?? event.metric}`, {
        description: `A ${event.severity} threshold was breached.`,
      });
      qc.setQueryData<DashboardSummary>(dashboardKey, (prev) =>
        prev
          ? {
              greenhouses: prev.greenhouses.map((g) =>
                g.greenhouseId === event.greenhouseId
                  ? { ...g, activeAlerts: g.activeAlerts + 1 }
                  : g,
              ),
            }
          : prev,
      );
    },
  });

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            Live readings across all your greenhouses.
          </p>
        </div>
        <LiveIndicator connected={connected} />
      </div>

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-48 w-full rounded-xl" />
          ))}
        </div>
      )}

      {isError && (
        <Card className="p-6 text-center text-sm text-muted-foreground">
          Could not load the dashboard. Please refresh.
        </Card>
      )}

      {data && data.greenhouses.length === 0 && (
        <Card className="flex flex-col items-center gap-3 p-10 text-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <Sprout className="h-6 w-6 text-primary" />
          </div>
          <div>
            <p className="font-medium">Nothing to show yet</p>
            <p className="text-sm text-muted-foreground">
              Add a greenhouse and start ingesting readings to see live data here.
            </p>
          </div>
          <Link
            href="/greenhouses"
            className="text-sm font-medium text-primary hover:underline"
          >
            Go to greenhouses
          </Link>
        </Card>
      )}

      {data && data.greenhouses.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.greenhouses.map((g) => (
            <GreenhouseCard key={g.greenhouseId} greenhouse={g} />
          ))}
        </div>
      )}
    </div>
  );
}

function LiveIndicator({ connected }: { connected: boolean }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium",
        connected
          ? "border-success/30 bg-success/10 text-success"
          : "border-border bg-muted text-muted-foreground",
      )}
    >
      <span className="relative flex h-2 w-2">
        {connected && (
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-success opacity-75" />
        )}
        <span
          className={cn(
            "relative inline-flex h-2 w-2 rounded-full",
            connected ? "bg-success" : "bg-muted-foreground/50",
          )}
        />
      </span>
      {connected ? "Live" : "Offline"}
    </span>
  );
}

function GreenhouseCard({ greenhouse }: { greenhouse: GreenhouseSummary }) {
  return (
    <Card className="group flex flex-col gap-0 overflow-hidden py-0 transition-colors hover:border-primary/40">
      <CardHeader className="flex-row items-center gap-3 space-y-0 p-5">
        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
          <Sprout className="h-5 w-5 text-primary" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold">{greenhouse.name}</p>
          <p className="flex items-center gap-1 text-xs text-muted-foreground">
            <Cpu className="h-3 w-3" />
            {greenhouse.deviceCount} device{greenhouse.deviceCount === 1 ? "" : "s"}
          </p>
        </div>
        {greenhouse.activeAlerts > 0 && (
          <Badge variant="destructive" className="gap-1">
            <Bell className="h-3 w-3" />
            {greenhouse.activeAlerts}
          </Badge>
        )}
      </CardHeader>

      <CardContent className="flex-1 px-5 pb-4">
        {greenhouse.metrics.length === 0 ? (
          <p className="text-sm text-muted-foreground">No readings yet.</p>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            {greenhouse.metrics.map((m) => (
              <MetricChip key={m.metric} metric={m} />
            ))}
          </div>
        )}
      </CardContent>

      <div className="border-t bg-muted/30 px-5 py-2.5">
        <Link
          href={`/greenhouses/${greenhouse.greenhouseId}`}
          className="inline-flex items-center text-sm font-medium text-primary hover:underline"
        >
          View greenhouse
          <ChevronRight className="ml-0.5 h-4 w-4 transition-transform group-hover:translate-x-0.5" />
        </Link>
      </div>
    </Card>
  );
}

function MetricChip({ metric }: { metric: MetricCurrent }) {
  const meta = METRIC_META[metric.metric as Metric];
  const Icon = meta?.icon;
  return (
    <div className="flex items-center gap-2 rounded-lg border bg-card p-2.5">
      {Icon && (
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-primary/10">
          <Icon className="h-4 w-4 text-primary" />
        </div>
      )}
      <div className="min-w-0">
        <p className="truncate text-xs text-muted-foreground">
          {meta?.label ?? metric.metric}
        </p>
        <p className="font-semibold tabular-nums">
          {metric.current}
          <span className="ml-0.5 text-xs font-normal text-muted-foreground">
            {meta?.unit}
          </span>
        </p>
      </div>
    </div>
  );
}

function upsertMetric(
  metrics: MetricCurrent[],
  metric: Metric,
  value: number,
): MetricCurrent[] {
  const current = Math.round(value * 100) / 100;
  if (metrics.some((m) => m.metric === metric)) {
    return metrics.map((m) => (m.metric === metric ? { ...m, current } : m));
  }
  return [...metrics, { metric, current }].sort((a, b) =>
    a.metric.localeCompare(b.metric),
  );
}
