import { describe, expect, it } from "vitest";
import { bandLayout } from "./band";

describe("bandLayout", () => {
  it("places band and needle inside a padded domain", () => {
    const r = bandLayout({ value: 20, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("in");
    expect(r.delta).toBe(0);
    expect(r.bandStart).toBeGreaterThan(0);
    expect(r.bandEnd).toBeLessThan(100);
    expect(r.needle).toBeGreaterThan(r.bandStart);
    expect(r.needle).toBeLessThan(r.bandEnd);
  });

  it("flags values over the band with a positive delta", () => {
    const r = bandLayout({ value: 34.6, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("over");
    expect(r.delta).toBeCloseTo(4.6);
    expect(r.needle).toBeGreaterThan(r.bandEnd);
    expect(r.needle).toBeLessThanOrEqual(100);
  });

  it("flags values under the band with a negative delta", () => {
    const r = bandLayout({ value: 5, min: 10, max: 30, metric: "temperature" });
    expect(r.status).toBe("under");
    expect(r.delta).toBeCloseTo(-5);
  });

  it("supports a one-sided threshold", () => {
    const r = bandLayout({ value: 1500, min: null, max: 1200, metric: "co2" });
    expect(r.status).toBe("over");
    expect(r.bandStart).toBe(0);
  });

  it("falls back to the metric's physical range without a threshold", () => {
    const r = bandLayout({ value: 50, min: null, max: null, metric: "humidity" });
    expect(r.status).toBe("none");
    expect(r.needle).toBeCloseTo(50);
  });

  it("clamps the needle into 0–100", () => {
    const r = bandLayout({ value: 9999, min: 10, max: 30, metric: "temperature" });
    expect(r.needle).toBeLessThanOrEqual(100);
  });
});
