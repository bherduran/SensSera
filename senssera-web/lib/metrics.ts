import {
  Thermometer,
  Droplets,
  Wind,
  Sprout,
  Sun,
  Gauge,
  type LucideIcon,
} from "lucide-react";
import type { Metric } from "./types";

export const METRIC_META: Record<
  Metric,
  { label: string; unit: string; icon: LucideIcon }
> = {
  temperature: { label: "Temperature", unit: "°C", icon: Thermometer },
  humidity: { label: "Humidity", unit: "%", icon: Droplets },
  co2: { label: "CO₂", unit: "ppm", icon: Wind },
  soilMoisture: { label: "Soil moisture", unit: "%", icon: Sprout },
  light: { label: "Light", unit: "lux", icon: Sun },
  pressure: { label: "Pressure", unit: "hPa", icon: Gauge },
};
