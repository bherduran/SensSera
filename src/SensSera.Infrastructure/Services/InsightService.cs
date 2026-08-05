using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SensSera.Application.DTOs;
using SensSera.Application.Interfaces;
using SensSera.Application.Llm;
using SensSera.Application.Options;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;
using SensSera.Infrastructure.Persistence;

namespace SensSera.Infrastructure.Services;

/// <summary>
/// Read-and-interpret use-case logic for the insight layer (§7.10). It gathers tenant-filtered
/// aggregates, grounds a prompt on those concrete numbers, and asks the model to phrase them —
/// it never writes domain data and never makes the alert decision. Every data path the model can
/// trigger (via tools) runs through the EF global query filter, so tenant isolation is enforced by
/// the same mechanism as every other endpoint, not by trusting the model.
/// </summary>
public sealed class InsightService(
    AppDbContext db,
    ILlmClient llm,
    ITenantContext tenant,
    IMemoryCache cache,
    TimeProvider timeProvider,
    IOptions<LlmOptions> options,
    ILogger<InsightService> logger) : IInsightService
{
    private readonly LlmOptions _options = options.Value;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ---------------------------------------------------------------- Explain

    public async Task<AlertExplanationDto> ExplainAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();

        var alert = await db.Alerts.AsNoTracking()
            .Include(a => a.Threshold)
            .Include(a => a.Device)
            .Include(a => a.Greenhouse)
            .FirstOrDefaultAsync(a => a.Id == alertId && a.OrganizationId == orgId, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {alertId} not found");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var from = now.AddHours(-_options.ExplanationWindowHours);

        var rollups = await db.ReadingRollups.AsNoTracking()
            .Where(r => r.DeviceId == alert.DeviceId && r.Metric == alert.Metric
                        && r.Bucket == RollupBucket.Hour && r.PeriodStart >= from)
            .OrderBy(r => r.PeriodStart)
            .Select(r => new RollupPoint(r.PeriodStart, r.Min, r.Avg, r.Max))
            .ToListAsync(cancellationToken);

        // Cache key: alert id + newest relevant data timestamp — regenerate only when the data moves.
        var dataStamp = rollups.Count > 0 ? rollups[^1].PeriodStart : alert.TriggeredAt;
        var cacheKey = $"explain:{alert.Id}:{dataStamp.Ticks}";
        if (cache.TryGetValue(cacheKey, out CachedExplanation? hit) && hit is not null)
            return new AlertExplanationDto(alert.Id, hit.Explanation, hit.SuggestedAction, hit.Model, hit.GeneratedAt, Cached: true);

        var prompt = BuildExplainPrompt(alert, rollups);
        var completion = await llm.CompleteAsync(prompt, cancellationToken);

        // Log the shape of the call for cost tracking — never the prompt/response tenant data (§16).
        logger.LogInformation(
            "Insight explain alert {AlertId} model {Model} tokens in {InputTokens} out {OutputTokens}",
            alert.Id, completion.Model, completion.Usage.InputTokens, completion.Usage.OutputTokens);

        var (explanation, suggestedAction) = ParseExplanation(completion.Text);
        var generatedAt = timeProvider.GetUtcNow().UtcDateTime;
        cache.Set(cacheKey, new CachedExplanation(explanation, suggestedAction, completion.Model, generatedAt), TimeSpan.FromHours(6));

        return new AlertExplanationDto(alert.Id, explanation, suggestedAction, completion.Model, generatedAt, Cached: false);
    }

    private LlmPrompt BuildExplainPrompt(Alert alert, IReadOnlyList<RollupPoint> rollups)
    {
        var unit = MetricUnit(alert.Metric);
        var data = new StringBuilder();
        data.AppendLine($"Greenhouse: {alert.Greenhouse.Name}");
        data.AppendLine($"Device: {alert.Device.Name}");
        data.AppendLine($"Metric: {alert.Metric} ({unit})");
        data.AppendLine($"Threshold: min={N(alert.Threshold.MinValue)} max={N(alert.Threshold.MaxValue)}");
        data.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Alert: severity={alert.Severity}, status={alert.Status}, triggeredValue={N(alert.TriggeredValue)} {unit} at {alert.TriggeredAt:yyyy-MM-dd HH:mm} UTC"));
        data.AppendLine("Recent hourly readings (UTC hour: min/avg/max):");
        if (rollups.Count == 0)
        {
            data.AppendLine("  (no rollups in window)");
        }
        else
        {
            foreach (var r in rollups)
                data.AppendLine($"  {r.PeriodStart.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture)}: {N(r.Min)}/{N(r.Avg)}/{N(r.Max)}");
        }

        const string system = """
            You are SensSera's greenhouse monitoring assistant. Explain one sensor alert to a grower in plain language.
            Use ONLY the numbers in the data block — never invent, estimate, or add values that are not given.
            Reply with a compact JSON object and nothing else: {"explanation": string, "suggestedAction": string}.
            "explanation": what breached, by how much, and the recent trend. "suggestedAction": one concrete step the grower could take. Keep each under 60 words.
            """;

        return new LlmPrompt(system, data.ToString(), _options.MaxOutputTokens);
    }

    private static (string Explanation, string SuggestedAction) ParseExplanation(string text)
    {
        var json = StripFences(text);
        try
        {
            var obj = JsonSerializer.Deserialize<ExplanationJson>(json, Json);
            if (obj is not null && !string.IsNullOrWhiteSpace(obj.Explanation))
                return (obj.Explanation.Trim(), (obj.SuggestedAction ?? string.Empty).Trim());
        }
        catch (JsonException)
        {
            // Fall through — model output is untrusted; degrade instead of throwing.
        }
        return (text.Trim(), string.Empty);
    }

    // ------------------------------------------------------------------- Ask

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = tenant.RequireOrganizationId();

        var greenhouseNames = await db.Greenhouses.AsNoTracking()
            .Where(g => g.OrganizationId == orgId)
            .Select(g => g.Name)
            .ToListAsync(cancellationToken);

        var prompt = BuildAskPrompt(request.Question, greenhouseNames);

        var completion = await llm.CompleteWithToolsAsync(
            prompt, AskTools,
            (call, ct) => ExecuteToolAsync(call, orgId, ct),
            cancellationToken);

        logger.LogInformation(
            "Insight ask model {Model} tokens in {InputTokens} out {OutputTokens} tools {Tools}",
            completion.Model, completion.Usage.InputTokens, completion.Usage.OutputTokens, string.Join(",", completion.UsedTools));

        var answer = string.IsNullOrWhiteSpace(completion.Text)
            ? "I couldn't produce an answer from the available data."
            : completion.Text.Trim();

        return new AskResponse(answer, completion.UsedTools, completion.Model, timeProvider.GetUtcNow().UtcDateTime);
    }

    private LlmPrompt BuildAskPrompt(string question, IReadOnlyList<string> greenhouseNames)
    {
        var names = greenhouseNames.Count == 0
            ? "(none)"
            : string.Join(", ", greenhouseNames.Select(n => $"\"{n}\""));

        var system = $$"""
            You are SensSera's greenhouse monitoring assistant. Answer the grower's question using ONLY data returned by the provided tools.
            Never invent or estimate numbers. If the tools return no relevant data, say so plainly.
            The grower's greenhouses are: {{names}}.
            Available metrics: Temperature, Humidity, Co2, SoilMoisture, Light, Pressure.
            Call tools as needed, then give a concise natural-language answer — no JSON, no markdown tables.
            """;

        // The question rides in the user role, never in the system role — untrusted text stays out of authority.
        return new LlmPrompt(system, question, _options.MaxOutputTokens);
    }

    // Whitelisted, parameterized query functions. The model may only choose one and fill typed args;
    // every implementation runs through the tenant-scoped DbContext (§7.10).
    private static readonly IReadOnlyList<LlmTool> AskTools =
    [
        new("get_active_alerts",
            "List currently open alerts, optionally filtered to one greenhouse by name.",
            "{\"type\":\"object\",\"properties\":{\"greenhouse\":{\"type\":\"string\",\"description\":\"Greenhouse name (optional)\"}}}"),
        new("get_latest_readings",
            "Get the most recent readings for a metric in a greenhouse.",
            "{\"type\":\"object\",\"properties\":{\"greenhouse\":{\"type\":\"string\"},\"metric\":{\"type\":\"string\",\"enum\":[\"Temperature\",\"Humidity\",\"Co2\",\"SoilMoisture\",\"Light\",\"Pressure\"]}},\"required\":[\"greenhouse\",\"metric\"]}"),
        new("get_rollup",
            "Get aggregated min/avg/max for a metric in a greenhouse over the last N hours.",
            "{\"type\":\"object\",\"properties\":{\"greenhouse\":{\"type\":\"string\"},\"metric\":{\"type\":\"string\",\"enum\":[\"Temperature\",\"Humidity\",\"Co2\",\"SoilMoisture\",\"Light\",\"Pressure\"]},\"bucket\":{\"type\":\"string\",\"enum\":[\"Hour\",\"Day\"]},\"hours\":{\"type\":\"integer\"}},\"required\":[\"greenhouse\",\"metric\"]}"),
    ];

    private async Task<string> ExecuteToolAsync(LlmToolCall call, Guid orgId, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(call.ArgumentsJson);
            var args = doc.RootElement;
            return call.Name switch
            {
                "get_active_alerts" => await GetActiveAlertsAsync(args, orgId, cancellationToken),
                "get_latest_readings" => await GetLatestReadingsAsync(args, orgId, cancellationToken),
                "get_rollup" => await GetRollupAsync(args, orgId, cancellationToken),
                _ => JsonError($"Unknown function '{call.Name}'."),
            };
        }
        catch (JsonException)
        {
            return JsonError("Invalid arguments.");
        }
    }

    private async Task<string> GetActiveAlertsAsync(JsonElement args, Guid orgId, CancellationToken cancellationToken)
    {
        Guid? greenhouseId = null;
        var name = Str(args, "greenhouse");
        if (!string.IsNullOrWhiteSpace(name))
        {
            greenhouseId = await ResolveGreenhouseAsync(name, orgId, cancellationToken);
            if (greenhouseId is null) return JsonError($"No greenhouse named '{name}'.");
        }

        var alerts = await db.Alerts.AsNoTracking()
            .Where(a => a.OrganizationId == orgId && a.Status == AlertStatus.Open
                        && (greenhouseId == null || a.GreenhouseId == greenhouseId))
            .OrderByDescending(a => a.TriggeredAt)
            .Take(20)
            .Select(a => new
            {
                greenhouse = a.Greenhouse.Name,
                a.Metric,
                a.Severity,
                value = a.TriggeredValue,
                triggeredAt = a.TriggeredAt,
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new { count = alerts.Count, alerts }, Json);
    }

    private async Task<string> GetLatestReadingsAsync(JsonElement args, Guid orgId, CancellationToken cancellationToken)
    {
        var metric = ParseMetric(Str(args, "metric"));
        if (metric is null) return JsonError("Unknown metric.");
        var name = Str(args, "greenhouse");
        var greenhouseId = await ResolveGreenhouseAsync(name, orgId, cancellationToken);
        if (greenhouseId is null) return JsonError($"No greenhouse named '{name}'.");

        var readings = await db.SensorReadings.AsNoTracking()
            .Where(r => r.OrganizationId == orgId && r.Metric == metric.Value && r.Device.GreenhouseId == greenhouseId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(5)
            .Select(r => new { device = r.Device.Name, r.Metric, value = r.Value, recordedAt = r.RecordedAt })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new { count = readings.Count, readings }, Json);
    }

    private async Task<string> GetRollupAsync(JsonElement args, Guid orgId, CancellationToken cancellationToken)
    {
        var metric = ParseMetric(Str(args, "metric"));
        if (metric is null) return JsonError("Unknown metric.");
        var name = Str(args, "greenhouse");
        var greenhouseId = await ResolveGreenhouseAsync(name, orgId, cancellationToken);
        if (greenhouseId is null) return JsonError($"No greenhouse named '{name}'.");

        var bucket = ParseBucket(Str(args, "bucket")) ?? RollupBucket.Hour;
        var hours = Math.Clamp(Int(args, "hours") ?? 24, 1, 720);
        var from = timeProvider.GetUtcNow().UtcDateTime.AddHours(-hours);

        var rollups = await db.ReadingRollups.AsNoTracking()
            .Where(r => r.OrganizationId == orgId && r.Metric == metric.Value && r.Bucket == bucket
                        && r.Device.GreenhouseId == greenhouseId && r.PeriodStart >= from)
            .OrderBy(r => r.PeriodStart)
            .Take(200)
            .Select(r => new { periodStart = r.PeriodStart, min = r.Min, avg = r.Avg, max = r.Max })
            .ToListAsync(cancellationToken);

        if (rollups.Count == 0)
            return JsonSerializer.Serialize(new { count = 0, rollups = Array.Empty<object>() }, Json);

        var summary = new
        {
            overallMin = rollups.Min(r => r.min),
            overallMax = rollups.Max(r => r.max),
            overallAvg = Math.Round(rollups.Average(r => r.avg), 2),
        };
        return JsonSerializer.Serialize(new { summary, count = rollups.Count, rollups }, Json);
    }

    private Task<Guid?> ResolveGreenhouseAsync(string? name, Guid orgId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<Guid?>(null);
        // Invariant lower on the .NET side: the machine culture is tr-TR, where "I".ToLower() is
        // the dotless "ı" and would never match Postgres lower('I') = 'i' (Turkish-I bug).
        var lowered = name.ToLowerInvariant();
        return db.Greenhouses.AsNoTracking()
            .Where(g => g.OrganizationId == orgId && g.Name.ToLower() == lowered)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ------------------------------------------------------------- Helpers

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string N(double? value) => value.HasValue ? N(value.Value) : "none";

    private static string MetricUnit(MetricType metric) => metric switch
    {
        MetricType.Temperature => "°C",
        MetricType.Humidity => "%",
        MetricType.Co2 => "ppm",
        MetricType.SoilMoisture => "%",
        MetricType.Light => "lux",
        MetricType.Pressure => "hPa",
        _ => string.Empty,
    };

    private static MetricType? ParseMetric(string? s) =>
        Enum.TryParse<MetricType>(s, ignoreCase: true, out var m) ? m : null;

    private static RollupBucket? ParseBucket(string? s) =>
        Enum.TryParse<RollupBucket>(s, ignoreCase: true, out var b) ? b : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static string JsonError(string message) => JsonSerializer.Serialize(new { error = message });

    private sealed record RollupPoint(DateTime PeriodStart, double Min, double Avg, double Max);

    private static string StripFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal)) return s;
        var firstNewline = s.IndexOf('\n');
        if (firstNewline >= 0) s = s[(firstNewline + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal)) s = s[..^3];
        return s.Trim();
    }
}

file sealed record CachedExplanation(string Explanation, string SuggestedAction, string Model, DateTime GeneratedAt);
file sealed record ExplanationJson(string Explanation, string? SuggestedAction);
