"use client";

import { useMutation } from "@tanstack/react-query";
import { ApiError, apiFetch } from "@/lib/api";
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

// Maps the API's insight failures to copy the user can act on: 503 means no AI
// provider is configured/reachable, 429 is the per-tenant insights rate limit.
export function insightErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    if (error.status === 503) return "AI insights are not available right now.";
    if (error.status === 429) return "Too many AI requests. Wait a minute and try again.";
  }
  return fallback;
}
