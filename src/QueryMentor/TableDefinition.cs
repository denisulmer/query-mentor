namespace QueryMentor;

/// <summary>
/// Schema information about a table referenced by a slow query.
/// </summary>
public class TableDefinition
{
    /// <summary>The table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>The table's columns.</summary>
    public List<ColumnDefinition> Columns { get; set; } = [];

    /// <summary>The table's indexes.</summary>
    public List<IndexDefinition> Indexes { get; set; } = [];

    /// <summary>The columns that make up the primary key.</summary>
    public List<string> PrimaryKeyColumns { get; set; } = [];
}

/// <summary>
/// Schema information about a column.
/// </summary>
public class ColumnDefinition
{
    /// <summary>The column name.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>The store (database) type of the column, if known.</summary>
    public string? DataType { get; set; }

    /// <summary>Whether the column allows NULL values.</summary>
    public bool IsNullable { get; set; }
}

/// <summary>
/// Schema information about an index.
/// </summary>
public class IndexDefinition
{
    /// <summary>The index name.</summary>
    public string? IndexName { get; set; }

    /// <summary>The indexed columns, in order.</summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>Whether the index is unique.</summary>
    public bool IsUnique { get; set; }
}
