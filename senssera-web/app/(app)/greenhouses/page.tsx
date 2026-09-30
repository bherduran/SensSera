"use client";

import Link from "next/link";
import { toast } from "sonner";
import { Plus, Pencil, Trash2 } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { useGreenhouses, useDeleteGreenhouse } from "@/hooks/use-greenhouses";
import type { Greenhouse } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
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
import { SpecimenHeader } from "@/components/notebook/specimen-header";

export default function GreenhousesPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const { data, isLoading, isError } = useGreenhouses();

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <SpecimenHeader
        code="Collection"
        subtitle={data ? `${data.length} specimen${data.length === 1 ? "" : "s"}` : undefined}
        title="Greenhouses"
        aside={
          isAdmin && (
            <GreenhouseFormDialog
              trigger={
                <Button>
                  <Plus className="mr-1 h-4 w-4" />
                  New greenhouse
                </Button>
              }
            />
          )
        }
      />

      {isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-40 w-full rounded-md" />
          ))}
        </div>
      )}

      {isError && (
        <p className="font-serif text-lg italic text-alert-text">
          Greenhouses couldn’t be loaded. Refresh the page to try again.
        </p>
      )}

      {data && data.length === 0 && (
        <Card className="items-center gap-3 p-10 text-center">
          <p className="font-serif text-2xl">The collection is empty.</p>
          <p className="text-sm text-muted-foreground">Create your first greenhouse to start monitoring.</p>
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
          {data.map((g, i) => (
            <GreenhouseCard key={g.id} greenhouse={g} index={i} isAdmin={isAdmin} />
          ))}
        </div>
      )}
    </div>
  );
}

function GreenhouseCard({
  greenhouse,
  index,
  isAdmin,
}: {
  greenhouse: Greenhouse;
  index: number;
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
    <Card className="gap-0 py-0">
      <Link href={`/greenhouses/${greenhouse.id}`} className="group block px-5 pt-5 pb-4">
        <p className="label-caps truncate">
          GH-{String(index + 1).padStart(2, "0")} · {greenhouse.location || "no location"}
        </p>
        <p className="mt-1.5 truncate font-serif text-[26px] leading-tight">{greenhouse.name}</p>
        <p className="mt-4 font-serif text-[15px] italic text-muted-foreground group-hover:text-foreground">
          Open notebook →
        </p>
      </Link>

      {isAdmin && (
        <div className="flex justify-end gap-1 border-t border-dashed px-3 py-1.5">
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
        </div>
      )}
    </Card>
  );
}
