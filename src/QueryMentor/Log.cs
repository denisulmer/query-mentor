using Microsoft.Extensions.Logging;

namespace QueryMentor;

/// <summary>
/// High-performance log messages (source generated) used by QueryMentor.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Slow query detected ({ExecutionTimeMs:F0} ms). QueryMentor analysis:\n{Analysis}\nQuery:\n{CommandText}")]
    public static partial void SlowQueryAnalyzed(ILogger logger, double executionTimeMs, string commandText, string analysis);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "QueryMentor could not analyze a slow query ({ExecutionTimeMs:F0} ms). Query:\n{CommandText}")]
    public static partial void AnalysisFailed(ILogger logger, Exception exception, double executionTimeMs, string commandText);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Skipping analysis of a slow query ({ExecutionTimeMs:F0} ms): {MaxConcurrentAnalyses} analyses are already in progress.")]
    public static partial void AnalysisSkipped(ILogger logger, double executionTimeMs, int maxConcurrentAnalyses);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Skipping analysis of a slow query ({ExecutionTimeMs:F0} ms): the same query was already analyzed.")]
    public static partial void AlreadyAnalyzed(ILogger logger, double executionTimeMs);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Slow query detected ({ExecutionTimeMs:F0} ms, threshold {ThresholdMs} ms); sending it for analysis.")]
    public static partial void SlowQueryDetected(ILogger logger, double executionTimeMs, double thresholdMs);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "QueryMentor could not collect table schema information for a slow query; analyzing it without.")]
    public static partial void SchemaExtractionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error,
        Message = "Unexpected error in the QueryMentor interceptor. The database command was not affected.")]
    public static partial void InterceptorFailed(ILogger logger, Exception exception);
}
