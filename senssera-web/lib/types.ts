import type { components } from "./api-schema";

type Schemas = components["schemas"];

/**
 * Domain types are derived from the generated OpenAPI schema
 * (`lib/api-schema.ts`, regenerate with `npm run gen:api` against the running
 * API). Deriving them here means any backend contract change that isn't
 * reflected in the frontend becomes a TypeScript compile error — no silent drift.
 *
 * One refinement: the .NET OpenAPI generator emits enums as `number`, but the
 * API serializes `MetricType` as a camelCase string at runtime. So we override
 * the `metric` field with the real string union below.
 */

export const METRICS = [
  "temperature",
  "humidity",
  "co2",
  "soilMoisture",
  "light",
  "pressure",
] as const;
export type Metric = (typeof METRICS)[number];

export type Greenhouse = Schemas["GreenhouseResponse"];
export type GreenhouseInput = Schemas["GreenhouseRequest"];

export const DEVICE_STATUSES = ["active", "inactive"] as const;
export type DeviceStatus = (typeof DEVICE_STATUSES)[number];

export type Device = Omit<Schemas["DeviceResponse"], "metric" | "status"> & {
  metric: Metric;
  status: DeviceStatus;
};
export type DeviceInput = Omit<Schemas["DeviceRequest"], "metric"> & {
  metric: Metric;
};
export type DeviceWithToken = Omit<Schemas["DeviceWithTokenResponse"], "metric"> & {
  metric: Metric;
};

// Alert enums serialize camelCase (JsonStringEnumConverter), mirroring the backend enums.
export const ALERT_SEVERITIES = ["warning", "critical"] as const;
export type AlertSeverity = (typeof ALERT_SEVERITIES)[number];

export const ALERT_STATUSES = ["open", "acknowledged", "resolved"] as const;
export type AlertStatus = (typeof ALERT_STATUSES)[number];

// Dashboard/alert domain types. Derived from the schema (so a removed/renamed
// field is a compile error) but with the enum fields narrowed to real unions and
// the .NET `number | string` doubles collapsed to `number`.
export type MetricCurrent = Omit<Schemas["MetricCurrent"], "metric" | "current"> & {
  metric: Metric;
  current: number;
};

export type GreenhouseSummary = Omit<
  Schemas["GreenhouseSummary"],
  "deviceCount" | "activeAlerts" | "metrics"
> & {
  deviceCount: number;
  activeAlerts: number;
  metrics: MetricCurrent[];
};

export type DashboardSummary = {
  greenhouses: GreenhouseSummary[];
};

export type Alert = Omit<
  Schemas["AlertResponse"],
  "metric" | "severity" | "status" | "triggeredValue"
> & {
  metric: Metric;
  severity: AlertSeverity;
  status: AlertStatus;
  triggeredValue: number;
};

export type MetricSummary = Omit<
  Schemas["MetricSummary"],
  "metric" | "current" | "min24h" | "max24h" | "avg24h"
> & {
  metric: Metric;
  current: number;
  min24h: number;
  max24h: number;
  avg24h: number;
};

export type GreenhouseDetail = Omit<
  Schemas["GreenhouseDetailResponse"],
  "metrics" | "activeAlerts"
> & {
  metrics: MetricSummary[];
  activeAlerts: Alert[];
};

// Rollup time-series for one metric (chart source). Doubles collapsed to number.
export type RollupPoint = {
  periodStart: string;
  min: number;
  max: number;
  avg: number;
  count: number;
};

export type GreenhouseReadings = Omit<
  Schemas["GreenhouseReadingsResponse"],
  "points"
> & {
  points: RollupPoint[];
};

export type Threshold = Omit<
  Schemas["ThresholdResponse"],
  "metric" | "minValue" | "maxValue"
> & {
  metric: Metric;
  minValue: number | null;
  maxValue: number | null;
};

export type ThresholdInput = Omit<
  Schemas["ThresholdRequest"],
  "metric" | "minValue" | "maxValue"
> & {
  metric: Metric;
  minValue: number | null;
  maxValue: number | null;
};

export type AlertsPage = Omit<
  Schemas["PagedResponseOfAlertResponse"],
  "items" | "total" | "page" | "pageSize"
> & {
  items: Alert[];
  total: number;
  page: number;
  pageSize: number;
};

// LLM insight layer (§7.10) — derived from the generated schema so contract drift is a compile error.
export type AlertExplanation = Schemas["AlertExplanationDto"];
export type AskResponse = Schemas["AskResponse"];
