using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace QueryMentor.Tests;

public class OptionsValidationTests
{
    private static SlowQueryAnalyzerOptions ValidOptions() => new()
    {
        AzureOpenAIEndpoint = "https://example.openai.azure.com/",
        AzureOpenAIKey = "test-key",
        ModelName = "test-model",
    };

    public static TheoryData<string, Action<SlowQueryAnalyzerOptions>> InvalidOptions => new()
    {
        { nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint), o => o.AzureOpenAIEndpoint = "" },
        { nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint), o => o.AzureOpenAIEndpoint = "example.openai.azure.com" },
        { nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint), o => o.AzureOpenAIEndpoint = "ftp://example.openai.azure.com/" },
        { nameof(SlowQueryAnalyzerOptions.AzureOpenAIKey), o => o.AzureOpenAIKey = " " },
        { nameof(SlowQueryAnalyzerOptions.ModelName), o => o.ModelName = "" },
        { nameof(SlowQueryAnalyzerOptions.SlowQueryThresholdMs), o => o.SlowQueryThresholdMs = -1 },
        { nameof(SlowQueryAnalyzerOptions.SlowQueryThresholdMs), o => o.SlowQueryThresholdMs = double.NaN },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void InvalidOptions_FailWithDescriptiveErrorOnResolve(string invalidProperty, Action<SlowQueryAnalyzerOptions> makeInvalid)
    {
        var services = new ServiceCollection();
        services.AddSlowQueryAnalyzer(options =>
        {
            var valid = ValidOptions();
            options.AzureOpenAIEndpoint = valid.AzureOpenAIEndpoint;
            options.AzureOpenAIKey = valid.AzureOpenAIKey;
            options.ModelName = valid.ModelName;
            makeInvalid(options);
        });
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<SlowQueryInterceptor>());
        Assert.Contains(invalidProperty, ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void InvalidOptions_FailWithDescriptiveErrorWithoutDependencyInjection(string invalidProperty, Action<SlowQueryAnalyzerOptions> makeInvalid)
    {
        var options = ValidOptions();
        makeInvalid(options);

        var ex = Assert.Throws<OptionsValidationException>(() => new SlowQueryAnalyzerService(options));
        Assert.Contains(invalidProperty, ex.Message);
    }

    [Fact]
    public void InvalidOptions_AreValidatedAtStartup()
    {
        var services = new ServiceCollection();
        services.AddSlowQueryAnalyzer(_ => { });
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(nameof(SlowQueryAnalyzerOptions.AzureOpenAIEndpoint), ex.Message);
        Assert.Contains(nameof(SlowQueryAnalyzerOptions.AzureOpenAIKey), ex.Message);
        Assert.Contains(nameof(SlowQueryAnalyzerOptions.ModelName), ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(250.5)]
    public void ValidOptions_AreAccepted(double thresholdMs)
    {
        var options = ValidOptions();
        options.SlowQueryThresholdMs = thresholdMs;

        Assert.NotNull(new SlowQueryAnalyzerService(options));
    }

    [Fact]
    public void NullConfigureAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddSlowQueryAnalyzer(null!));
    }
}
