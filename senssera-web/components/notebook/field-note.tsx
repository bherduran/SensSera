import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type FieldNoteProps = {
  label?: string;
  children?: ReactNode;
  pending?: boolean;
  className?: string;
};

/** AI output written like a margin note in the notebook, not a chat bubble. */
export function FieldNote({ label = "Field note — AI", children, pending, className }: FieldNoteProps) {
  return (
    <figure className={cn("border-l-2 border-alert bg-alert/[0.07] px-4 py-3", className)}>
      <figcaption className="label-caps text-alert-text">{label}</figcaption>
      <blockquote className="mt-1.5 whitespace-pre-line font-serif text-[17px] leading-snug italic text-foreground">
        {pending ? <span className="text-muted-foreground motion-safe:animate-pulse">Writing…</span> : children}
      </blockquote>
    </figure>
  );
}
