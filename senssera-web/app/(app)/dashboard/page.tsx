"use client";

import Link from "next/link";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";
import { useDashboard, dashboardKey } from "@/hooks/use-dashboard";
import { useTelemetry } from "@/hooks/use-telemetry";
import { useThresholds } from "@/hooks/use-thresholds";
import { bandLayout } from "@/lib/band";
import { METRIC_META } from "@/lib/metrics";
import type {
  DashboardSummary,
  GreenhouseSummary,
  MetricCurrent,
  Metric,
} from "@/lib/types";
import { Skeleton } from "@/components/ui/skeleton";
import { AskPanel } from "@/components/insights/ask-panel";
import { BandGauge } from "@/components/notebook/band-gauge";
import { SpecimenHeader } from "@/components/notebook/specimen-header";
import { LiveStamp } from "@/components/notebook/live-stamp";

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
      // Refetch the true count rather than optimistically incrementing — an
      // increment misses alerts that fired while the connection was down.
      qc.invalidateQueries({ queryKey: dashboardKey });
    },
  });

  const count = data?.greenhouses.length ?? 0;

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <SpecimenHeader
        code="Notebook"
        subtitle={data ? `${count} greenhouse${count === 1 ? "" : "s"}` : undefined}
        title="Dashboard"
        aside={<LiveStamp connected={connected} />}
      />

      <AskPanel />

      {isLoading && (
        <div className="space-y-6">
          {Array.from({ length: 2 }).map((_, i) => (
            <Skeleton key={i} className="h-72 w-full rounded-md" />
          ))}
        </div>
      )}

      {isError && (
        <p className="font-serif text-lg italic text-alert-text">
          The notebook couldn’t be loaded. Refresh the page to try again.
        </p>
      )}

      {data && data.greenhouses.length === 0 && (
        <div className="rounded-md border bg-card p-10 text-center paper-shadow">
          <p className="font-serif text-2xl">Nothing recorded yet.</p>
          <p className="mt-2 text-sm text-muted-foreground">
            Add a greenhouse and start sending readings; they will appear here live.
          </p>
          <Link href="/greenhouses" className="mt-4 inline-block font-serif italic underline underline-offset-4">
            Go to greenhouses →
          </Link>
        </div>
      )}

      {data && data.greenhouses.length > 0 && (
        <div className="space-y-6">
          {data.greenhouses.map((g, i) => (
            <GreenhouseSheet key={g.greenhouseId} greenhouse={g} index={i} />
          ))}
        </div>
      )}
    </div>
  );
}

function GreenhouseSheet({ greenhouse, index }: { greenhouse: GreenhouseSummary; index: number }) {
  const { data: thresholds } = useThresholds(greenhouse.greenhouseId);
  const bands = new Map(
    (thresholds ?? []).filter((t) => t.isEnabled).map((t) => [t.metric, t] as const),
  );

  // Out-of-band readings first, so the sheet leads with what needs attention.
  const metrics = greenhouse.metrics
    .map((m) => {
      const t = bands.get(m.metric as Metric);
      const min = t?.minValue ?? null;
      const max = t?.maxValue ?? null;
      const status = bandLayout({ value: m.current, min, max, metric: m.metric as Metric }).status;
      return { ...m, min, max, out: status === "over" || status === "under" };
    })
    .sort((a, b) => Number(b.out) - Number(a.out));

  return (
    <article className="rounded-md border bg-card p-5 paper-shadow sm:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-dashed pb-3">
        <div>
          <p className="label-caps">
            GH-{String(index + 1).padStart(2, "0")} · {greenhouse.deviceCount} device
            {greenhouse.deviceCount === 1 ? "" : "s"}
          </p>
          <h2 className="mt-1 font-serif text-[28px] leading-none">{greenhouse.name}</h2>
        </div>
        {greenhouse.activeAlerts > 0 ? (
          <Link
            href={`/alerts?status=open&greenhouse=${greenhouse.greenhouseId}`}
            className="inline-flex items-center gap-1.5 font-mono text-xs text-alert-text underline-offset-4 hover:underline"
          >
            <span className="size-2 rounded-full bg-alert" />
            {greenhouse.activeAlerts} open alert{greenhouse.activeAlerts === 1 ? "" : "s"}
          </Link>
        ) : (
          <span className="font-mono text-xs text-muted-foreground">no open alerts</span>
        )}
      </div>

      {metrics.length === 0 ? (
        <p className="py-6 font-serif italic text-muted-foreground">No readings recorded yet.</p>
      ) : (
        <div className="grid gap-x-8 gap-y-6 pt-5 sm:grid-cols-2 lg:grid-cols-3">
          {metrics.map((m) => (
            <BandGauge key={m.metric} size="sm" metric={m.metric as Metric} value={m.current} min={m.min} max={m.max} />
          ))}
        </div>
      )}

      <div className="mt-5 flex justify-end">
        <Link
          href={`/greenhouses/${greenhouse.greenhouseId}`}
          className="font-serif text-[15px] italic underline-offset-4 hover:underline"
        >
          Open this notebook →
        </Link>
      </div>
    </article>
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
