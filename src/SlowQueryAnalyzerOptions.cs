public class SlowQueryAnalyzerOptions
{
    public string AzureOpenAIEndpoint { get; set; }
    public string AzureOpenAIKey { get; set; }
    public string ModelName { get; set; }
    public double SlowQueryThresholdMs { get; set; } = 500; // Default to 500ms
}
