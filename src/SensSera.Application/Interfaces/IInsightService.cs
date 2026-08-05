using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

/// <summary>
/// Use-case logic for the insight layer: gather tenant-filtered data → build a grounded
/// prompt → call <see cref="ILlmClient"/> → map to a DTO. Read-and-interpret only; it never
/// writes domain data and never makes the alert decision (that stays in the threshold job).
/// </summary>
public interface IInsightService
{
    /// <summary>
    /// Explains one alert in plain language. Gathers the alert, its threshold, device/greenhouse,
    /// and the recent readings window, then grounds the model on those concrete numbers.
    /// Throws <see cref="KeyNotFoundException"/> if the alert belongs to another tenant (→ 404).
    /// </summary>
    Task<AlertExplanationDto> ExplainAlertAsync(Guid alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers a natural-language question over the caller's data via whitelisted, tenant-filtered
    /// query functions (function calling — no free-form NL→SQL).
    /// </summary>
    Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken = default);
}
