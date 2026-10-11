using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace QueryMentor;

/// <summary>
/// Sends slow queries to Azure OpenAI for analysis.
/// </summary>
public class SlowQueryAnalyzerService
{
    private readonly ChatClient _client;

    private const string SystemPrompt = "System: You are an SQL query analyzer. Always respond in JSON format as specified in the user’s request.";

    /// <summary>Creates the service using the given options.</summary>
    public SlowQueryAnalyzerService(SlowQueryAnalyzerOptions options)
    {
        var azureClient = new AzureOpenAIClient(new Uri(options.AzureOpenAIEndpoint), new AzureKeyCredential(options.AzureOpenAIKey));
        _client = azureClient.GetChatClient(options.ModelName);
    }

    /// <summary>Analyzes the given query and returns the model's findings.</summary>
    public async Task<AnalysisResult> AnalyzeQueryAsync(QueryContext queryContext)
    {
        var jsonContext = JsonSerializer.Serialize(queryContext);
        var prompt = $$"""
            Analyze the following SQL query for performance issues. Provide your response in JSON format with this structure:
            {
              "problemFound": "Yes/No",
              "description": "[Short description if a problem is found, otherwise 'No significant issues detected']",
              "issues": [
                {
                  "issue": "[Description of the issue]",
                  "suggestion": "[Suggestion to fix the issue]",
                  "impactGuess": "[High/Medium/Low]"
                }
              ]
            }

            Example response:
            {
              "problemFound": "Yes",
              "description": "The query is inefficient due to a full table scan.",
              "issues": [
                {
                  "issue": "Missing index on the 'CustomerId' column",
                  "suggestion": "Create an index on 'CustomerId' to speed up filtering.",
                  "impactGuess": "High"
                }
              ]
            }

            Query Context: {{jsonContext}}
            """;

        var chatCompletionsOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 500,
            Temperature = 0.5f,
        };

        ChatCompletion response = await _client.CompleteChatAsync([new UserChatMessage(prompt)], chatCompletionsOptions);

        var analysisResult = JsonSerializer.Deserialize<AnalysisResult>(response.Content[0].Text);

        return analysisResult!;
    }
}
