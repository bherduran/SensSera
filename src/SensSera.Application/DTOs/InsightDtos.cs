namespace SensSera.Application.DTOs;

// LLM insight layer DTOs (§7.10). Text is produced by the model; every number in it
// comes from tenant-filtered aggregates injected into the prompt, never model recall.

/// <summary>Plain-language explanation + suggested action for one alert.</summary>
public sealed record AlertExplanationDto(
    Guid AlertId,
    string Explanation,
    string SuggestedAction,
    string Model,
    DateTime GeneratedAt,
    bool Cached);

/// <summary>Natural-language question over the caller's own greenhouse data.</summary>
public sealed record AskRequest(string Question);

/// <summary>Answer plus which whitelisted tools were invoked (auditability).</summary>
public sealed record AskResponse(
    string Answer,
    IReadOnlyList<string> UsedFunctions,
    string Model,
    DateTime GeneratedAt);
