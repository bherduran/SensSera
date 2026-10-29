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
import { useAddChannel } from "@/hooks/use-nodes";
import { ApiError } from "@/lib/api";
import { METRICS } from "@/lib/types";
import { METRIC_META } from "@/lib/metrics";

// Same rule as the API: the firmware sends this string with every reading.
const schema = z.object({
  channel: z
    .string()
    .min(1, "Channel is required")
    .max(32)
    .regex(/^[a-z0-9_]+$/, "Lowercase letters, digits and _ only"),
  name: z.string().trim().min(1, "Name is required").max(100),
  metric: z.enum(METRICS),
});
type Values = z.infer<typeof schema>;

const DEFAULTS: Values = { channel: "", name: "", metric: "soilMoisture" };

export function ChannelFormDialog({
  greenhouseId,
  nodeId,
  nodeName,
  trigger,
}: {
  greenhouseId: string;
  nodeId: string;
  nodeName: string;
  trigger: React.ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const add = useAddChannel(greenhouseId);

  const {
    register,
    handleSubmit,
    control,
    reset,
    setError,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: DEFAULTS,
  });

  async function onSubmit(values: Values) {
    try {
      await add.mutateAsync({ nodeId, input: values });
      toast.success(`Channel ${values.channel} added`);
      setOpen(false);
      reset(DEFAULTS);
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setError("channel", { message: "This board already has that channel" });
        return;
      }
      toast.error("Could not add the channel. Please try again.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <DialogHeader>
            <DialogTitle>New channel on {nodeName}</DialogTitle>
            <DialogDescription>
              One sensor on the board. The channel code must match what the
              firmware sends, e.g. <code className="font-mono">soil1</code>.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="channel-code">Channel code</Label>
              <Input
                id="channel-code"
                placeholder="soil1"
                className="font-mono"
                autoCapitalize="none"
                aria-invalid={!!errors.channel}
                {...register("channel")}
              />
              {errors.channel && (
                <p className="text-xs text-destructive">{errors.channel.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="channel-name">Name</Label>
              <Input
                id="channel-name"
                placeholder="Basil pot soil"
                aria-invalid={!!errors.name}
                {...register("name")}
              />
              {errors.name && (
                <p className="text-xs text-destructive">{errors.name.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="channel-metric">Metric</Label>
              <Controller
                control={control}
                name="metric"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="channel-metric" className="w-full">
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
            <Button type="submit" disabled={add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add channel
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
