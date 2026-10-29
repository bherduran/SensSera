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

    /// <summary>Which <c>ILlmClient</c> backs the LLM layer: "Anthropic" (default), "Groq" (free cloud)
    /// or "Ollama" (local, no API key). Groq and Ollama share one OpenAI-compatible adapter; only
    /// <see cref="BaseUrl"/> differs. Set the matching <see cref="Model"/> for the chosen provider.</summary>
    [Required]
    [AllowedValues("Anthropic", "Groq", "Ollama")]
    public string Provider { get; set; } = "Anthropic";

    /// <summary>Model id for the active <see cref="Provider"/>. Authoritative ids come from config,
    /// never hardcoded (Anthropic e.g. "claude-opus-5"; Groq e.g. "openai/gpt-oss-120b";
    /// Ollama e.g. "qwen3:8b").</summary>
    [Required]
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>Base address of an OpenAI-compatible endpoint. Empty = the provider default (Groq cloud,
    /// or Ollama on localhost). Inside Docker Compose use "http://host.docker.internal:11434/v1/".
    /// Ignored for Anthropic.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Per-request timeout for the OpenAI-compatible adapter. A local 8B model can take tens
    /// of seconds per tool round, so this is well above a cloud API's typical latency.</summary>
    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Hours of recent readings to ground an alert explanation on.</summary>
    [Range(1, 168)]
    public int ExplanationWindowHours { get; set; } = 6;

    /// <summary>Cap on model output tokens — interpretation is short, so keep it bounded. Current
    /// Claude models think by default and thinking counts toward this cap, so leave headroom.</summary>
    [Range(128, 8192)]
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>Max whitelisted tool round-trips for one "Ask" question (bounds cost/latency).</summary>
    [Range(1, 10)]
    public int MaxToolIterations { get; set; } = 5;
}
