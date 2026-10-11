using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace QueryMentor;

/// <summary>
/// Finds the tables referenced by a SQL command and describes them using the EF Core relational model.
/// </summary>
internal static partial class QuerySchemaExtractor
{
    // One identifier part: [bracketed], "double quoted", `backticked` or bare.
    private const string IdentifierPart = @"(?:\[[^\]]+\]|""[^""]+""|`[^`]+`|[\w$#@]+)";

    // Basic regex-based parser: the identifier (optionally schema/database qualified) after FROM, JOIN, UPDATE, INTO or MERGE.
    [GeneratedRegex(
        @"\b(?:FROM|JOIN|UPDATE|INTO|MERGE)\s+(?<name>" + IdentifierPart + @"(?:\s*\.\s*" + IdentifierPart + "){0,2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TableReferenceRegex();

    [GeneratedRegex(IdentifierPart, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdentifierPartRegex();

    /// <summary>
    /// Returns the distinct table references in <paramref name="sql"/>, in order of appearance, without quoting.
    /// </summary>
    public static IReadOnlyList<(string? Schema, string Name)> ParseTableReferences(string sql)
    {
        var references = new List<(string? Schema, string Name)>();
        foreach (Match match in TableReferenceRegex().Matches(sql))
        {
            var parts = IdentifierPartRegex().Matches(match.Groups["name"].Value)
                .Select(part => Unquote(part.Value))
                .ToList();

            var name = parts[^1];
            var schema = parts.Count > 1 ? parts[^2] : null;
            var alreadyFound = references.Exists(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Schema, schema, StringComparison.OrdinalIgnoreCase));
            if (!alreadyFound)
            {
                references.Add((schema, name));
            }
        }

        return references;
    }

    /// <summary>
    /// Describes the tables of <paramref name="model"/> that are referenced in <paramref name="sql"/>.
    /// Tables that are not part of the model (temporary tables, CTEs, system tables, ...) are ignored.
    /// </summary>
    public static List<TableDefinition> GetTableDefinitions(IModel model, string sql)
    {
        var references = ParseTableReferences(sql);
        if (references.Count == 0)
        {
            return [];
        }

        // The relational model describes the actual database tables, including columns contributed by
        // TPH-derived and owned entity types and the database names of indexes.
        var tables = model.GetRelationalModel().Tables.ToList();
        var result = new List<TableDefinition>();
        foreach (var (schema, name) in references)
        {
            var table = tables.Find(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                && (schema is null || t.Schema is null || string.Equals(t.Schema, schema, StringComparison.OrdinalIgnoreCase)));
            if (table is not null && !result.Exists(r => r.TableName == table.Name))
            {
                result.Add(Describe(table));
            }
        }

        return result;
    }

    private static TableDefinition Describe(ITable table) => new()
    {
        TableName = table.Name,
        Columns = table.Columns.Select(c => new ColumnDefinition
        {
            ColumnName = c.Name,
            DataType = c.StoreType,
            IsNullable = c.IsNullable,
        }).ToList(),
        Indexes = table.Indexes.Select(i => new IndexDefinition
        {
            IndexName = i.Name,
            Columns = i.Columns.Select(c => c.Name).ToList(),
            IsUnique = i.IsUnique,
        }).ToList(),
        PrimaryKeyColumns = table.PrimaryKey?.Columns.Select(c => c.Name).ToList() ?? [],
    };

    private static string Unquote(string identifier)
        => identifier.Length >= 2 && identifier[0] is '[' or '"' or '`'
            ? identifier[1..^1]
            : identifier;
}
