using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace QueryMentor;

/// <summary>
/// Extension methods for registering QueryMentor with dependency injection.
/// </summary>
public static class SlowQueryAnalyzerExtensions
{
    /// <summary>
    /// Registers the <see cref="SlowQueryAnalyzerService"/> and the <see cref="SlowQueryInterceptor"/>.
    /// The options are validated when the host starts (or, without a host, when the services are first resolved).
    /// Add the interceptor to your <c>DbContext</c> with <c>AddInterceptors(sp.GetRequiredService&lt;SlowQueryInterceptor&gt;())</c>.
    /// </summary>
    public static IServiceCollection AddSlowQueryAnalyzer(
        this IServiceCollection services,
        Action<SlowQueryAnalyzerOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddLogging();
        services.AddOptions<SlowQueryAnalyzerOptions>()
            .Configure(configureOptions)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SlowQueryAnalyzerOptions>, SlowQueryAnalyzerOptionsValidator>());
        services.TryAddSingleton<SlowQueryAnalyzerService>();
        services.TryAddSingleton<SlowQueryInterceptor>();
        return services;
    }
}
