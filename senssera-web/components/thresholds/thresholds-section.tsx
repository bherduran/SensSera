"use client";

import { toast } from "sonner";
import { Plus, Pencil, Trash2, BellRing } from "lucide-react";
import { useThresholds, useDeleteThreshold } from "@/hooks/use-thresholds";
import { METRIC_META } from "@/lib/metrics";
import type { Metric, Threshold } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { ThresholdFormDialog } from "@/components/thresholds/threshold-form-dialog";

const fmt = (v: number | null) => (v === null ? "—" : v);

export function ThresholdsSection({ greenhouseId }: { greenhouseId: string }) {
  const { data, isLoading, isError } = useThresholds(greenhouseId);

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-lg font-semibold">Alert thresholds</h2>
        <ThresholdFormDialog
          greenhouseId={greenhouseId}
          trigger={
            <Button variant="outline" size="sm">
              <Plus className="mr-1 h-4 w-4" />
              Add threshold
            </Button>
          }
        />
      </div>

      {isLoading && <Skeleton className="h-32 w-full rounded-xl" />}

      {isError && (
        <Card className="p-6 text-center text-sm text-muted-foreground">
          Could not load thresholds.
        </Card>
      )}

      {data && data.length === 0 && (
        <Card className="flex flex-col items-center gap-2 p-8 text-center">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
            <BellRing className="h-5 w-5 text-primary" />
          </div>
          <p className="text-sm text-muted-foreground">
            No thresholds yet — add one to start getting alerts.
          </p>
        </Card>
      )}

      {data && data.length > 0 && (
        <Card className="overflow-hidden py-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Metric</TableHead>
                <TableHead>Range</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.map((t) => (
                <ThresholdRow key={t.id} threshold={t} greenhouseId={greenhouseId} />
              ))}
            </TableBody>
          </Table>
        </Card>
      )}
    </section>
  );
}

function ThresholdRow({
  threshold,
  greenhouseId,
}: {
  threshold: Threshold;
  greenhouseId: string;
}) {
  const meta = METRIC_META[threshold.metric as Metric];
  const Icon = meta?.icon;
  const del = useDeleteThreshold(greenhouseId);

  async function handleDelete() {
    try {
      await del.mutateAsync(threshold.id);
      toast.success("Threshold deleted");
    } catch {
      toast.error("Could not delete the threshold.");
    }
  }

  return (
    <TableRow>
      <TableCell>
        <span className="flex items-center gap-2 font-medium">
          {Icon && <Icon className="h-4 w-4 text-muted-foreground" />}
          {meta?.label ?? threshold.metric}
        </span>
      </TableCell>
      <TableCell className="tabular-nums text-muted-foreground">
        {fmt(threshold.minValue)} – {fmt(threshold.maxValue)}
        {meta?.unit ? <span className="ml-1">{meta.unit}</span> : null}
      </TableCell>
      <TableCell>
        <span className="inline-flex items-center gap-1.5 text-sm">
          <span
            className={
              threshold.isEnabled
                ? "inline-block h-2 w-2 rounded-full bg-success"
                : "inline-block h-2 w-2 rounded-full bg-muted-foreground/50"
            }
          />
          {threshold.isEnabled ? "Enabled" : "Disabled"}
        </span>
      </TableCell>
      <TableCell className="text-right">
        <div className="flex justify-end gap-1">
          <ThresholdFormDialog
            greenhouseId={greenhouseId}
            threshold={threshold}
            trigger={
              <Button variant="ghost" size="icon" aria-label="Edit threshold">
                <Pencil className="h-4 w-4" />
              </Button>
            }
          />
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Delete threshold"
                className="text-muted-foreground hover:text-destructive"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete threshold?</AlertDialogTitle>
                <AlertDialogDescription>
                  This removes the {meta?.label ?? threshold.metric} threshold.
                  Existing alerts are kept.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={handleDelete}
                  className="bg-destructive text-white hover:bg-destructive/90"
                >
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </TableCell>
    </TableRow>
  );
}
