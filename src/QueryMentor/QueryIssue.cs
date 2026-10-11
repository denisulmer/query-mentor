namespace QueryMentor;

/// <summary>
/// A single issue reported by the AI analysis.
/// </summary>
public class QueryIssue
{
    /// <summary>Description of the issue.</summary>
    public string? Issue { get; set; }

    /// <summary>Suggestion for fixing the issue.</summary>
    public string? Suggestion { get; set; }

    /// <summary>The model's estimate of the impact of fixing the issue ("High", "Medium" or "Low").</summary>
    public string? ImpactGuess { get; set; }
}
