using System.ComponentModel.DataAnnotations;

namespace SensSera.Application.Options;

/// <summary>
/// Non-secret LLM configuration. Bound from the "Llm" section and validated on start
/// (mirrors <see cref="JwtOptions"/>). The API key is NOT here — it comes from the §15
/// secret mechanism (user-secrets in dev, Key Vault in prod) straight into the adapter,
/// so it never rides in a bound options object that could be logged or serialized.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Which <c>ILlmClient</c> backs the insight layer: "Anthropic" (default) or "Groq"
    /// (free, OpenAI-compatible — used for local/dev testing without an Anthropic key). Set the
    /// matching <see cref="Model"/> for the chosen provider.</summary>
    [Required]
    public string Provider { get; set; } = "Anthropic";

    /// <summary>Model id for the active <see cref="Provider"/>. Authoritative ids come from config,
    /// never hardcoded (Anthropic e.g. "claude-opus-4-8"; Groq e.g. "llama-3.3-70b-versatile").</summary>
    [Required]
    public string Model { get; set; } = "claude-opus-4-8";

    /// <summary>Hours of recent readings to ground an alert explanation on.</summary>
    [Range(1, 168)]
    public int ExplanationWindowHours { get; set; } = 6;

    /// <summary>Cap on model output tokens — interpretation is short, so keep it bounded.</summary>
    [Range(128, 8192)]
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>Max whitelisted tool round-trips for one "Ask" question (bounds cost/latency).</summary>
    [Range(1, 10)]
    public int MaxToolIterations { get; set; } = 5;
}
