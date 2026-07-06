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
import { useCreateDevice } from "@/hooks/use-devices";
import { METRICS, type DeviceWithToken } from "@/lib/types";
import { METRIC_META } from "@/lib/metrics";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  metric: z.enum(METRICS),
});
type Values = z.infer<typeof schema>;

export function DeviceFormDialog({
  greenhouseId,
  trigger,
  onCreated,
}: {
  greenhouseId: string;
  trigger: React.ReactNode;
  onCreated: (device: DeviceWithToken) => void;
}) {
  const [open, setOpen] = useState(false);
  const create = useCreateDevice(greenhouseId);

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", metric: "temperature" },
  });

  async function onSubmit(values: Values) {
    try {
      const device = await create.mutateAsync({ ...values, greenhouseId });
      toast.success("Device created");
      setOpen(false);
      reset({ name: "", metric: "temperature" });
      onCreated(device);
    } catch {
      toast.error("Could not create the device. Please try again.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <DialogHeader>
            <DialogTitle>New device</DialogTitle>
            <DialogDescription>
              Each device measures a single metric and gets its own ingestion
              token.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input
                id="name"
                placeholder="Roof sensor #1"
                aria-invalid={!!errors.name}
                {...register("name")}
              />
              {errors.name && (
                <p className="text-xs text-destructive">{errors.name.message}</p>
              )}
            </div>

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
          </div>

          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Create device
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
