"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { Device, DeviceInput, DeviceWithToken } from "@/lib/types";

export function useDevices(greenhouseId: string) {
  return useQuery({
    queryKey: ["devices", greenhouseId],
    queryFn: () =>
      apiFetch<Device[]>(`/api/devices/greenhouse/${greenhouseId}`),
    enabled: Boolean(greenhouseId),
  });
}

export function useCreateDevice(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: DeviceInput) =>
      apiFetch<DeviceWithToken>("/api/devices", {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["devices", greenhouseId] }),
  });
}

export function useDeleteDevice(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/devices/${id}`, { method: "DELETE" }),
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["devices", greenhouseId] }),
  });
}

export function useRotateToken(greenhouseId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<DeviceWithToken>(`/api/devices/${id}/rotate-token`, {
        method: "POST",
      }),
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["devices", greenhouseId] }),
  });
}
