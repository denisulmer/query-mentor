namespace QueryMentor;

/// <summary>
/// The structured result of an AI analysis of a slow query.
/// </summary>
public class AnalysisResult
{
    /// <summary>"Yes" if the model found a problem with the query, otherwise "No".</summary>
    public string? ProblemFound { get; set; }

    /// <summary>A short description of the problem, or a note that no significant issues were detected.</summary>
    public string? Description { get; set; }

    /// <summary>The individual issues found, each with a suggestion and an impact estimate.</summary>
    public List<QueryIssue> Issues { get; set; } = [];
}
