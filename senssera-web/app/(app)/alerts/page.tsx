"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Bell, Check, CheckCheck } from "lucide-react";
import {
  useAlerts,
  useAcknowledgeAlert,
  useResolveAlert,
} from "@/hooks/use-alerts";
import { useGreenhouses } from "@/hooks/use-greenhouses";
import { useTelemetry } from "@/hooks/use-telemetry";
import { METRIC_META } from "@/lib/metrics";
import type { Alert, AlertSeverity, AlertStatus, Metric } from "@/lib/types";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { LiveIndicator } from "@/components/app/live-indicator";

const PAGE_SIZE = 20;
const STATUS_OPTIONS = ["open", "acknowledged", "resolved"] as const;

export default function AlertsPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState<string | undefined>(undefined);
  const [greenhouseId, setGreenhouseId] = useState<string | undefined>(undefined);
  const [page, setPage] = useState(1);

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
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Alerts</h1>
          <p className="text-sm text-muted-foreground">
            Threshold breaches across your greenhouses.
          </p>
        </div>
        <LiveIndicator connected={connected} />
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Select
          value={status ?? "all"}
          onValueChange={(v) => {
            setStatus(v === "all" ? undefined : v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-40">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {STATUS_OPTIONS.map((s) => (
              <SelectItem key={s} value={s} className="capitalize">
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={greenhouseId ?? "all"}
          onValueChange={(v) => {
            setGreenhouseId(v === "all" ? undefined : v);
            setPage(1);
          }}
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

      {isLoading && <Skeleton className="h-72 w-full rounded-xl" />}

      {isError && (
        <Card className="p-6 text-center text-sm text-muted-foreground">
          Could not load alerts. Please refresh.
        </Card>
      )}

      {data && data.items.length === 0 && (
        <Card className="flex flex-col items-center gap-3 p-10 text-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-success/10">
            <Bell className="h-6 w-6 text-success" />
          </div>
          <div>
            <p className="font-medium">No alerts</p>
            <p className="text-sm text-muted-foreground">
              Nothing is out of range with the current filters.
            </p>
          </div>
        </Card>
      )}

      {data && data.items.length > 0 && (
        <Card className="overflow-hidden py-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Severity</TableHead>
                  <TableHead>Metric</TableHead>
                  <TableHead>Greenhouse</TableHead>
                  <TableHead>Value</TableHead>
                  <TableHead>Triggered</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((alert) => (
                  <AlertRow
                    key={alert.id}
                    alert={alert}
                    greenhouse={greenhouseName.get(alert.greenhouseId)}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
        </Card>
      )}

      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {from}–{to} of {total}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page === 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={to >= total}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function AlertRow({ alert, greenhouse }: { alert: Alert; greenhouse?: string }) {
  const meta = METRIC_META[alert.metric as Metric];
  const Icon = meta?.icon;
  const ack = useAcknowledgeAlert();
  const resolve = useResolveAlert();

  async function run(
    action: typeof ack | typeof resolve,
    label: string,
  ) {
    try {
      await action.mutateAsync(alert.id);
      toast.success(label);
    } catch {
      toast.error("Action failed. Please try again.");
    }
  }

  return (
    <TableRow>
      <TableCell>
        <SeverityBadge severity={alert.severity} />
      </TableCell>
      <TableCell>
        <span className="flex items-center gap-2 font-medium">
          {Icon && <Icon className="h-4 w-4 text-muted-foreground" />}
          {meta?.label ?? alert.metric}
        </span>
      </TableCell>
      <TableCell className="text-muted-foreground">
        {greenhouse ?? "—"}
      </TableCell>
      <TableCell className="tabular-nums">
        {alert.triggeredValue}
        {meta?.unit}
      </TableCell>
      <TableCell className="text-muted-foreground">
        {new Date(alert.triggeredAt).toLocaleString([], {
          month: "short",
          day: "numeric",
          hour: "2-digit",
          minute: "2-digit",
        })}
      </TableCell>
      <TableCell>
        <StatusBadge status={alert.status} />
      </TableCell>
      <TableCell className="text-right">
        <div className="flex justify-end gap-1">
          {alert.status === "open" && (
            <Button
              variant="ghost"
              size="icon"
              aria-label="Acknowledge"
              disabled={ack.isPending}
              onClick={() => run(ack, "Alert acknowledged")}
            >
              <Check className="h-4 w-4" />
            </Button>
          )}
          {alert.status !== "resolved" && (
            <Button
              variant="ghost"
              size="icon"
              aria-label="Resolve"
              disabled={resolve.isPending}
              onClick={() => run(resolve, "Alert resolved")}
            >
              <CheckCheck className="h-4 w-4" />
            </Button>
          )}
        </div>
      </TableCell>
    </TableRow>
  );
}

function SeverityBadge({ severity }: { severity: AlertSeverity }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium capitalize",
        severity === "critical"
          ? "border-destructive/30 bg-destructive/10 text-destructive"
          : "border-warning/30 bg-warning/10 text-warning",
      )}
    >
      {severity}
    </span>
  );
}

function StatusBadge({ status }: { status: AlertStatus }) {
  const styles: Record<AlertStatus, string> = {
    open: "border-destructive/30 bg-destructive/10 text-destructive",
    acknowledged: "border-warning/30 bg-warning/10 text-warning",
    resolved: "border-success/30 bg-success/10 text-success",
  };
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium capitalize",
        styles[status],
      )}
    >
      {status}
    </span>
  );
}
