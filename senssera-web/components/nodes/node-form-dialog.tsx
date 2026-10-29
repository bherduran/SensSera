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
import { useCreateNode } from "@/hooks/use-nodes";
import type { NodeWithToken } from "@/lib/types";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required").max(200),
});
type Values = z.infer<typeof schema>;

export function NodeFormDialog({
  greenhouseId,
  trigger,
  onCreated,
}: {
  greenhouseId: string;
  trigger: React.ReactNode;
  onCreated: (node: NodeWithToken) => void;
}) {
  const [open, setOpen] = useState(false);
  const create = useCreateNode(greenhouseId);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "" },
  });

  async function onSubmit(values: Values) {
    try {
      const node = await create.mutateAsync({ ...values, greenhouseId });
      toast.success("Board registered");
      setOpen(false);
      reset({ name: "" });
      onCreated(node);
    } catch {
      toast.error("Could not register the board. Please try again.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate>
          <DialogHeader>
            <DialogTitle>New board</DialogTitle>
            <DialogDescription>
              A board (e.g. an ESP32) reports several sensors with one token.
              Add its sensors as channels after registering it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2 py-4">
            <Label htmlFor="node-name">Name</Label>
            <Input
              id="node-name"
              placeholder="esp32-balcony"
              aria-invalid={!!errors.name}
              {...register("name")}
            />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>

          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Register board
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
