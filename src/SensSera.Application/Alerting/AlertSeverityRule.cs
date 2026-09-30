using SensSera.Domain.Enums;

namespace SensSera.Application.Alerting;

/// <summary>
/// Severity from how far a reading overshoots its threshold (TDD §7.6 "a simple rule"):
/// beyond 20% of the allowed band — or of the bound itself when only one side is set —
/// the alert is <see cref="AlertSeverity.Critical"/>, otherwise <see cref="AlertSeverity.Warning"/>.
/// </summary>
public static class AlertSeverityRule
{
    public const double CriticalFraction = 0.2;

    public static AlertSeverity For(double value, double? min, double? max)
    {
        var overshoot = value switch
        {
            _ when max is { } hi && value > hi => value - hi,
            _ when min is { } lo && value < lo => lo - value,
            _ => 0,
        };
        if (overshoot <= 0) return AlertSeverity.Info;

        // Scale: band width when both bounds exist, else the magnitude of the one bound (≥ 1 so a
        // zero bound doesn't turn every overshoot critical).
        var scale = min is { } a && max is { } b && b > a
            ? b - a
            : Math.Max(1, Math.Abs(max ?? min ?? 0));

        return overshoot >= scale * CriticalFraction ? AlertSeverity.Critical : AlertSeverity.Warning;
    }
}
