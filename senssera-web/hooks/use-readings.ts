"use client";

import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import type { GreenhouseReadings, Metric } from "@/lib/types";

/**
 * Rollup time-series for one greenhouse + metric (default hourly buckets).
 * The backend requires `metric`, so the query is only enabled once one is set.
 */
export function useGreenhouseReadings(
  greenhouseId: string,
  metric: Metric,
  bucket: "Hour" | "Day" = "Hour",
) {
  return useQuery({
    queryKey: ["greenhouse-readings", greenhouseId, metric, bucket],
    queryFn: () =>
      apiFetch<GreenhouseReadings>(
        `/api/greenhouses/${greenhouseId}/readings?metric=${metric}&bucket=${bucket}`,
      ),
    enabled: Boolean(greenhouseId) && Boolean(metric),
  });
}
