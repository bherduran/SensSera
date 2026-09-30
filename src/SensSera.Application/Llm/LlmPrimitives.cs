namespace SensSera.Application.Llm;

// Provider-agnostic primitives for the insight layer. The concrete vendor SDK
// (Anthropic) lives in Infrastructure behind ILlmClient — nothing here knows
// which model provider is used. Keeps the one-way reference rule intact.

/// <summary>A grounded prompt: authoritative system instructions + the user/data content.</summary>
public sealed record LlmPrompt(string System, string User, int MaxTokens = 1024);

/// <summary>Token usage for one model call — logged for cost tracking (never the prompt content).</summary>
public sealed record LlmUsage(int InputTokens, int OutputTokens);

/// <summary>
/// A whitelisted, parameterized function the model may choose to call. The model can only
/// pick a name and fill typed args; it never runs free-form code or SQL (§7.10).
/// </summary>
public sealed record LlmTool(string Name, string Description, string InputSchemaJson);

/// <summary>A model's request to invoke one whitelisted tool, with its raw JSON arguments.</summary>
public sealed record LlmToolCall(string Name, string ArgumentsJson);

/// <summary>Result of a model call: the text, which model produced it, usage, and any tools it invoked.</summary>
public sealed record LlmCompletion(
    string Text,
    string Model,
    LlmUsage Usage,
    IReadOnlyList<string> UsedTools);

/// <summary>
/// The model provider could not be reached or rejected the call (missing/invalid key, outage,
/// rate limit). Adapters translate vendor exceptions into this so the API maps it to 503
/// without knowing which provider is configured.
/// </summary>
public sealed class LlmUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
