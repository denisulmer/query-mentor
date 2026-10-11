using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;

namespace QueryMentor.Tests;

/// <summary>
/// Exercises the real Azure OpenAI SDK against a fake HTTP handler to verify the request QueryMentor sends.
/// </summary>
public class AzureOpenAIChatCompletionClientTests
{
    private const string CompletionResponse = """
        {
          "id": "chatcmpl-1",
          "object": "chat.completion",
          "created": 1700000000,
          "model": "gpt-4o",
          "choices": [
            { "index": 0, "finish_reason": "stop", "message": { "role": "assistant", "content": "{\"problemFound\":\"No\"}" } }
          ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;

    [Fact]
    public async Task CompleteAsync_SendsExpectedRequestAndReturnsContent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, CompletionResponse);
        var client = CreateClient(handler);

        var content = await client.CompleteAsync("system prompt", "user prompt", CancellationToken.None);

        Assert.Equal("{\"problemFound\":\"No\"}", content);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.StartsWith("https://example.openai.azure.com/openai/deployments/my-deployment/chat/completions", request.Uri);
        Assert.Equal("test-key", request.ApiKey);

        using var body = JsonDocument.Parse(request.Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("system prompt", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("user prompt", messages[1].GetProperty("content").GetString());
        Assert.Equal(0.5, body.RootElement.GetProperty("temperature").GetDouble(), 3);
        var hasTokenLimit = body.RootElement.TryGetProperty("max_tokens", out var maxTokens)
            || body.RootElement.TryGetProperty("max_completion_tokens", out maxTokens);
        Assert.True(hasTokenLimit, request.Body);
        Assert.Equal(500, maxTokens.GetInt32());
    }

    [Fact]
    public async Task CompleteAsync_ErrorResponse_Throws()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, """{"error":{"code":"401","message":"Access denied"}}""");
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(() => client.CompleteAsync("s", "u", CancellationToken.None));
    }

    private static AzureOpenAIChatCompletionClient CreateClient(HttpMessageHandler handler)
    {
        var options = new SlowQueryAnalyzerOptions
        {
            AzureOpenAIEndpoint = "https://example.openai.azure.com/",
            AzureOpenAIKey = "test-key",
            ModelName = "my-deployment",
        };
        var clientOptions = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        return new AzureOpenAIChatCompletionClient(options, clientOptions);
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? ApiKey, string Body);

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("api-key", out var apiKeys);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(), apiKeys?.FirstOrDefault(), body));
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
