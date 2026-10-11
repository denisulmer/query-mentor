using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace QueryMentor;

/// <summary>
/// <see cref="IChatCompletionClient"/> backed by an Azure OpenAI chat deployment.
/// </summary>
internal sealed class AzureOpenAIChatCompletionClient : IChatCompletionClient
{
    private const int MaxOutputTokens = 500;
    private const float Temperature = 0.5f;

    private readonly ChatClient _client;

    public AzureOpenAIChatCompletionClient(SlowQueryAnalyzerOptions options)
    {
        var azureClient = new AzureOpenAIClient(new Uri(options.AzureOpenAIEndpoint), new AzureKeyCredential(options.AzureOpenAIKey));
        _client = azureClient.GetChatClient(options.ModelName);
    }

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = MaxOutputTokens,
            Temperature = Temperature,
        };

        ChatMessage[] messages = [new SystemChatMessage(systemPrompt), new UserChatMessage(userPrompt)];
        ChatCompletion completion = await _client.CompleteChatAsync(messages, completionOptions, cancellationToken).ConfigureAwait(false);

        var text = string.Concat(completion.Content.Select(part => part.Text));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
