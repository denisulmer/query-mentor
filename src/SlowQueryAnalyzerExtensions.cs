public static class SlowQueryAnalyzerExtensions
{
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
