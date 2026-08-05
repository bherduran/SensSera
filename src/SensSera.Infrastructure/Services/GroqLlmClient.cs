using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SensSera.Application.Interfaces;
using SensSera.Application.Llm;
using SensSera.Application.Options;

namespace SensSera.Infrastructure.Services;

/// <summary>
/// Groq-backed <see cref="ILlmClient"/> — a free, OpenAI-compatible provider used for local/dev
/// testing without an Anthropic key. Like <see cref="AnthropicLlmClient"/>, this is the only place
/// that speaks the vendor wire format: a provider swap is confined to one adapter. Talks the
/// OpenAI <c>/chat/completions</c> shape over a typed <see cref="HttpClient"/> whose base address
/// and bearer key are set once at the composition root — the key never rides in bound options.
/// </summary>
public sealed class GroqLlmClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly LlmOptions _options = options.Value;

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
        using var response = await http.PostAsync("chat/completions", content, cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Groq request failed ({(int)response.StatusCode}): {payload}");

        return JsonDocument.Parse(payload);
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

    private static string Text(JsonElement message) =>
        message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? string.Empty
            : string.Empty;

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
