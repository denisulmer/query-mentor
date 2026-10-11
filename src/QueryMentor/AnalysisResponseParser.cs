using System.Text.Json;

namespace QueryMentor;

/// <summary>
/// Turns the model's reply into an <see cref="AnalysisResult"/>.
/// </summary>
internal static class AnalysisResponseParser
{
    private const int MaxResponseExcerptLength = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Parses the model's reply. The prompt asks for camelCase JSON, so property names are matched
    /// case-insensitively, and any text or Markdown code fence around the JSON object is ignored.
    /// </summary>
    /// <exception cref="InvalidOperationException">The reply does not contain a valid analysis result.</exception>
    public static AnalysisResult Parse(string? response)
    {
        var json = ExtractJsonObject(response);
        if (json is null)
        {
            throw CreateParseException(response, innerException: null);
        }

        AnalysisResult? result;
        try
        {
            result = JsonSerializer.Deserialize<AnalysisResult>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw CreateParseException(response, ex);
        }

        if (result is null)
        {
            throw CreateParseException(response, innerException: null);
        }

        result.Issues ??= [];
        result.Issues.RemoveAll(issue => issue is null);
        return result;
    }

    private static string? ExtractJsonObject(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        // Models frequently wrap JSON in ```json fences or add a sentence before/after it.
        var start = response.IndexOf('{', StringComparison.Ordinal);
        var end = response.LastIndexOf('}');
        return start >= 0 && end > start ? response[start..(end + 1)] : null;
    }

    private static InvalidOperationException CreateParseException(string? response, Exception? innerException)
    {
        var excerpt = response is null
            ? "<empty>"
            : response.Length <= MaxResponseExcerptLength ? response : response[..MaxResponseExcerptLength] + "…";
        return new InvalidOperationException($"The model response could not be parsed as a QueryMentor analysis result. Response: {excerpt}", innerException);
    }
}
