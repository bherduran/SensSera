"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";

const clock = (d: Date) => d.toLocaleTimeString("en-GB", { hour12: false });

/** Connection state plus a ticking clock, like a timestamp in the margin. */
export function LiveStamp({ connected }: { connected: boolean }) {
  // Empty until mounted so the server-rendered markup never disagrees with the client clock.
  const [now, setNow] = useState<string>("");

  useEffect(() => {
    const tick = () => setNow(clock(new Date()));
    const id = setInterval(tick, 1000);
    const first = setTimeout(tick, 0);
    return () => {
      clearInterval(id);
      clearTimeout(first);
    };
  }, []);

  return (
    <div className="text-right" aria-live="polite">
      <p className="label-caps">{connected ? "Live" : "Offline"}</p>
      <p className="mt-1 flex items-center justify-end gap-1.5 font-mono text-[13px]">
        <span
          className={cn(
            "inline-block size-[7px] rounded-full",
            connected ? "bg-alert motion-safe:animate-pulse" : "bg-muted-foreground",
          )}
        />
        <span className="min-w-[8ch]">{now || "--:--:--"}</span>
      </p>
    </div>
  );
}
