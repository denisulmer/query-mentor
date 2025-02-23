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
                Tables = new List<string>() // Add table extraction logic here if needed
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
}
