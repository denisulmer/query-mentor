namespace QueryMentor;

/// <summary>
/// Options for QueryMentor's slow query analysis.
/// </summary>
public class SlowQueryAnalyzerOptions
{
    /// <summary>The Azure OpenAI endpoint, e.g. <c>https://your-resource.openai.azure.com/</c>.</summary>
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    /// <summary>The Azure OpenAI API key.</summary>
    public string AzureOpenAIKey { get; set; } = string.Empty;

    /// <summary>The name of the Azure OpenAI model deployment to use.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Queries that take longer than this many milliseconds are analyzed. Defaults to 500 ms.</summary>
    public double SlowQueryThresholdMs { get; set; } = 500;
}
