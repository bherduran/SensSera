import * as React from "react";
import { Leaf } from "lucide-react";
import { Card } from "@/components/ui/card";

export function AuthCard({
  title,
  description,
  children,
  footer,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}) {
  return (
    <Card className="w-full max-w-md gap-5 p-8 shadow-lg">
      <div className="mb-1 flex items-center justify-center gap-2 lg:hidden">
        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
          <Leaf className="h-6 w-6 text-primary" />
        </div>
        <span className="text-2xl font-bold text-foreground">SensSera</span>
      </div>
      <div className="text-center">
        <h2 className="text-2xl font-bold text-foreground">{title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      </div>
      {children}
      {footer && (
        <div className="text-center text-sm text-muted-foreground">{footer}</div>
      )}
    </Card>
  );
}
