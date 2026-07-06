"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
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
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  useCreateGreenhouse,
  useUpdateGreenhouse,
} from "@/hooks/use-greenhouses";
import type { Greenhouse } from "@/lib/types";

const schema = z.object({
  name: z.string().min(1, "Name is required").max(100),
  location: z.string().max(200).optional(),
});
type Values = z.infer<typeof schema>;

export function GreenhouseFormDialog({
  greenhouse,
  trigger,
}: {
  greenhouse?: Greenhouse;
  trigger: React.ReactNode;
}) {
  const isEdit = Boolean(greenhouse);
  const [open, setOpen] = useState(false);
  const create = useCreateGreenhouse();
  const update = useUpdateGreenhouse(greenhouse?.id ?? "");

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: greenhouse?.name ?? "",
      location: greenhouse?.location ?? "",
    },
  });

  const pending = create.isPending || update.isPending;

  async function onSubmit(values: Values) {
    const payload = { name: values.name, location: values.location ?? "" };
    try {
      if (isEdit) await update.mutateAsync(payload);
      else await create.mutateAsync(payload);
      toast.success(isEdit ? "Greenhouse updated" : "Greenhouse created");
      setOpen(false);
      if (!isEdit) reset({ name: "", location: "" });
    } catch {
      toast.error("Could not save the greenhouse. Please try again.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <DialogHeader>
            <DialogTitle>
              {isEdit ? "Edit greenhouse" : "New greenhouse"}
            </DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Update the greenhouse details."
                : "Add a greenhouse to start attaching devices."}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input
                id="name"
                placeholder="Block A — Tomatoes"
                aria-invalid={!!errors.name}
                {...register("name")}
              />
              {errors.name && (
                <p className="text-xs text-destructive">{errors.name.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="location">Location</Label>
              <Input
                id="location"
                placeholder="Antalya, Turkey (optional)"
                aria-invalid={!!errors.location}
                {...register("location")}
              />
              {errors.location && (
                <p className="text-xs text-destructive">
                  {errors.location.message}
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button type="submit" disabled={pending}>
              {pending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {isEdit ? "Save changes" : "Create greenhouse"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
