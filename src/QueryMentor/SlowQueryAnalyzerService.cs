using System.Text.Json;
using Microsoft.Extensions.Options;

namespace QueryMentor;

/// <summary>
/// Sends slow queries to Azure OpenAI for analysis.
/// </summary>
public class SlowQueryAnalyzerService
{
    internal const string SystemPrompt = "You are an SQL query analyzer. Always respond in JSON format as specified in the user’s request.";

    private readonly IChatCompletionClient _client;

    /// <summary>Creates the service using the configured options. This is the constructor used by dependency injection.</summary>
    public SlowQueryAnalyzerService(IOptions<SlowQueryAnalyzerOptions> options)
        : this(options.Value)
    {
    }

    /// <summary>Creates the service using the given options.</summary>
    public SlowQueryAnalyzerService(SlowQueryAnalyzerOptions options)
        : this(new AzureOpenAIChatCompletionClient(options))
    {
    }

    internal SlowQueryAnalyzerService(IChatCompletionClient client)
    {
        _client = client;
    }

    /// <summary>Analyzes the given query and returns the model's findings.</summary>
    /// <param name="queryContext">The query to analyze.</param>
    /// <param name="cancellationToken">Cancels the request to the model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="queryContext"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The model's reply could not be parsed.</exception>
    public async Task<AnalysisResult> AnalyzeQueryAsync(QueryContext queryContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryContext);

        var prompt = BuildPrompt(queryContext);
        var response = await _client.CompleteAsync(SystemPrompt, prompt, cancellationToken).ConfigureAwait(false);

        return AnalysisResponseParser.Parse(response);
    }

    internal static string BuildPrompt(QueryContext queryContext)
    {
        var jsonContext = JsonSerializer.Serialize(queryContext);
        return $$"""
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
    }
}
