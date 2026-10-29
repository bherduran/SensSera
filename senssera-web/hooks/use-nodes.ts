"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type {
  Node,
  NodeChannel,
  NodeChannelInput,
  NodeInput,
  NodeWithToken,
} from "@/lib/types";

// Channel devices also appear in the device list, so node mutations refresh both.
function useInvalidate(greenhouseId: string) {
  const qc = useQueryClient();
  return () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ["nodes", greenhouseId] }),
      qc.invalidateQueries({ queryKey: ["devices", greenhouseId] }),
    ]);
}

export function useNodes(greenhouseId: string) {
  return useQuery({
    queryKey: ["nodes", greenhouseId],
    queryFn: () => apiFetch<Node[]>(`/api/nodes/greenhouse/${greenhouseId}`),
    enabled: Boolean(greenhouseId),
    // Online/offline is computed from lastSeenAt on each fetch; poll so it moves on its own.
    refetchInterval: 60_000,
  });
}

export function useCreateNode(greenhouseId: string) {
  const invalidate = useInvalidate(greenhouseId);
  return useMutation({
    mutationFn: (input: NodeInput) =>
      apiFetch<NodeWithToken>("/api/nodes", {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: invalidate,
  });
}

export function useAddChannel(greenhouseId: string) {
  const invalidate = useInvalidate(greenhouseId);
  return useMutation({
    mutationFn: ({ nodeId, input }: { nodeId: string; input: NodeChannelInput }) =>
      apiFetch<NodeChannel>(`/api/nodes/${nodeId}/channels`, {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: invalidate,
  });
}

export function useRotateNodeToken(greenhouseId: string) {
  const invalidate = useInvalidate(greenhouseId);
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<NodeWithToken>(`/api/nodes/${id}/rotate-token`, { method: "POST" }),
    onSuccess: invalidate,
  });
}

export function useDeleteNode(greenhouseId: string) {
  const invalidate = useInvalidate(greenhouseId);
  return useMutation({
    mutationFn: (id: string) =>
      apiFetch<void>(`/api/nodes/${id}`, { method: "DELETE" }),
    onSuccess: invalidate,
  });
}
