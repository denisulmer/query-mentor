namespace QueryMentor.Tests;

internal sealed class FakeChatCompletionClient : IChatCompletionClient
{
    private readonly Func<string, Task<string?>> _respond;

    public FakeChatCompletionClient(string? response)
        : this(_ => Task.FromResult(response))
    {
    }

    public FakeChatCompletionClient(Func<string, Task<string?>> respond)
    {
        _respond = respond;
    }

    public List<(string SystemPrompt, string UserPrompt)> Requests { get; } = [];

    public Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add((systemPrompt, userPrompt));
        }

        return _respond(userPrompt);
    }
}
