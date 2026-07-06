"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { Greenhouse, GreenhouseInput } from "@/lib/types";

const listKey = ["greenhouses"] as const;

export function useGreenhouses() {
  return useQuery({
    queryKey: listKey,
    queryFn: () => apiFetch<Greenhouse[]>("/api/greenhouses"),
  });
}

export function useGreenhouse(id: string) {
  return useQuery({
    queryKey: ["greenhouses", id],
    queryFn: () => apiFetch<Greenhouse>(`/api/greenhouses/${id}`),
    enabled: Boolean(id),
  });
}

export function useCreateGreenhouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: GreenhouseInput) =>
      apiFetch<Greenhouse>("/api/greenhouses", {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: listKey }),
  });
}

export function useUpdateGreenhouse(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: GreenhouseInput) =>
      apiFetch<Greenhouse>(`/api/greenhouses/${id}`, {
        method: "PUT",
        body: JSON.stringify(input),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: listKey });
      qc.invalidateQueries({ queryKey: ["greenhouses", id] });
    },
  });
}

export function useDeleteGreenhouse() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/greenhouses/${id}`, { method: "DELETE" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: listKey }),
  });
}
