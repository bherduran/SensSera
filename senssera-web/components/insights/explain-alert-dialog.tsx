"use client";

import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { insightErrorMessage, useExplainAlert } from "@/hooks/use-insights";
import { FieldNote } from "@/components/notebook/field-note";

export function ExplainAlertDialog({
  alertId,
  trigger,
}: {
  alertId: string;
  trigger: React.ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const explain = useExplainAlert();

  function onOpenChange(next: boolean) {
    setOpen(next);
    // Only pay for a model call when the grower actually opens the dialog.
    if (next && !explain.data) explain.mutate(alertId);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <p className="label-caps">Alert explanation</p>
          <DialogTitle className="font-serif text-2xl font-normal">What the readings say</DialogTitle>
          <DialogDescription>
            Interpreted from your real readings; the model only phrases the numbers it was given.
          </DialogDescription>
        </DialogHeader>

        {explain.isPending && <FieldNote pending />}

        {explain.isError && (
          <p className="py-4 text-sm text-alert-text">
            {insightErrorMessage(explain.error, "Couldn’t generate an explanation. Please try again.")}
          </p>
        )}

        {explain.data && (
          <div className="space-y-3">
            <FieldNote label="What happened">{explain.data.explanation}</FieldNote>
            {explain.data.suggestedAction && (
              <FieldNote label="Suggested action">{explain.data.suggestedAction}</FieldNote>
            )}
            <p className="font-mono text-[11px] text-muted-foreground">
              {explain.data.model}
              {explain.data.cached && " · cached"}
            </p>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
