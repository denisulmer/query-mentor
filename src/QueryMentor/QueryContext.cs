namespace QueryMentor;

/// <summary>
/// Information about a slow query that is sent to the model for analysis.
/// </summary>
public class QueryContext
{
    /// <summary>The SQL command text as executed by EF Core (parameter placeholders, not values).</summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>Execution time of the command in milliseconds.</summary>
    public double ExecutionTime { get; set; }

    /// <summary>Schema information for the tables referenced by the query, taken from the EF Core model.</summary>
    public List<TableDefinition> Tables { get; set; } = [];
}
