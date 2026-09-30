"use client";

import { useState } from "react";
import { Copy, Check, TriangleAlert } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

export function DeviceTokenDialog({
  token,
  deviceName,
  onClose,
}: {
  token: string | null;
  deviceName?: string;
  onClose: () => void;
}) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    if (!token) return;
    await navigator.clipboard.writeText(token);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <Dialog
      open={Boolean(token)}
      onOpenChange={(open) => {
        if (!open) {
          setCopied(false);
          onClose();
        }
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            Device token{deviceName ? ` — ${deviceName}` : ""}
          </DialogTitle>
          <DialogDescription>
            Copy this token now. For security it is shown only once and cannot be
            retrieved later.
          </DialogDescription>
        </DialogHeader>

        <div className="flex items-start gap-2 border-l-2 border-alert bg-alert/[0.07] px-3 py-2.5 text-sm">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-alert-text" />
          <span className="text-foreground/80">
            Paste it into your device / simulator config. If it&apos;s lost, rotate
            the token to issue a new one.
          </span>
        </div>

        <div className="flex items-center gap-2 rounded-[2px] border border-dashed bg-muted/50 p-3">
          <code className="flex-1 break-all font-mono text-xs">{token}</code>
          <Button
            variant="outline"
            size="icon"
            onClick={copy}
            aria-label="Copy token"
          >
            {copied ? (
              <Check className="h-4 w-4" />
            ) : (
              <Copy className="h-4 w-4" />
            )}
          </Button>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>Done</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
