import { cn } from "@/lib/utils";

/** Small connection-state pill for pages backed by a live SignalR feed. */
export function LiveIndicator({ connected }: { connected: boolean }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium",
        connected
          ? "border-success/30 bg-success/10 text-success"
          : "border-border bg-muted text-muted-foreground",
      )}
    >
      <span className="relative flex h-2 w-2">
        {connected && (
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-success opacity-75" />
        )}
        <span
          className={cn(
            "relative inline-flex h-2 w-2 rounded-full",
            connected ? "bg-success" : "bg-muted-foreground/50",
          )}
        />
      </span>
      {connected ? "Live" : "Offline"}
    </span>
  );
}
