"use client";

import { useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { useCreateThreshold, useUpdateThreshold } from "@/hooks/use-thresholds";
import { METRICS, type Threshold } from "@/lib/types";
import { METRIC_META } from "@/lib/metrics";
import { ApiError } from "@/lib/api";

const nullableNumber = z
  .number({ message: "Enter a number" })
  .nullable();

const schema = z
  .object({
    metric: z.enum(METRICS),
    minValue: nullableNumber,
    maxValue: nullableNumber,
    isEnabled: z.boolean(),
  })
  .refine((v) => v.minValue !== null || v.maxValue !== null, {
    message: "Set at least a min or a max",
    path: ["minValue"],
  })
  .refine(
    (v) => v.minValue === null || v.maxValue === null || v.minValue < v.maxValue,
    { message: "Min must be less than max", path: ["maxValue"] },
  );

type Values = z.infer<typeof schema>;

export function ThresholdFormDialog({
  greenhouseId,
  threshold,
  trigger,
}: {
  greenhouseId: string;
  threshold?: Threshold;
  trigger: React.ReactNode;
}) {
  const isEdit = Boolean(threshold);
  const [open, setOpen] = useState(false);
  const create = useCreateThreshold(greenhouseId);
  const update = useUpdateThreshold(greenhouseId);
  const pending = create.isPending || update.isPending;

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      metric: threshold?.metric ?? "temperature",
      minValue: threshold?.minValue ?? null,
      maxValue: threshold?.maxValue ?? null,
      isEnabled: threshold?.isEnabled ?? true,
    },
  });

  async function onSubmit(values: Values) {
    try {
      const input = { ...values, greenhouseId };
      if (isEdit && threshold) {
        await update.mutateAsync({ id: threshold.id, input });
        toast.success("Threshold updated");
      } else {
        await create.mutateAsync(input);
        toast.success("Threshold created");
      }
      setOpen(false);
    } catch (e) {
      toast.error(
        e instanceof ApiError && e.status === 409
          ? "This greenhouse already has a threshold for that metric. Edit it instead."
          : "Could not save the threshold. Please try again.",
      );
    }
  }

  const numberField = (name: "minValue" | "maxValue") =>
    register(name, {
      setValueAs: (v) => (v === "" || v === null ? null : Number(v)),
    });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <DialogHeader>
            <DialogTitle>{isEdit ? "Edit threshold" : "New threshold"}</DialogTitle>
            <DialogDescription>
              An alert is raised when a reading goes below the min or above the
              max for this metric.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="metric">Metric</Label>
              <Controller
                control={control}
                name="metric"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="metric" className="w-full">
                      <SelectValue placeholder="Select a metric" />
                    </SelectTrigger>
                    <SelectContent>
                      {METRICS.map((m) => {
                        const { label, icon: Icon } = METRIC_META[m];
                        return (
                          <SelectItem key={m} value={m}>
                            <Icon className="h-4 w-4 text-muted-foreground" />
                            {label}
                          </SelectItem>
                        );
                      })}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="minValue">Min</Label>
                <Input
                  id="minValue"
                  type="number"
                  step="any"
                  placeholder="—"
                  aria-invalid={!!errors.minValue}
                  {...numberField("minValue")}
                />
                {errors.minValue && (
                  <p className="text-xs text-destructive">
                    {errors.minValue.message}
                  </p>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="maxValue">Max</Label>
                <Input
                  id="maxValue"
                  type="number"
                  step="any"
                  placeholder="—"
                  aria-invalid={!!errors.maxValue}
                  {...numberField("maxValue")}
                />
                {errors.maxValue && (
                  <p className="text-xs text-destructive">
                    {errors.maxValue.message}
                  </p>
                )}
              </div>
            </div>

            <div className="flex items-center justify-between rounded-lg border p-3">
              <div>
                <Label htmlFor="isEnabled">Enabled</Label>
                <p className="text-xs text-muted-foreground">
                  Only enabled thresholds raise alerts.
                </p>
              </div>
              <Controller
                control={control}
                name="isEnabled"
                render={({ field }) => (
                  <Switch
                    id="isEnabled"
                    checked={field.value}
                    onCheckedChange={field.onChange}
                  />
                )}
              />
            </div>
          </div>

          <DialogFooter>
            <Button type="submit" disabled={pending}>
              {pending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? "Save changes" : "Create threshold"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
