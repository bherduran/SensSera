"use client";

import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { DashboardSummary, GreenhouseDetail } from "@/lib/types";

export const dashboardKey = ["dashboard"] as const;

export function useDashboard() {
  return useQuery({
    queryKey: dashboardKey,
    queryFn: () => apiFetch<DashboardSummary>("/api/dashboard"),
  });
}

export function useGreenhouseDetail(id: string) {
  return useQuery({
    queryKey: ["dashboard", "greenhouse", id],
    queryFn: () =>
      apiFetch<GreenhouseDetail>(`/api/dashboard/greenhouses/${id}`),
    enabled: Boolean(id),
  });
}
