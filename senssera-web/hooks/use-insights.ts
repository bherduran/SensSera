"use client";

import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { AlertExplanation, AskResponse } from "@/lib/types";

// Both insight calls are on-demand (triggered by a button / form submit) and
// slow (they hit the model), so they're mutations with their own loading state
// rather than queries that run on mount.

export function useExplainAlert() {
  return useMutation({
    mutationFn: (alertId: string) =>
      apiFetch<AlertExplanation>(`/api/alerts/${alertId}/explain`),
  });
}

export function useAskInsights() {
  return useMutation({
    mutationFn: (question: string) =>
      apiFetch<AskResponse>(`/api/insights/ask`, {
        method: "POST",
        body: JSON.stringify({ question }),
      }),
  });
}
