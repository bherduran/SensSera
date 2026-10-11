using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SensSera.Application.Interfaces;
using SensSera.Application.Llm;
using SensSera.Application.Options;

namespace SensSera.Infrastructure.Services;

/// <summary>
/// <see cref="ILlmClient"/> for any OpenAI-compatible <c>/chat/completions</c> endpoint — Groq in the
/// cloud, Ollama locally. Like <see cref="AnthropicLlmClient"/>, this is the only place that speaks
/// this wire format. The base address, timeout and optional bearer key are set once on the typed
/// <see cref="HttpClient"/> at the composition root — the key never rides in bound options.
/// </summary>
public sealed partial class OpenAiCompatibleLlmClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    public const string GroqBaseUrl = "https://api.groq.com/openai/v1/";
    public const string OllamaBaseUrl = "http://localhost:11434/v1/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly LlmOptions _options = options.Value;

    /// <summary>Configured <see cref="LlmOptions.BaseUrl"/>, or the provider default when empty.</summary>
    public static Uri ResolveBaseAddress(LlmOptions options)
    {
        var url = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? options.Provider == "Ollama" ? OllamaBaseUrl : GroqBaseUrl
            : options.BaseUrl;

        // "chat/completions" is resolved relative to the base; without the trailing slash HttpClient
        // would replace the last segment ("v1") instead of appending to it.
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }

    public async Task<LlmCompletion> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        var messages = new JsonArray
        {
            Message("system", prompt.System),
            Message("user", prompt.User),
        };

        using var doc = await PostAsync(messages, prompt.MaxTokens, tools: null, cancellationToken);
        var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        return new LlmCompletion(
            Text(choice),
            ModelOf(doc.RootElement),
            UsageOf(doc.RootElement),
            []);
    }

    public async Task<LlmCompletion> CompleteWithToolsAsync(
        LlmPrompt prompt,
        IReadOnlyList<LlmTool> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeToolAsync,
        CancellationToken cancellationToken = default)
    {
        var toolDefs = BuildTools(tools);
        var messages = new JsonArray
        {
            Message("system", prompt.System),
            Message("user", prompt.User),
        };

        var usedTools = new List<string>();
        var model = _options.Model;
        int totalInput = 0, totalOutput = 0;

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            using var doc = await PostAsync(messages, prompt.MaxTokens, toolDefs, cancellationToken);
            var root = doc.RootElement;
            model = ModelOf(root);
            var usage = UsageOf(root);
            totalInput += usage.InputTokens;
            totalOutput += usage.OutputTokens;

            var message = root.GetProperty("choices")[0].GetProperty("message");

            // No tool call this turn — the model has produced its final text answer.
            if (!message.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.GetArrayLength() == 0)
                return new LlmCompletion(
                    Text(message), model,
                    new LlmUsage(totalInput, totalOutput),
                    usedTools.Distinct().ToList());

            // Echo the assistant turn verbatim (OpenAI protocol requires the tool_calls it just
            // emitted), then run each whitelisted tool and append its result keyed by call id.
            messages.Add(JsonNode.Parse(message.GetRawText())!);
            foreach (var call in toolCalls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? string.Empty;
                var args = fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}";
                var id = call.GetProperty("id").GetString() ?? string.Empty;

                usedTools.Add(name);
                var result = await executeToolAsync(new LlmToolCall(name, args), cancellationToken);
                messages.Add(ToolMessage(id, result));
            }
        }

        // Iteration cap hit without a final answer — the service degrades gracefully.
        return new LlmCompletion(string.Empty, model, new LlmUsage(totalInput, totalOutput), usedTools.Distinct().ToList());
    }

    private async Task<JsonDocument> PostAsync(
        JsonArray messages, int maxTokens, JsonArray? tools, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["max_tokens"] = maxTokens,
            ["messages"] = messages.DeepClone(),
        };
        if (tools is not null)
        {
            body["tools"] = tools.DeepClone();
            body["tool_choice"] = "auto";
        }

        using var content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("chat/completions", content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new LlmUnavailableException($"{_options.Provider} request failed.", ex);
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new LlmUnavailableException($"{_options.Provider} request failed ({(int)response.StatusCode}): {payload}");

            return JsonDocument.Parse(payload);
        }
    }

    private static JsonArray BuildTools(IReadOnlyList<LlmTool> tools)
    {
        var array = new JsonArray();
        foreach (var tool in tools)
        {
            array.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.InputSchemaJson),
                },
            });
        }
        return array;
    }

    private static JsonObject Message(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static JsonObject ToolMessage(string toolCallId, string content) =>
        new() { ["role"] = "tool", ["tool_call_id"] = toolCallId, ["content"] = content };

    // Qwen3 (Ollama) emits its reasoning as <think>…</think> before the answer. An unclosed block means
    // max_tokens cut the model off mid-thought, so there is no answer to keep.
    private static string Text(JsonElement message) =>
        message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? ThinkBlock().Replace(c.GetString() ?? string.Empty, string.Empty).Trim()
            : string.Empty;

    [GeneratedRegex(@"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();

    private string ModelOf(JsonElement root) =>
        root.TryGetProperty("model", out var m) ? m.GetString() ?? _options.Model : _options.Model;

    private static LlmUsage UsageOf(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
            return new LlmUsage(0, 0);
        var input = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
        var output = usage.TryGetProperty("completion_tokens", out var o) ? o.GetInt32() : 0;
        return new LlmUsage(input, output);
    }
}
