"use client";

import { useEffect, useRef, useState } from "react";
import {
  createTelemetryConnection,
  type ReadingReceived,
  type AlertRaised,
} from "@/lib/signalr";
import { useAuth } from "@/lib/auth";

type TelemetryHandlers = {
  onReading?: (event: ReadingReceived) => void;
  onAlert?: (event: AlertRaised) => void;
};

/**
 * Opens a live telemetry connection while the user is authenticated and routes
 * hub events to the latest handlers. Handlers are held in a ref so passing new
 * inline callbacks each render never tears down the connection. Returns the
 * live connection state for a UI indicator.
 */
export function useTelemetry(handlers: TelemetryHandlers): { connected: boolean } {
  const { user } = useAuth();
  const userId = user?.id;
  const handlersRef = useRef(handlers);
  const [connected, setConnected] = useState(false);

  // Keep the ref current without re-running the connection effect.
  useEffect(() => {
    handlersRef.current = handlers;
  });

  useEffect(() => {
    if (!userId) return;

    const connection = createTelemetryConnection();
    connection.on("ReadingReceived", (event: ReadingReceived) =>
      handlersRef.current.onReading?.(event),
    );
    connection.on("AlertRaised", (event: AlertRaised) =>
      handlersRef.current.onAlert?.(event),
    );
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    const startPromise = connection
      .start()
      .then(() => setConnected(true))
      .catch(() => {
        // withAutomaticReconnect handles transient drops; initial failure stays disconnected.
      });

    return () => {
      setConnected(false);
      // Only stop once start settles, so a fast unmount (React StrictMode in dev,
      // or a route change) never aborts the negotiation mid-flight.
      void startPromise.finally(() => connection.stop());
    };
  }, [userId]);

  return { connected };
}
