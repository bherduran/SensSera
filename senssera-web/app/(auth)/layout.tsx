"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { GreenhouseHero } from "@/components/auth/greenhouse-hero";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  const { status } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "authenticated") router.replace("/greenhouses");
  }, [status, router]);

  return (
    <div className="grid min-h-screen lg:grid-cols-[1.2fr_1fr]">
      {/* The live clay model sits directly on the page — no frame — with its caption beneath. */}
      <section className="relative flex min-h-[18rem] flex-col lg:min-h-screen">
        <GreenhouseHero className="min-h-[18rem] flex-1" />
        <div className="pointer-events-none absolute inset-x-0 bottom-0 hidden items-end justify-between gap-6 px-10 pb-10 lg:flex">
          <div>
            <p className="label-caps">Plate 01 — live clay study</p>
            <p className="mt-2 max-w-sm font-serif text-[32px] leading-[1.05]">Every leaf, measured.</p>
          </div>
          <p className="font-serif text-2xl">SensSera</p>
        </div>
      </section>

      <div className="flex items-center justify-center p-6 sm:p-10">{children}</div>
    </div>
  );
}
