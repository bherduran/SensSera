using SensSera.Application.DTOs;
using SensSera.Domain.Enums;

namespace SensSera.Application.Ingestion;

/// <summary>
/// Checks one reading on its own, before it is matched to a device. The single-reading endpoint
/// turns a failure into a 400; a batch skips that reading and reports it, so one bad value can't
/// cost a board the rest of its buffer.
/// </summary>
public static class ReadingRules
{
    // A few minutes ahead is clock skew; more is a wrong clock. Such a reading would stay the
    // "latest" one for hours and pin threshold evaluation to it.
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public const int MaxChannelLength = 32;

    private static readonly Dictionary<string, (MetricType Metric, double Min, double Max)> Ranges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Temperature"] = (MetricType.Temperature, -40, 80),
            ["Humidity"] = (MetricType.Humidity, 0, 100),
            ["Co2"] = (MetricType.Co2, 0, 5000),
            ["Light"] = (MetricType.Light, 0, 100_000),
            ["SoilMoisture"] = (MetricType.SoilMoisture, 0, 100),
            ["Pressure"] = (MetricType.Pressure, 800, 1200),
        };

    /// <summary>The reason the reading is unusable, or null when it passes.</summary>
    public static string? Check(IngestRequest reading, DateTime nowUtc)
    {
        if (!Ranges.TryGetValue(reading.Metric ?? "", out var range))
            return $"unknown metric '{Clip(reading.Metric)}'.";
        if (reading.Value < range.Min || reading.Value > range.Max)
            return $"{range.Metric} value {reading.Value} is outside {range.Min}–{range.Max}.";
        if (reading.RecordedAt == default)
            return "recordedAt is missing.";
        if (ToUtc(reading.RecordedAt) > nowUtc + MaxClockSkew)
            return "recordedAt is in the future; check the device clock.";
        if (reading.Channel is { } channel && (channel.Length == 0 || channel.Length > MaxChannelLength))
            return $"channel must be 1–{MaxChannelLength} characters.";
        return null;
    }

    /// <summary>Only valid after <see cref="Check"/> passed.</summary>
    public static MetricType MetricOf(IngestRequest reading) => Ranges[reading.Metric].Metric;

    // System.Text.Json yields Kind=Local for offsets like "+03:00" and Kind=Unspecified when no
    // offset is given; Npgsql only accepts UTC for timestamptz. Devices are expected to send UTC,
    // so an offset-less value is taken as UTC rather than the server's local zone.
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    // Client-supplied text echoed back in a reason (and stored on the node) stays short.
    public static string Clip(string? value) =>
        value is null ? "" : value.Length <= MaxChannelLength ? value : value[..MaxChannelLength] + "…";
}
