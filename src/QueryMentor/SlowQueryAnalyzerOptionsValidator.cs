using Microsoft.Extensions.Options;

namespace QueryMentor;

/// <summary>
/// Validates <see cref="SlowQueryAnalyzerOptions"/> so misconfiguration is reported clearly at startup
/// instead of as an obscure exception when the first <c>DbContext</c> is created.
/// </summary>
internal sealed class SlowQueryAnalyzerOptionsValidator : IValidateOptions<SlowQueryAnalyzerOptions>
{
    public ValidateOptionsResult Validate(string? name, SlowQueryAnalyzerOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.AzureOpenAIEndpoint))
        {
            failures.Add($"{nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint)} is required.");
        }
        else if (!Uri.TryCreate(options.AzureOpenAIEndpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            failures.Add($"{nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint)} must be an absolute http(s) URL, e.g. https://your-resource.openai.azure.com/.");
        }

        if (string.IsNullOrWhiteSpace(options.AzureOpenAIKey))
        {
            failures.Add($"{nameof(SlowQueryAnalyzerOptions.AzureOpenAIKey)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelName))
        {
            failures.Add($"{nameof(SlowQueryAnalyzerOptions.ModelName)} (the Azure OpenAI deployment name) is required.");
        }

        if (double.IsNaN(options.SlowQueryThresholdMs) || options.SlowQueryThresholdMs < 0)
        {
            failures.Add($"{nameof(SlowQueryAnalyzerOptions.SlowQueryThresholdMs)} must be zero or greater.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Validates options that were not obtained through the options system.</summary>
    /// <exception cref="OptionsValidationException">The options are invalid.</exception>
    public static void ThrowIfInvalid(SlowQueryAnalyzerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = new SlowQueryAnalyzerOptionsValidator().Validate(Options.DefaultName, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(SlowQueryAnalyzerOptions), result.Failures);
        }
    }
}
