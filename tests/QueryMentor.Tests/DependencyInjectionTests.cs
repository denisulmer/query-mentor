using Microsoft.Extensions.DependencyInjection;

namespace QueryMentor.Tests;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSlowQueryAnalyzer(options =>
        {
            options.AzureOpenAIEndpoint = "https://example.openai.azure.com/";
            options.AzureOpenAIKey = "test-key";
            options.ModelName = "test-model";
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddSlowQueryAnalyzer_ServiceCanBeResolved()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<SlowQueryAnalyzerService>());
    }

    [Fact]
    public void AddSlowQueryAnalyzer_InterceptorCanBeResolved()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<SlowQueryInterceptor>());
    }
}
