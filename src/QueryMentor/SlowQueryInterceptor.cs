using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace QueryMentor;

/// <summary>
/// EF Core interceptor that sends queries slower than the configured threshold to the
/// <see cref="SlowQueryAnalyzerService"/>.
/// </summary>
public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly double _thresholdMs;

    /// <summary>Creates the interceptor.</summary>
    public SlowQueryInterceptor(IServiceProvider serviceProvider, double thresholdMs)
    {
        _serviceProvider = serviceProvider;
        _thresholdMs = thresholdMs;
    }

    /// <inheritdoc />
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        var executionTime = eventData.Duration.TotalMilliseconds;
        if (executionTime > _thresholdMs && eventData.Context is { } context)
        {
            var queryContext = new QueryContext
            {
                CommandText = command.CommandText,
                ExecutionTime = executionTime,
                Tables = GetTableDefinitions(context, command.CommandText),
            };

            var analyzerService = context.GetService<SlowQueryAnalyzerService>();
            if (analyzerService != null)
            {
                var analysis = await analyzerService.AnalyzeQueryAsync(queryContext);
                Console.WriteLine("Slow Query Analysis:");
                Console.WriteLine(JsonSerializer.Serialize(analysis));
            }
        }
        return result;
    }

    private static List<TableDefinition> GetTableDefinitions(DbContext context, string sql)
    {
        var tableNames = ParseTableNamesFromSql(sql);
        var tables = new List<TableDefinition>();
        foreach (var tableName in tableNames)
        {
            var entityType = context.Model.GetEntityTypes()
                .FirstOrDefault(e => e.GetTableName()!.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            if (entityType != null)
            {
                var tableDef = new TableDefinition
                {
                    TableName = tableName,
                    Columns = entityType.GetProperties().Select(p => new ColumnDefinition
                    {
                        ColumnName = p.GetColumnName(),
                        DataType = p.GetColumnType(),
                        IsNullable = p.IsNullable,
                    }).ToList(),
                    Indexes = entityType.GetIndexes().Select(i => new IndexDefinition
                    {
                        IndexName = i.Name,
                        Columns = i.Properties.Select(p => p.GetColumnName()).ToList(),
                        IsUnique = i.IsUnique,
                    }).ToList(),
                    PrimaryKeyColumns = entityType.FindPrimaryKey()?.Properties
                        .Select(p => p.GetColumnName()).ToList() ?? [],
                };
                tables.Add(tableDef);
            }
        }
        return tables;
    }

    // Basic regex-based parser to extract table names from SQL
    private static List<string> ParseTableNamesFromSql(string sql)
    {
        var tableNames = new List<string>();
        var regex = new Regex(@"(?:FROM|JOIN)\s+([^\s]+)", RegexOptions.IgnoreCase);
        var matches = regex.Matches(sql);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var tableName = match.Groups[1].Value.Trim('[', ']', '"');
                if (!tableNames.Contains(tableName))
                {
                    tableNames.Add(tableName);
                }
            }
        }
        return tableNames;
    }
}
