using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using SensSera.Application.Interfaces;
using SensSera.Application.Llm;
using SensSera.Application.Options;

namespace SensSera.Infrastructure.Services;

/// <summary>
/// Anthropic-backed <see cref="ILlmClient"/>. The only class that references the vendor SDK —
/// a model swap is confined here. The API key lives on the injected <see cref="AnthropicClient"/>
/// (constructed once at the composition root), never in a bound options object.
/// </summary>
public sealed class AnthropicLlmClient(AnthropicClient client, IOptions<LlmOptions> options) : ILlmClient
{
    private readonly LlmOptions _options = options.Value;

    public async Task<LlmCompletion> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = prompt.MaxTokens,
            System = prompt.System,
            Messages = [new() { Role = Role.User, Content = prompt.User }],
        }, cancellationToken: cancellationToken);

        return new LlmCompletion(
            ExtractText(response.Content),
            response.Model ?? _options.Model,
            new LlmUsage((int)response.Usage.InputTokens, (int)response.Usage.OutputTokens),
            []);
    }

    public async Task<LlmCompletion> CompleteWithToolsAsync(
        LlmPrompt prompt,
        IReadOnlyList<LlmTool> tools,
        Func<LlmToolCall, CancellationToken, Task<string>> executeToolAsync,
        CancellationToken cancellationToken = default)
    {
        ToolUnion[] toolDefs = tools.Select(t => new ToolUnion(BuildTool(t))).ToArray();
        var messages = new List<MessageParam> { new() { Role = Role.User, Content = prompt.User } };
        var usedTools = new List<string>();
        var model = _options.Model;
        int totalInput = 0, totalOutput = 0;

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = prompt.MaxTokens,
                System = prompt.System,
                Messages = messages,
                Tools = toolDefs,
            }, cancellationToken: cancellationToken);

            model = response.Model ?? model;
            totalInput += (int)response.Usage.InputTokens;
            totalOutput += (int)response.Usage.OutputTokens;

            // Model is done deliberating — return its text answer.
            if (response.StopReason != "tool_use")
                return new LlmCompletion(
                    ExtractText(response.Content), model,
                    new LlmUsage(totalInput, totalOutput),
                    usedTools.Distinct().ToList());

            // Echo the assistant turn, run each requested whitelisted tool, collect results.
            var assistantContent = new List<ContentBlockParam>();
            var toolResults = new List<ContentBlockParam>();
            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });
                    usedTools.Add(toolUse.Name);
                    var argsJson = JsonSerializer.Serialize(toolUse.Input);
                    var result = await executeToolAsync(new LlmToolCall(toolUse.Name, argsJson), cancellationToken);
                    toolResults.Add(new ToolResultBlockParam { ToolUseID = toolUse.ID, Content = result });
                }
            }

            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        // Iteration cap hit without a final answer — the service degrades gracefully.
        return new LlmCompletion(string.Empty, model, new LlmUsage(totalInput, totalOutput), usedTools.Distinct().ToList());
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> content) =>
        string.Concat(content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

    private static Tool BuildTool(LlmTool tool)
    {
        using var doc = JsonDocument.Parse(tool.InputSchemaJson);
        var root = doc.RootElement;

        var properties = new Dictionary<string, JsonElement>();
        if (root.TryGetProperty("properties", out var props))
            foreach (var prop in props.EnumerateObject())
                properties[prop.Name] = prop.Value.Clone(); // Clone survives JsonDocument disposal

        var required = new List<string>();
        if (root.TryGetProperty("required", out var req))
            required.AddRange(req.EnumerateArray().Select(e => e.GetString()!));

        return new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = new() { Properties = properties, Required = required },
        };
    }
}
