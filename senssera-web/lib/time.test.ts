import { describe, expect, it } from "vitest";
import { timeAgo } from "./time";

const now = Date.parse("2026-10-29T12:00:00Z");

describe("timeAgo", () => {
  it("says never when the board has not reported", () => {
    expect(timeAgo(null, now)).toBe("never");
  });

  it.each([
    ["2026-10-29T11:59:30Z", "just now"],
    ["2026-10-29T11:56:00Z", "4 min ago"],
    ["2026-10-29T09:00:00Z", "3 h ago"],
    ["2026-10-27T12:00:00Z", "2 d ago"],
  ])("formats %s as %s", (iso, expected) => {
    expect(timeAgo(iso, now)).toBe(expected);
  });

  it("treats a timestamp slightly in the future (clock skew) as just now", () => {
    expect(timeAgo("2026-10-29T12:00:05Z", now)).toBe("just now");
  });
});
