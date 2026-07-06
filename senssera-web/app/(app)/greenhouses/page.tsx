"use client";

import Link from "next/link";
import { toast } from "sonner";
import {
  Sprout,
  Plus,
  MapPin,
  Pencil,
  Trash2,
  ChevronRight,
} from "lucide-react";
import { useAuth } from "@/lib/auth";
import { useGreenhouses, useDeleteGreenhouse } from "@/hooks/use-greenhouses";
import type { Greenhouse } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
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
import { GreenhouseFormDialog } from "@/components/greenhouses/greenhouse-form-dialog";

export default function GreenhousesPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { data, isLoading, isError } = useGreenhouses();

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Greenhouses</h1>
          <p className="text-sm text-muted-foreground">
            Manage your greenhouses and their devices.
          </p>
        </div>
        {isAdmin && (
          <GreenhouseFormDialog
            trigger={
              <Button>
                <Plus className="mr-1 h-4 w-4" />
                New greenhouse
              </Button>
            }
          />
        )}
      </div>

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-40 w-full rounded-xl" />
          ))}
        </div>
      )}

      {isError && (
        <Card className="p-6 text-center text-sm text-muted-foreground">
          Could not load greenhouses. Please refresh.
        </Card>
      )}

      {data && data.length === 0 && (
        <Card className="flex flex-col items-center gap-3 p-10 text-center">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <Sprout className="h-6 w-6 text-primary" />
          </div>
          <div>
            <p className="font-medium">No greenhouses yet</p>
            <p className="text-sm text-muted-foreground">
              Create your first greenhouse to start monitoring.
            </p>
          </div>
          {isAdmin && (
            <GreenhouseFormDialog
              trigger={
                <Button>
                  <Plus className="mr-1 h-4 w-4" />
                  New greenhouse
                </Button>
              }
            />
          )}
        </Card>
      )}

      {data && data.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.map((g) => (
            <GreenhouseCard key={g.id} greenhouse={g} isAdmin={isAdmin} />
          ))}
        </div>
      )}
    </div>
  );
}

function GreenhouseCard({
  greenhouse,
  isAdmin,
}: {
  greenhouse: Greenhouse;
  isAdmin: boolean;
}) {
  const del = useDeleteGreenhouse();

  async function handleDelete() {
    try {
      await del.mutateAsync(greenhouse.id);
      toast.success("Greenhouse deleted");
    } catch {
      toast.error("Could not delete the greenhouse.");
    }
  }

  return (
    <Card className="group gap-0 overflow-hidden py-0 transition-colors hover:border-primary/40">
      <CardHeader className="flex-row items-center gap-3 space-y-0 p-5">
        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
          <Sprout className="h-5 w-5 text-primary" />
        </div>
        <div className="min-w-0">
          <p className="truncate font-semibold">{greenhouse.name}</p>
          <p className="flex items-center gap-1 truncate text-xs text-muted-foreground">
            <MapPin className="h-3 w-3 shrink-0" />
            {greenhouse.location || "No location set"}
          </p>
        </div>
      </CardHeader>

      <CardContent className="px-5 pb-4">
        <Link
          href={`/greenhouses/${greenhouse.id}`}
          className="inline-flex items-center text-sm font-medium text-primary hover:underline"
        >
          View devices
          <ChevronRight className="ml-0.5 h-4 w-4 transition-transform group-hover:translate-x-0.5" />
        </Link>
      </CardContent>

      {isAdmin && (
        <CardFooter className="justify-end gap-1 border-t bg-muted/30 px-3 py-2">
          <GreenhouseFormDialog
            greenhouse={greenhouse}
            trigger={
              <Button variant="ghost" size="icon" aria-label="Edit">
                <Pencil className="h-4 w-4" />
              </Button>
            }
          />
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Delete"
                className="text-muted-foreground hover:text-destructive"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete “{greenhouse.name}”?</AlertDialogTitle>
                <AlertDialogDescription>
                  This permanently removes the greenhouse and its devices,
                  readings, and alerts. This cannot be undone.
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
        </CardFooter>
      )}
    </Card>
  );
}
