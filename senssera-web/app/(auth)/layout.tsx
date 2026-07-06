"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { GreenhousePanel } from "@/components/auth/greenhouse-panel";

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { status } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "authenticated") router.replace("/greenhouses");
  }, [status, router]);

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <GreenhousePanel />
      <div className="flex items-center justify-center bg-background p-6 sm:p-10">
        {children}
      </div>
    </div>
  );
}
