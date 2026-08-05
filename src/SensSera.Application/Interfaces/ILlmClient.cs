using SensSera.Application.Llm;

namespace SensSera.Application.Interfaces;

/// <summary>
/// Thin adapter over the model provider. Implemented in Infrastructure (Anthropic SDK),
/// so the rest of the codebase never references a vendor SDK. A model swap touches one class.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// One grounded completion. Used for alert explanations: the answer's facts come from
    /// <paramref name="prompt"/>, the model only phrases them.
    /// </summary>
    Task<LlmCompletion> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the tool-use loop for "Ask": the model may only choose a whitelisted tool and fill
    /// typed args. <paramref name="executeToolAsync"/> runs that tool (through the EF global query
    /// filter) and returns rows as text; the model then summarizes only what it was handed.
    /// Tenant isolation is enforced by the tool implementations, never by trusting the model.
    /// </summary>
    Task<LlmCompletion> CompleteWithToolsAsync(
        LlmPrompt prompt,
        IReadOnlyList<LlmTool> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeToolAsync,
        CancellationToken cancellationToken = default);
}
