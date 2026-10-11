using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace QueryMentor;

/// <summary>
/// EF Core interceptor that sends commands slower than <see cref="SlowQueryAnalyzerOptions.SlowQueryThresholdMs"/>
/// to the <see cref="SlowQueryAnalyzerService"/> and logs the result.
/// </summary>
/// <remarks>
/// The analysis runs in the background: it never delays or fails the application's database command.
/// Each distinct command text is analyzed at most once per interceptor instance (unless the analysis fails),
/// and at most <see cref="MaxConcurrentAnalyses"/> analyses run at the same time; further slow commands are
/// skipped while that limit is reached.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "SemaphoreSlim only needs disposal when its AvailableWaitHandle is used, which it is not.")]
public class SlowQueryInterceptor : DbCommandInterceptor
{
    /// <summary>The maximum number of analyses that are in flight at the same time.</summary>
    public const int MaxConcurrentAnalyses = 2;

    private const int MaxTrackedQueries = 1000;

    private static readonly JsonSerializerOptions OutputSerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SlowQueryAnalyzerService _analyzerService;
    private readonly double _thresholdMs;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, byte> _analyzedQueries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrentAnalyses, MaxConcurrentAnalyses);

    /// <summary>Creates the interceptor. This is the constructor used by dependency injection.</summary>
    public SlowQueryInterceptor(
        SlowQueryAnalyzerService analyzerService,
        IOptions<SlowQueryAnalyzerOptions> options,
        ILogger<SlowQueryInterceptor> logger)
        : this(analyzerService, options.Value.SlowQueryThresholdMs, logger)
    {
    }

    /// <summary>Creates the interceptor, resolving the <see cref="SlowQueryAnalyzerService"/> and logger from <paramref name="serviceProvider"/>.</summary>
    public SlowQueryInterceptor(IServiceProvider serviceProvider, double thresholdMs)
        : this(
            serviceProvider.GetRequiredService<SlowQueryAnalyzerService>(),
            thresholdMs,
            serviceProvider.GetService<ILogger<SlowQueryInterceptor>>())
    {
    }

    private SlowQueryInterceptor(SlowQueryAnalyzerService analyzerService, double thresholdMs, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(analyzerService);
        _analyzerService = analyzerService;
        _thresholdMs = thresholdMs;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        OnCommandExecuted(command, eventData);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        OnCommandExecuted(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        OnCommandExecuted(command, eventData);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        OnCommandExecuted(command, eventData);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        OnCommandExecuted(command, eventData);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        OnCommandExecuted(command, eventData);
        return ValueTask.FromResult(result);
    }

    private void OnCommandExecuted(DbCommand command, CommandExecutedEventData eventData)
    {
        try
        {
            var executionTime = eventData.Duration.TotalMilliseconds;
            var commandText = command.CommandText;
            if (executionTime <= _thresholdMs || string.IsNullOrWhiteSpace(commandText))
            {
                return;
            }

            if (_analyzedQueries.Count >= MaxTrackedQueries)
            {
                _analyzedQueries.Clear();
            }

            if (!_analyzedQueries.TryAdd(commandText, 0))
            {
                Log.AlreadyAnalyzed(_logger, executionTime);
                return;
            }

            if (!_concurrencyLimiter.Wait(0))
            {
                _analyzedQueries.TryRemove(commandText, out _);
                Log.AnalysisSkipped(_logger, executionTime, MaxConcurrentAnalyses);
                return;
            }

            // Collect everything that needs the DbContext now, on the calling thread: the context is not
            // thread-safe and may be disposed before the background analysis runs.
            var queryContext = new QueryContext
            {
                CommandText = commandText,
                ExecutionTime = executionTime,
                Tables = TryGetTableDefinitions(eventData.Context, commandText),
            };

            Log.SlowQueryDetected(_logger, executionTime, _thresholdMs);
            _ = Task.Run(() => AnalyzeAndLogAsync(queryContext));
        }
        catch (Exception ex)
        {
            // Never let QueryMentor break the application's database access.
            Log.InterceptorFailed(_logger, ex);
        }
    }

    private async Task AnalyzeAndLogAsync(QueryContext queryContext)
    {
        try
        {
            var result = await _analyzerService.AnalyzeQueryAsync(queryContext).ConfigureAwait(false);
            Log.SlowQueryAnalyzed(
                _logger,
                queryContext.ExecutionTime,
                queryContext.CommandText,
                JsonSerializer.Serialize(result, OutputSerializerOptions));
        }
        catch (Exception ex)
        {
            // Allow another attempt the next time this query is slow.
            _analyzedQueries.TryRemove(queryContext.CommandText, out _);
            Log.AnalysisFailed(_logger, ex, queryContext.ExecutionTime, queryContext.CommandText);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private List<TableDefinition> TryGetTableDefinitions(DbContext? context, string sql)
    {
        if (context is null)
        {
            return [];
        }

        try
        {
            return GetTableDefinitions(context, sql);
        }
        catch (Exception ex)
        {
            Log.SchemaExtractionFailed(_logger, ex);
            return [];
        }
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
