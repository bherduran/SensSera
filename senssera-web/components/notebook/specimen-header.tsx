import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type SpecimenHeaderProps = {
  code: string;
  subtitle?: string;
  title: ReactNode;
  aside?: ReactNode;
  className?: string;
};

/** Page header styled like a specimen label: caption line, serif title, ink rule. */
export function SpecimenHeader({ code, subtitle, title, aside, className }: SpecimenHeaderProps) {
  return (
    <header className={cn("flex items-end justify-between gap-4 pb-3 ink-rule", className)}>
      <div className="min-w-0">
        <p className="label-caps">
          {code}
          {subtitle && <span> · {subtitle}</span>}
        </p>
        <h1 className="mt-1.5 truncate font-serif text-[40px] leading-none tracking-[-0.01em]">{title}</h1>
      </div>
      {aside && <div className="shrink-0">{aside}</div>}
    </header>
  );
}
