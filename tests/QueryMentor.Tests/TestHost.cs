using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QueryMentor.Tests;

/// <summary>
/// Wires QueryMentor up the same way an application does (via <see cref="SlowQueryAnalyzerExtensions.AddSlowQueryAnalyzer"/>),
/// but with a fake chat client and an in-memory SQLite database.
/// </summary>
internal sealed class TestHost : IDisposable
{
    public const string ValidResponse = """
        {
          "problemFound": "Yes",
          "description": "Full table scan.",
          "issues": [ { "issue": "Missing index", "suggestion": "Add an index", "impactGuess": "High" } ]
        }
        """;

    private readonly SqliteConnection _connection;

    private TestHost(Func<string, Task<string?>> respond, double thresholdMs)
    {
        Client = new FakeChatCompletionClient(async prompt =>
        {
            try
            {
                return await respond(prompt);
            }
            finally
            {
                FirstRequest.TrySetResult(prompt);
            }
        });

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
        services.AddSlowQueryAnalyzer(options =>
        {
            options.AzureOpenAIEndpoint = "https://example.openai.azure.com/";
            options.AzureOpenAIKey = "test-key";
            options.ModelName = "test-model";
            options.SlowQueryThresholdMs = thresholdMs;
        });
        // Replace the real Azure-backed service with one using the fake client.
        services.AddSingleton(new SlowQueryAnalyzerService(Client));
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        // Create the schema without the interceptor so it does not trigger an analysis.
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options);
        context.Database.EnsureCreated();
    }

    public FakeChatCompletionClient Client { get; }

    public CapturingLoggerProvider Logs { get; } = new();

    public ServiceProvider Services { get; }

    public TaskCompletionSource<string> FirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static TestHost Create(string? response = ValidResponse, double thresholdMs = 0)
        => new(_ => Task.FromResult(response), thresholdMs);

    public static TestHost Create(Func<string, Task<string?>> respond, double thresholdMs = 0)
        => new(respond, thresholdMs);

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(Services.GetRequiredService<SlowQueryInterceptor>())
            .Options;
        return new TestDbContext(options);
    }

    public async Task<string> WaitForFirstRequestAsync()
        => await FirstRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }
}
