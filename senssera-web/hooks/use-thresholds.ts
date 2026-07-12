"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { Threshold, ThresholdInput } from "@/lib/types";

export function useThresholds(greenhouseId: string) {
  return useQuery({
    queryKey: ["thresholds", greenhouseId],
    queryFn: () =>
      apiFetch<Threshold[]>(`/api/thresholds/greenhouse/${greenhouseId}`),
    enabled: Boolean(greenhouseId),
  });
}

export function useCreateThreshold(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ThresholdInput) =>
      apiFetch<Threshold>("/api/thresholds", {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["thresholds", greenhouseId] }),
  });
}

export function useUpdateThreshold(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: ThresholdInput }) =>
      apiFetch<Threshold>(`/api/thresholds/${id}`, {
        method: "PUT",
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["thresholds", greenhouseId] }),
  });
}

export function useDeleteThreshold(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/thresholds/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      // Deleting a threshold also removes its alerts, so refresh those views too.
      qc.invalidateQueries({ queryKey: ["thresholds", greenhouseId] });
      qc.invalidateQueries({ queryKey: ["dashboard"] });
      qc.invalidateQueries({ queryKey: ["alerts"] });
    },
  });
}
