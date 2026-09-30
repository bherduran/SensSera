"use client";

import { toast } from "sonner";
import { Plus, Pencil, Trash2 } from "lucide-react";
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
      <div className="flex items-end justify-between gap-4 border-b border-dashed pt-4 pb-2">
        <div>
          <p className="label-caps">Safe bands</p>
          <h2 className="font-serif text-2xl leading-tight">Alert thresholds</h2>
        </div>
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

      {isLoading && <Skeleton className="h-32 w-full rounded-md" />}

      {isError && (
        <p className="font-serif italic text-alert-text">Thresholds couldn’t be loaded.</p>
      )}

      {data && data.length === 0 && (
        <Card className="items-center p-8 text-center">
          <p className="font-serif text-xl">No safe bands yet.</p>
          <p className="text-sm text-muted-foreground">Add a threshold to start getting alerts.</p>
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
      <TableCell className="font-mono text-sm">
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
