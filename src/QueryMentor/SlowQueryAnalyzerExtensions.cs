using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace QueryMentor;

/// <summary>
/// Extension methods for registering QueryMentor with dependency injection.
/// </summary>
public static class SlowQueryAnalyzerExtensions
{
    /// <summary>
    /// Registers the <see cref="SlowQueryAnalyzerService"/> and the <see cref="SlowQueryInterceptor"/>.
    /// Add the interceptor to your <c>DbContext</c> with <c>AddInterceptors(sp.GetRequiredService&lt;SlowQueryInterceptor&gt;())</c>.
    /// </summary>
    public static IServiceCollection AddSlowQueryAnalyzer(
        this IServiceCollection services,
        Action<SlowQueryAnalyzerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<SlowQueryAnalyzerService>();
        services.AddSingleton<SlowQueryInterceptor>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SlowQueryAnalyzerOptions>>().Value;
            return new SlowQueryInterceptor(sp, options.SlowQueryThresholdMs);
        });
        return services;
    }
}
