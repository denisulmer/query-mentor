public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly double _thresholdMs;
    
    public SlowQueryInterceptor(IServiceProvider serviceProvider, double thresholdMs)
    {
        _serviceProvider = serviceProvider;
        _thresholdMs = thresholdMs;
    }
    
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        double executionTime = eventData.Duration.TotalMilliseconds;
        if (executionTime > _thresholdMs)
        {
            var queryContext = new QueryContext
            {
                CommandText = command.CommandText,
                ExecutionTime = executionTime,
                Tables = GetTableDefinitions(command.CommandText)
            };
    
            var analyzerService = eventData.Context.GetService<SlowQueryAnalyzerService>();
            if (analyzerService != null)
            {
                string analysis = await analyzerService.AnalyzeQueryAsync(queryContext);
                Console.WriteLine("Slow Query Analysis:");
                Console.WriteLine(analysis);
            }
        }
        return result;
    }
    
    private List<TableDefinition> GetTableDefinitions(string sql)
    {
        var tableNames = ParseTableNamesFromSql(sql);
        var tables = new List<TableDefinition>();
        foreach (var tableName in tableNames)
        {
            var entityType = _context.Model.GetEntityTypes()
                .FirstOrDefault(e => e.GetTableName().Equals(tableName, StringComparison.OrdinalIgnoreCase));
            if (entityType != null)
            {
                var tableDef = new TableDefinition
                {
                    TableName = tableName,
                    Columns = entityType.GetProperties().Select(p => new ColumnDefinition
                    {
                        ColumnName = p.GetColumnName(),
                        DataType = p.GetColumnType(),
                        IsNullable = p.IsNullable
                    }).ToList(),
                    Indexes = entityType.GetIndexes().Select(i => new IndexDefinition
                    {
                        IndexName = i.Name,
                        Columns = i.Properties.Select(p => p.GetColumnName()).ToList(),
                        IsUnique = i.IsUnique
                    }).ToList(),
                    PrimaryKeyColumns = entityType.FindPrimaryKey()?.Properties
                        .Select(p => p.GetColumnName()).ToList() ?? new List<string>()
                };
                tables.Add(tableDef);
            }
        }
        return tables;
    }
    
    // Basic regex-based parser to extract table names from SQL
    private List<string> ParseTableNamesFromSql(string sql)
    {
        var tableNames = new List<string>();
        var regex = new Regex(@"(?:FROM|JOIN)\s+([^\s]+)", RegexOptions.IgnoreCase);
        var matches = regex.Matches(sql);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                string tableName = match.Groups[1].Value.Trim('[', ']', '"');
                if (!tableNames.Contains(tableName))
                {
                    tableNames.Add(tableName);
                }
            }
        }
        return tableNames;
    }
}
