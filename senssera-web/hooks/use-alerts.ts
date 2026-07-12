"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { Alert, AlertsPage } from "@/lib/types";

export type AlertFilter = {
  status?: string;
  greenhouseId?: string;
  page?: number;
  pageSize?: number;
};

export function useAlerts(filter: AlertFilter) {
  return useQuery({
    queryKey: ["alerts", filter],
    queryFn: () => {
      const params = new URLSearchParams();
      if (filter.status) params.set("status", filter.status);
      if (filter.greenhouseId) params.set("greenhouseId", filter.greenhouseId);
      params.set("page", String(filter.page ?? 1));
      params.set("pageSize", String(filter.pageSize ?? 20));
      return apiFetch<AlertsPage>(`/api/alerts?${params.toString()}`);
    },
  });
}

// Alert state changes also affect the dashboard's active-alert counts, so both
// query trees are invalidated.
function invalidateAlertsAndDashboard(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ["alerts"] });
  qc.invalidateQueries({ queryKey: ["dashboard"] });
}

export function useAcknowledgeAlert() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<Alert>(`/api/alerts/${id}/acknowledge`, { method: "POST" }),
    onSuccess: () => invalidateAlertsAndDashboard(qc),
  });
}

export function useResolveAlert() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<Alert>(`/api/alerts/${id}/resolve`, { method: "POST" }),
    onSuccess: () => invalidateAlertsAndDashboard(qc),
  });
}
