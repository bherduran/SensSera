"use client";

import { useState } from "react";
import { Sparkles, Loader2, Lightbulb, ArrowRight } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { useExplainAlert } from "@/hooks/use-insights";

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
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-4 w-4 text-primary" />
            Alert explanation
          </DialogTitle>
          <DialogDescription>
            Plain-language interpretation of the threshold breach — grounded on
            your real readings.
          </DialogDescription>
        </DialogHeader>

        {explain.isPending && (
          <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Interpreting the readings…
          </div>
        )}

        {explain.isError && (
          <p className="py-6 text-sm text-destructive">
            Couldn&rsquo;t generate an explanation. Please try again.
          </p>
        )}

        {explain.data && (
          <div className="space-y-4">
            <section className="space-y-1.5">
              <h3 className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                <Lightbulb className="h-3.5 w-3.5" /> What happened
              </h3>
              <p className="text-sm leading-relaxed">
                {explain.data.explanation}
              </p>
            </section>

            {explain.data.suggestedAction && (
              <section className="space-y-1.5 rounded-lg border bg-muted/30 p-3">
                <h3 className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  <ArrowRight className="h-3.5 w-3.5" /> Suggested action
                </h3>
                <p className="text-sm leading-relaxed">
                  {explain.data.suggestedAction}
                </p>
              </section>
            )}

            <p className="text-[11px] text-muted-foreground">
              {explain.data.model}
              {explain.data.cached && " · cached"}
            </p>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
