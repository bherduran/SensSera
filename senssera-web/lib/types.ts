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

export type Device = Omit<Schemas["DeviceResponse"], "metric"> & {
  metric: Metric;
};
export type DeviceInput = Omit<Schemas["DeviceRequest"], "metric"> & {
  metric: Metric;
};
export type DeviceWithToken = Omit<Schemas["DeviceWithTokenResponse"], "metric"> & {
  metric: Metric;
};
