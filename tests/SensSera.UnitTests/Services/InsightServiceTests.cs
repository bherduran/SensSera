using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Application.Llm;
using SensSera.Application.Options;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class InsightServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeLlmClient _llm = new();
    private readonly InsightService _sut;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    public InsightServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenant = new FakeInsightTenant(_orgId);
        _db = new AppDbContext(options, tenant);
        _sut = new InsightService(
            _db, _llm, tenant,
            new MemoryCache(new MemoryCacheOptions()),
            new FixedInsightClock(_now),
            Options.Create(new LlmOptions()),
            NullLogger<InsightService>.Instance);
    }

    [Fact]
    public async Task ExplainAlertAsync_GroundsPromptOnRealAggregates()
    {
        var (gh, device) = SeedGreenhouseWithDevice("GH1");
        var threshold = new Threshold { OrganizationId = _orgId, GreenhouseId = gh.Id, Metric = MetricType.Temperature, MaxValue = 28, IsEnabled = true, CreatedAt = _now, UpdatedAt = _now };
        var alert = new Alert
        {
            OrganizationId = _orgId, GreenhouseId = gh.Id, DeviceId = device.Id, ThresholdId = threshold.Id,
            Metric = MetricType.Temperature, TriggeredValue = 31.2, Severity = AlertSeverity.Critical,
            Status = AlertStatus.Open, TriggeredAt = _now, CreatedAt = _now, UpdatedAt = _now,
        };
        _db.Thresholds.Add(threshold);
        _db.Alerts.Add(alert);
        _db.ReadingRollups.Add(Rollup(device, _now.AddHours(-1), min: 24.8, avg: 27.5, max: 31.2));
        await _db.SaveChangesAsync();

        var result = await _sut.ExplainAlertAsync(alert.Id);

        // Every real number was injected into the prompt — the model only phrases them.
        _llm.LastPrompt!.User.Should().Contain("31.2");   // triggered value
        _llm.LastPrompt!.User.Should().Contain("28");     // threshold max
        _llm.LastPrompt!.User.Should().Contain("24.8");   // rollup min
        result.Explanation.Should().Be("Temperature exceeded the max.");
        result.SuggestedAction.Should().Be("Ventilate.");
        result.Cached.Should().BeFalse();
    }

    [Fact]
    public async Task ExplainAlertAsync_SecondCall_IsCachedAndSkipsModel()
    {
        var (gh, device) = SeedGreenhouseWithDevice("GH1");
        var alert = new Alert
        {
            OrganizationId = _orgId, GreenhouseId = gh.Id, DeviceId = device.Id, ThresholdId = Guid.NewGuid(),
            Metric = MetricType.Temperature, TriggeredValue = 31.2, Severity = AlertSeverity.Warning,
            Status = AlertStatus.Open, TriggeredAt = _now, CreatedAt = _now, UpdatedAt = _now,
            Threshold = new Threshold { OrganizationId = _orgId, GreenhouseId = gh.Id, Metric = MetricType.Temperature, MaxValue = 28, CreatedAt = _now, UpdatedAt = _now },
        };
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();

        await _sut.ExplainAlertAsync(alert.Id);
        var second = await _sut.ExplainAlertAsync(alert.Id);

        second.Cached.Should().BeTrue();
        _llm.CompleteCalls.Should().Be(1); // regenerated once, then served from cache
    }

    [Fact]
    public async Task ExplainAlertAsync_CrossTenant_ThrowsKeyNotFound()
    {
        var otherOrg = Guid.NewGuid();
        _db.Alerts.Add(new Alert
        {
            OrganizationId = otherOrg, GreenhouseId = Guid.NewGuid(), DeviceId = Guid.NewGuid(), ThresholdId = Guid.NewGuid(),
            Metric = MetricType.Temperature, TriggeredValue = 40, Severity = AlertSeverity.Critical,
            Status = AlertStatus.Open, TriggeredAt = _now, CreatedAt = _now, UpdatedAt = _now,
        });
        await _db.SaveChangesAsync();
        var otherId = _db.Alerts.IgnoreQueryFilters().First().Id;

        await _sut.Invoking(s => s.ExplainAlertAsync(otherId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AskAsync_ToolReturnsTenantData_Grounded()
    {
        var (gh, device) = SeedGreenhouseWithDevice("GH1");
        _db.ReadingRollups.Add(Rollup(device, _now.AddHours(-1), min: 22, avg: 26, max: 30));
        await _db.SaveChangesAsync();

        string? toolResult = null;
        _llm.ToolHandler = async (_, execute) =>
        {
            toolResult = await execute(
                new LlmToolCall("get_rollup", "{\"greenhouse\":\"GH1\",\"metric\":\"Temperature\"}"),
                CancellationToken.None);
            return "GH1 averaged 26.";
        };

        var result = await _sut.AskAsync(new AskRequest("How hot was GH1?"));

        toolResult.Should().Contain("26");            // grounded on the seeded rollup
        result.Answer.Should().Be("GH1 averaged 26.");
        result.UsedFunctions.Should().Contain("get_rollup");
    }

    [Fact]
    public async Task AskAsync_ToolCannotReachAnotherTenantsGreenhouse()
    {
        // A greenhouse owned by a different org — must be invisible to this tenant's tools.
        var otherOrg = Guid.NewGuid();
        _db.Greenhouses.Add(new Greenhouse { OrganizationId = otherOrg, Name = "Secret", CreatedAt = _now, UpdatedAt = _now });
        await _db.SaveChangesAsync();

        string? toolResult = null;
        _llm.ToolHandler = async (_, execute) =>
        {
            toolResult = await execute(
                new LlmToolCall("get_rollup", "{\"greenhouse\":\"Secret\",\"metric\":\"Temperature\"}"),
                CancellationToken.None);
            return "done";
        };

        await _sut.AskAsync(new AskRequest("Show me Secret greenhouse"));

        toolResult.Should().Contain("No greenhouse named"); // resolved only within the caller's org
    }

    private (Greenhouse, Device) SeedGreenhouseWithDevice(string name)
    {
        var gh = new Greenhouse { OrganizationId = _orgId, Name = name, CreatedAt = _now, UpdatedAt = _now };
        var device = new Device { OrganizationId = _orgId, Greenhouse = gh, Name = "Temp", Metric = MetricType.Temperature, CreatedAt = _now, UpdatedAt = _now };
        _db.Greenhouses.Add(gh);
        _db.Devices.Add(device);
        return (gh, device);
    }

    private ReadingRollup Rollup(Device d, DateTime periodStart, double min, double avg, double max) =>
        new()
        {
            OrganizationId = _orgId,
            Device = d,
            Metric = d.Metric,
            Bucket = RollupBucket.Hour,
            PeriodStart = periodStart,
            Min = min,
            Max = max,
            Avg = avg,
            Count = 10,
            CreatedAt = _now,
            UpdatedAt = _now,
        };

    public void Dispose() => _db.Dispose();
}

file sealed class FakeInsightTenant(Guid orgId) : ITenantContext
{
    public Guid? OrganizationId => orgId;
}

file sealed class FixedInsightClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

sealed class FakeLlmClient : ILlmClient
{
    public LlmPrompt? LastPrompt;
    public int CompleteCalls;
    public string CompleteResponse = "{\"explanation\":\"Temperature exceeded the max.\",\"suggestedAction\":\"Ventilate.\"}";
    public Func<IReadOnlyList<LlmTool>, Func<LlmToolCall, CancellationToken, Task<string>>, Task<string>>? ToolHandler;

    public Task<LlmCompletion> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        LastPrompt = prompt;
        CompleteCalls++;
        return Task.FromResult(new LlmCompletion(CompleteResponse, "test-model", new LlmUsage(1, 1), []));
    }

    public async Task<LlmCompletion> CompleteWithToolsAsync(
        LlmPrompt prompt,
        IReadOnlyList<LlmTool> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeToolAsync,
        CancellationToken cancellationToken = default)
    {
        LastPrompt = prompt;
        var text = ToolHandler is null ? "answer" : await ToolHandler(tools, executeToolAsync);
        return new LlmCompletion(text, "test-model", new LlmUsage(1, 1), ["get_rollup"]);
    }
}
