namespace QueryMentor;

/// <summary>
/// Decides whether a command is worth sending for query analysis: application queries and DML are,
/// schema changes, migrations and EF Core's own catalog queries are not.
/// </summary>
internal static class CommandClassifier
{
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    // Schema changes and session/maintenance statements (typically from migrations or EnsureCreated) are
    // often slow but there is nothing to optimize in them, so analyzing them only costs tokens.
    private static readonly HashSet<string> IgnoredLeadingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "ALTER", "DROP", "TRUNCATE", "RENAME", "COMMENT", "GRANT", "REVOKE", "PRAGMA", "VACUUM", "ANALYZE", "DBCC",
    };

    // Catalog queries EF Core issues itself (e.g. to check whether the database exists).
    private static readonly HashSet<string> SystemSchemas = new(StringComparer.OrdinalIgnoreCase)
    {
        "sys", "INFORMATION_SCHEMA", "pg_catalog",
    };

    private static readonly HashSet<string> SystemTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite_master", "sqlite_schema", "sqlite_sequence",
    };

    public static bool IsAnalyzable(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText)
            || commandText.Contains(MigrationsHistoryTable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyword = FirstKeyword(commandText);
        if (keyword.Length == 0 || IgnoredLeadingKeywords.Contains(keyword.ToString()))
        {
            return false;
        }

        return !QuerySchemaExtractor.ParseTableReferences(commandText).Any(table =>
            SystemTables.Contains(table.Name) || (table.Schema is not null && SystemSchemas.Contains(table.Schema)));
    }

    private static ReadOnlySpan<char> FirstKeyword(ReadOnlySpan<char> sql)
    {
        while (true)
        {
            sql = sql.TrimStart();
            if (sql.StartsWith("--", StringComparison.Ordinal))
            {
                // Line comment, e.g. from TagWith().
                var end = sql.IndexOf('\n');
                sql = end < 0 ? [] : sql[(end + 1)..];
            }
            else if (sql.StartsWith("/*", StringComparison.Ordinal))
            {
                var end = sql.IndexOf("*/", StringComparison.Ordinal);
                sql = end < 0 ? [] : sql[(end + 2)..];
            }
            else
            {
                break;
            }
        }

        var length = 0;
        while (length < sql.Length && char.IsLetter(sql[length]))
        {
            length++;
        }

        return sql[..length];
    }
}
