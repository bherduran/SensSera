using FluentAssertions;
using SensSera.Application.Alerting;
using SensSera.Domain.Enums;

namespace SensSera.UnitTests.Services;

public class AlertSeverityRuleTests
{
    [Theory]
    // Band 10–30 (width 20): critical from 4 units outside.
    [InlineData(31, 10.0, 30.0, AlertSeverity.Warning)]
    [InlineData(34, 10.0, 30.0, AlertSeverity.Critical)]
    [InlineData(7, 10.0, 30.0, AlertSeverity.Warning)]
    [InlineData(2, 10.0, 30.0, AlertSeverity.Critical)]
    // Max only (1200): critical from 240 above.
    [InlineData(1300, null, 1200.0, AlertSeverity.Warning)]
    [InlineData(1500, null, 1200.0, AlertSeverity.Critical)]
    // Min only at 0: scale floors at 1 so tiny overshoots stay warnings.
    [InlineData(-0.1, 0.0, null, AlertSeverity.Warning)]
    // Inside the band there's nothing to alert on.
    [InlineData(20, 10.0, 30.0, AlertSeverity.Info)]
    public void For_ScalesWithOvershoot(double value, double? min, double? max, AlertSeverity expected) =>
        AlertSeverityRule.For(value, min, max).Should().Be(expected);
}
