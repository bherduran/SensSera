import * as React from "react";

/** The sign-in / sign-up sheet: specimen caption, serif title, form, quiet footer. */
export function AuthCard({
  eyebrow,
  title,
  description,
  children,
  footer,
}: {
  eyebrow: string;
  title: string;
  description: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}) {
  return (
    <section className="w-full max-w-[26rem] rounded-md border bg-card p-8 paper-shadow sm:p-10">
      <p className="font-serif text-xl lg:hidden">SensSera</p>
      <div className="mt-6 pb-3 ink-rule lg:mt-0">
        <p className="label-caps">{eyebrow}</p>
        <h1 className="mt-1.5 font-serif text-[34px] leading-none">{title}</h1>
      </div>
      <p className="mt-3 text-sm text-muted-foreground">{description}</p>
      <div className="mt-7">{children}</div>
      {footer && <div className="mt-6 border-t border-dashed pt-4 text-sm text-muted-foreground">{footer}</div>}
    </section>
  );
}
