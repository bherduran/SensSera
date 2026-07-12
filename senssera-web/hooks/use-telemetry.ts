"use client";

import { useEffect, useRef } from "react";
import {
  useTelemetryContext,
  type TelemetryHandlers,
} from "@/components/app/telemetry-provider";

/**
 * Subscribes to the app-wide telemetry feed and returns the live connection
 * state. Handlers are held in a ref so passing new inline callbacks each render
 * never re-subscribes. The connection itself lives in TelemetryProvider.
 */
export function useTelemetry(handlers: TelemetryHandlers): { connected: boolean } {
  const { connected, subscribe } = useTelemetryContext();
  const handlersRef = useRef(handlers);

  useEffect(() => {
    handlersRef.current = handlers;
  });

  useEffect(() => {
    return subscribe({
      onReading: (event) => handlersRef.current.onReading?.(event),
      onAlert: (event) => handlersRef.current.onAlert?.(event),
    });
  }, [subscribe]);

  return { connected };
}
