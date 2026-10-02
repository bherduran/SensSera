"use client";

import { Suspense, useMemo } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  useAlerts,
  useAcknowledgeAlert,
  useResolveAlert,
} from "@/hooks/use-alerts";
import { useGreenhouses } from "@/hooks/use-greenhouses";
import { useAuth } from "@/lib/auth";
import { useTelemetry } from "@/hooks/use-telemetry";
import { METRIC_META } from "@/lib/metrics";
import type { Alert, AlertSeverity, Metric } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SpecimenHeader } from "@/components/notebook/specimen-header";
import { LiveStamp } from "@/components/notebook/live-stamp";
import { ExplainAlertDialog } from "@/components/insights/explain-alert-dialog";

const PAGE_SIZE = 20;
const STATUS_OPTIONS = ["open", "acknowledged", "resolved"] as const;

// useSearchParams needs a Suspense boundary so the page can still be prerendered.
export default function AlertsPage() {
  return (
    <Suspense fallback={<Skeleton className="mx-auto h-72 max-w-6xl rounded-md" />}>
      <AlertsLedger />
    </Suspense>
  );
}

function AlertsLedger() {
  const qc = useQueryClient();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();

  // Filters live in the URL: they survive a refresh, and other pages can link to a filtered view.
  const statusParam = params.get("status");
  const status = STATUS_OPTIONS.find((s) => s === statusParam);
  const greenhouseId = params.get("greenhouse") ?? undefined;
  const page = Math.max(1, Number(params.get("page")) || 1);

  function setFilters(next: { status?: string; greenhouse?: string; page?: number }) {
    const merged = { status, greenhouse: greenhouseId, page, ...next };
    const query = new URLSearchParams();
    if (merged.status) query.set("status", merged.status);
    if (merged.greenhouse) query.set("greenhouse", merged.greenhouse);
    if (merged.page && merged.page > 1) query.set("page", String(merged.page));
    const qs = query.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
  }

  const greenhouses = useGreenhouses();
  const { data, isLoading, isError } = useAlerts({
    status,
    greenhouseId,
    page,
    pageSize: PAGE_SIZE,
  });

  const { connected } = useTelemetry({
    onAlert: (event) => {
      const meta = METRIC_META[event.metric];
      toast.warning(`Alert · ${meta?.label ?? event.metric}`, {
        description: `A ${event.severity} threshold was breached.`,
      });
      qc.invalidateQueries({ queryKey: ["alerts"] });
    },
  });

  const greenhouseName = useMemo(() => {
    const map = new Map<string, string>();
    greenhouses.data?.forEach((g) => map.set(g.id, g.name));
    return map;
  }, [greenhouses.data]);

  const total = data?.total ?? 0;
  const from = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const to = Math.min(page * PAGE_SIZE, total);

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <SpecimenHeader
        code="Ledger"
        subtitle={data ? `${total} entr${total === 1 ? "y" : "ies"}` : undefined}
        title="Alerts"
        aside={<LiveStamp connected={connected} />}
      />

      <div className="flex flex-wrap items-center justify-between gap-4">
        <nav aria-label="Filter by status" className="flex gap-5">
          {([undefined, ...STATUS_OPTIONS] as const).map((s) => {
            const active = status === s;
            return (
              <button
                key={s ?? "all"}
                type="button"
                aria-pressed={active}
                onClick={() => setFilters({ status: s, page: 1 })}
                className={cn(
                  "label-caps pb-1 transition-colors hover:text-foreground",
                  active && "border-b-[1.5px] border-foreground text-foreground",
                )}
              >
                {s ?? "all"}
              </button>
            );
          })}
        </nav>

        <Select
          value={greenhouseId ?? "all"}
          onValueChange={(v) => setFilters({ greenhouse: v === "all" ? undefined : v, page: 1 })}
        >
          <SelectTrigger className="w-56">
            <SelectValue placeholder="Greenhouse" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All greenhouses</SelectItem>
            {greenhouses.data?.map((g) => (
              <SelectItem key={g.id} value={g.id}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading && <Skeleton className="h-72 w-full rounded-md" />}

      {isError && (
        <p className="font-serif text-lg italic text-alert-text">
          The ledger couldn’t be loaded. Refresh the page to try again.
        </p>
      )}

      {data && data.items.length === 0 && (
        <div className="rounded-md border bg-card p-10 text-center paper-shadow">
          <p className="font-serif text-2xl">No entries.</p>
          <p className="mt-2 text-sm text-muted-foreground">
            Nothing has left its safe band with the current filters.
          </p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ol className="overflow-hidden rounded-md border bg-card paper-shadow">
          <li
            aria-hidden
            className="hidden grid-cols-[9rem_1rem_1fr_7rem_6.5rem_14.5rem] items-center gap-4 border-b px-5 py-2.5 md:grid"
          >
            <span className="label-caps">Logged</span>
            <span />
            <span className="label-caps">Reading</span>
            <span className="label-caps text-right">Value</span>
            <span className="label-caps">Status</span>
            <span />
          </li>
          {data.items.map((alert) => (
            <AlertEntry
              key={alert.id}
              alert={alert}
              greenhouse={greenhouseName.get(alert.greenhouseId)}
            />
          ))}
        </ol>
      )}

      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between font-mono text-xs text-muted-foreground">
          <span>
            {from}–{to} of {total}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page === 1} onClick={() => setFilters({ page: Math.max(1, page - 1) })}>
              ← Previous
            </Button>
            <Button variant="outline" size="sm" disabled={to >= total} onClick={() => setFilters({ page: page + 1 })}>
              Next →
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

const stamp = (iso: string) => {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const valueFmt = new Intl.NumberFormat("en-US", { maximumFractionDigits: 1 });

function AlertEntry({ alert, greenhouse }: { alert: Alert; greenhouse?: string }) {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const meta = METRIC_META[alert.metric as Metric];
  const ack = useAcknowledgeAlert();
  const resolve = useResolveAlert();

  async function run(action: typeof ack | typeof resolve, label: string) {
    try {
      await action.mutateAsync(alert.id);
      toast.success(label);
    } catch {
      toast.error("Action failed. Please try again.");
    }
  }

  const textAction = "font-mono text-xs underline decoration-dotted underline-offset-4 hover:text-foreground disabled:opacity-40";

  return (
    <li
      className={cn(
        "grid grid-cols-[1rem_1fr_auto] items-center gap-x-4 gap-y-1 border-b border-dashed px-5 py-3 last:border-b-0",
        "md:grid-cols-[9rem_1rem_1fr_7rem_6.5rem_14.5rem]",
        alert.status === "resolved" && "text-muted-foreground",
      )}
    >
      <span className="col-span-3 font-mono text-xs text-muted-foreground md:col-span-1">
        {stamp(alert.triggeredAt)}
      </span>
      <SeverityMark severity={alert.severity} />
      <span className="min-w-0">
        <span className="font-medium">{meta?.label ?? alert.metric}</span>
        <span className="text-muted-foreground"> · {greenhouse ?? "—"}</span>
      </span>
      <span className={cn("text-right font-mono", alert.status !== "resolved" && "text-alert-text")}>
        {valueFmt.format(alert.triggeredValue)}
        <span className="ml-1 text-muted-foreground">{meta?.unit}</span>
      </span>
      <span className="col-start-2 font-mono text-xs md:col-start-auto">{alert.status}</span>
      <span className="col-span-3 flex justify-end gap-4 text-muted-foreground md:col-span-1">
        <ExplainAlertDialog
          alertId={alert.id}
          trigger={<button type="button" className={textAction}>explain</button>}
        />
        {alert.status === "open" && (
          <button type="button" className={textAction} disabled={ack.isPending} onClick={() => run(ack, "Alert acknowledged")}>
            acknowledge
          </button>
        )}
        {isAdmin && alert.status !== "resolved" && (
          <button type="button" className={textAction} disabled={resolve.isPending} onClick={() => run(resolve, "Alert resolved")}>
            resolve
          </button>
        )}
      </span>
    </li>
  );
}

function SeverityMark({ severity }: { severity: AlertSeverity }) {
  return (
    <span
      title={severity}
      aria-label={severity}
      className={cn(
        "size-2.5 rounded-full",
        severity === "critical" ? "bg-alert" : "border-[1.5px] border-alert",
      )}
    />
  );
}
