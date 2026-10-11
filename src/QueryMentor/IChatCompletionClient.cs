namespace QueryMentor;

/// <summary>
/// Minimal abstraction over the chat completion API so the analysis logic can be tested
/// without calling Azure OpenAI.
/// </summary>
internal interface IChatCompletionClient
{
    /// <summary>Sends the prompts and returns the text of the model's reply (or <c>null</c> if it was empty).</summary>
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
