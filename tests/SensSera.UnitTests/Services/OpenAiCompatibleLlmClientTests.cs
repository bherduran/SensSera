using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SensSera.Application.Llm;
using SensSera.Application.Options;
using SensSera.Infrastructure.Services;

namespace SensSera.UnitTests.Services;

public class OpenAiCompatibleLlmClientTests
{
    [Fact]
    public async Task CompleteAsync_StripsThinkBlock()
    {
        var sut = CreateClient(HttpStatusCode.OK, Completion("<think>\nKısa cevap ver.\n</think>\n\nNem bandın altında."));

        var result = await sut.CompleteAsync(new LlmPrompt("system", "user"));

        result.Text.Should().Be("Nem bandın altında.");
    }

    [Fact]
    public async Task CompleteAsync_DropsUnclosedThinkBlock()
    {
        var sut = CreateClient(HttpStatusCode.OK, Completion("<think>still reasoning about the"));

        var result = await sut.CompleteAsync(new LlmPrompt("system", "user"));

        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_ProviderError_ThrowsLlmUnavailable()
    {
        var sut = CreateClient(HttpStatusCode.ServiceUnavailable, "{}");

        var act = () => sut.CompleteAsync(new LlmPrompt("system", "user"));

        await act.Should().ThrowAsync<LlmUnavailableException>();
    }

    [Theory]
    [InlineData("Ollama", null, "http://localhost:11434/v1/")]
    [InlineData("Groq", null, "https://api.groq.com/openai/v1/")]
    [InlineData("Ollama", "http://host.docker.internal:11434/v1", "http://host.docker.internal:11434/v1/")]
    public void ResolveBaseAddress_UsesProviderDefaultOrNormalizedOverride(string provider, string? baseUrl, string expected)
    {
        var uri = OpenAiCompatibleLlmClient.ResolveBaseAddress(new LlmOptions { Provider = provider, BaseUrl = baseUrl });

        uri.ToString().Should().Be(expected);
    }

    private static OpenAiCompatibleLlmClient CreateClient(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("http://llm.test/v1/") };
        return new OpenAiCompatibleLlmClient(http, Options.Create(new LlmOptions { Provider = "Ollama", Model = "qwen3:8b" }));
    }

    private static string Completion(string content) => JsonSerializer.Serialize(new
    {
        model = "qwen3:8b",
        choices = new[] { new { message = new { role = "assistant", content } } },
        usage = new { prompt_tokens = 10, completion_tokens = 5 },
    });

    // Stands in for the network: the adapter is exercised end to end without calling Groq or Ollama.
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
