"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  createTelemetryConnection,
  type ReadingReceived,
  type AlertRaised,
} from "@/lib/signalr";
import { useAuth } from "@/lib/auth";

export type TelemetryHandlers = {
  onReading?: (event: ReadingReceived) => void;
  onAlert?: (event: AlertRaised) => void;
};

type TelemetryContextValue = {
  connected: boolean;
  subscribe: (handlers: TelemetryHandlers) => () => void;
};

const TelemetryContext = createContext<TelemetryContextValue>({
  connected: false,
  subscribe: () => () => {},
});

/**
 * Holds a single app-wide telemetry connection for the authenticated session.
 * Pages subscribe/unsubscribe as they mount, so navigation never tears down or
 * re-negotiates the connection — the Live state stays stable across routes.
 */
export function TelemetryProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const userId = user?.id;
  const qc = useQueryClient();
  const [connected, setConnected] = useState(false);
  const listeners = useRef(new Set<TelemetryHandlers>());

  // After a (re)connect, refetch live-backed queries so any events missed while
  // disconnected are reconciled.
  const resync = useCallback(() => {
    qc.invalidateQueries({ queryKey: ["dashboard"] });
    qc.invalidateQueries({ queryKey: ["alerts"] });
  }, [qc]);

  const subscribe = useCallback((handlers: TelemetryHandlers) => {
    listeners.current.add(handlers);
    return () => {
      listeners.current.delete(handlers);
    };
  }, []);

  useEffect(() => {
    if (!userId) return;

    const connection = createTelemetryConnection();
    connection.on("ReadingReceived", (event: ReadingReceived) =>
      listeners.current.forEach((l) => l.onReading?.(event)),
    );
    connection.on("AlertRaised", (event: AlertRaised) =>
      listeners.current.forEach((l) => l.onAlert?.(event)),
    );
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => {
      setConnected(true);
      resync();
    });
    connection.onclose(() => setConnected(false));

    const startPromise = connection
      .start()
      .then(() => {
        setConnected(true);
        resync();
      })
      .catch(() => {
        // withAutomaticReconnect retries; initial failure stays disconnected.
      });

    return () => {
      setConnected(false);
      void startPromise.finally(() => connection.stop());
    };
  }, [userId, resync]);

  return (
    <TelemetryContext.Provider value={{ connected, subscribe }}>
      {children}
    </TelemetryContext.Provider>
  );
}

export function useTelemetryContext() {
  return useContext(TelemetryContext);
}
