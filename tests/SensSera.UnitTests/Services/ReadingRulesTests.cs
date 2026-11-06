using FluentAssertions;
using SensSera.Application.DTOs;
using SensSera.Application.Ingestion;

namespace SensSera.UnitTests.Services;

public class ReadingRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    // Board clocks drift a little; minutes ahead pass, an hour ahead is a wrong clock.
    [InlineData(4, true)]
    [InlineData(6, false)]
    [InlineData(-6 * 24 * 60, true)] // a backlog flushed after days offline
    public void Check_RecordedAt_AllowsSmallSkewOnly(int minutesFromNow, bool passes)
    {
        var reading = new IngestRequest("SoilMoisture", 40, Now.AddMinutes(minutesFromNow), "soil1");

        (ReadingRules.Check(reading, Now) is null).Should().Be(passes);
    }

    [Fact]
    public void Check_OffsetTimestamp_IsComparedInUtc()
    {
        // 14:03 at +03:00 is 11:03 UTC: in the past, even though the wall-clock digits are ahead.
        var local = new DateTimeOffset(2026, 11, 6, 14, 3, 0, TimeSpan.FromHours(3)).LocalDateTime;

        ReadingRules.Check(new IngestRequest("Temperature", 20, local), Now).Should().BeNull();
    }

    [Theory]
    [InlineData("Humidity", 101, "outside")]
    [InlineData("Pressure", 500, "outside")]
    [InlineData("Rainfall", 1, "unknown metric")]
    [InlineData("", 1, "unknown metric")]
    public void Check_BadMetricOrValue_SaysWhy(string metric, double value, string reason)
    {
        ReadingRules.Check(new IngestRequest(metric, value, Now), Now).Should().Contain(reason);
    }

    [Fact]
    public void Check_LongClientText_IsClippedInTheReason()
    {
        var reason = ReadingRules.Check(new IngestRequest(new string('x', 500), 1, Now), Now);

        reason!.Length.Should().BeLessThan(80);
    }
}
