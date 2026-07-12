import {
  HubConnectionBuilder,
  type HubConnection,
  LogLevel,
} from "@microsoft/signalr";
import { API_URL } from "./config";
import { getAccessToken } from "./api";
import type { Metric, AlertSeverity } from "./types";

// Server -> client payloads from TelemetryHub. Enum fields arrive camelCase
// (JsonStringEnumConverter), matching the REST DTOs.
export type ReadingReceived = {
  greenhouseId: string;
  deviceId: string;
  metric: Metric;
  value: number;
  recordedAt: string;
};

export type AlertRaised = {
  alertId: string;
  greenhouseId: string;
  metric: Metric;
  severity: AlertSeverity;
  triggeredAt: string;
};

/**
 * Builds a connection to /hubs/telemetry. The JWT is read from memory on every
 * (re)connect via accessTokenFactory — SignalR appends it as ?access_token=,
 * which the API only honours on the hub path. No token ever touches storage.
 */
export function createTelemetryConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/telemetry`, {
      accessTokenFactory: () => getAccessToken() ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
