using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace QueryMentor.Tests;

public class InterceptorTests
{
    [Fact]
    public async Task SlowAsyncQuery_IsSentForAnalysis()
    {
        using var host = TestHost.Create();
        await using var context = host.CreateContext();

        await context.Blogs.Where(b => b.Name == "x").ToListAsync();

        var prompt = await host.WaitForFirstRequestAsync();
        Assert.Contains("FROM", prompt);
        Assert.Contains("Blogs", prompt);
    }

    [Fact]
    public async Task SlowSyncQuery_IsSentForAnalysis()
    {
        using var host = TestHost.Create();
        using var context = host.CreateContext();

        _ = context.Blogs.Where(b => b.Name == "x").ToList();

        var prompt = await host.WaitForFirstRequestAsync();
        Assert.Contains("Blogs", prompt);
    }

    [Fact]
    public async Task SlowNonQuery_IsSentForAnalysis()
    {
        using var host = TestHost.Create();
        await using var context = host.CreateContext();

        await context.Blogs.Where(b => b.Name == "x").ExecuteDeleteAsync();

        var prompt = await host.WaitForFirstRequestAsync();
        Assert.Contains("DELETE", prompt);
    }

    [Fact]
    public async Task QueryBelowThreshold_IsNotAnalyzed()
    {
        using var host = TestHost.Create(thresholdMs: 60_000);
        await using var context = host.CreateContext();

        await context.Blogs.ToListAsync();

        await Task.Delay(200);
        Assert.Empty(host.Client.Requests);
    }

    [Fact]
    public async Task AnalysisFailure_DoesNotFailTheApplicationQuery()
    {
        using var host = TestHost.Create(_ => throw new HttpRequestException("Azure OpenAI is down"));
        await using var context = host.CreateContext();
        context.Blogs.Add(new Blog { Name = "a" });
        await context.SaveChangesAsync();

        var blogs = await context.Blogs.ToListAsync();

        Assert.Single(blogs);
    }

    [Fact]
    public async Task UnparseableAnalysis_DoesNotFailTheApplicationQuery()
    {
        using var host = TestHost.Create("Sorry, I can't help with that.");
        await using var context = host.CreateContext();

        var blogs = await context.Blogs.ToListAsync();

        Assert.Empty(blogs);
    }

    [Fact]
    public async Task SlowAnalysis_DoesNotDelayTheApplicationQuery()
    {
        var release = new TaskCompletionSource<string?>();
        using var host = TestHost.Create(_ => release.Task);
        await using var context = host.CreateContext();

        var query = context.Blogs.ToListAsync();
        var completed = await Task.WhenAny(query, Task.Delay(TimeSpan.FromSeconds(5)));

        release.SetResult(TestHost.ValidResponse);
        Assert.Same(query, completed);
    }

    [Fact]
    public async Task AnalysisResult_IsLoggedAsJsonWarning()
    {
        using var host = TestHost.Create();
        await using var context = host.CreateContext();

        await context.Blogs.ToListAsync();

        var entry = await host.Logs.WaitForAsync(e => e.Category == typeof(SlowQueryInterceptor).FullName && e.Level == LogLevel.Warning);
        Assert.Contains("\"problemFound\": \"Yes\"", entry.Message);
        Assert.Contains("\"impactGuess\": \"High\"", entry.Message);
        Assert.Contains("FROM \"Blogs\"", entry.Message);
    }

    [Fact]
    public async Task AnalysisFailure_IsLoggedWithException()
    {
        using var host = TestHost.Create(_ => throw new HttpRequestException("Azure OpenAI is down"));
        await using var context = host.CreateContext();

        await context.Blogs.ToListAsync();

        var entry = await host.Logs.WaitForAsync(e => e.Exception is HttpRequestException);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("could not analyze", entry.Message);
    }

    [Fact]
    public async Task SameQuery_IsAnalyzedOnlyOnce()
    {
        using var host = TestHost.Create();
        await using var context = host.CreateContext();

        await context.Blogs.Where(b => b.Name == "x").ToListAsync();
        await host.Logs.WaitForAsync(e => e.Message.Contains("QueryMentor analysis", StringComparison.Ordinal));
        await context.Blogs.Where(b => b.Name == "x").ToListAsync();
        await context.Blogs.Where(b => b.Name == "x").ToListAsync();
        await Task.Delay(200);

        Assert.Single(host.Client.Requests);
    }

    [Fact]
    public async Task FailedAnalysis_IsRetriedTheNextTimeTheQueryIsSlow()
    {
        var calls = 0;
        using var host = TestHost.Create(_ => Interlocked.Increment(ref calls) == 1
            ? throw new HttpRequestException("transient")
            : Task.FromResult<string?>(TestHost.ValidResponse));
        await using var context = host.CreateContext();

        await context.Blogs.ToListAsync();
        await host.Logs.WaitForAsync(e => e.Exception is HttpRequestException);
        await context.Blogs.ToListAsync();

        await host.Logs.WaitForAsync(e => e.Message.Contains("QueryMentor analysis", StringComparison.Ordinal));
        Assert.Equal(2, host.Client.Requests.Count);
    }

    [Fact]
    public async Task ConcurrentAnalyses_AreLimited()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = TestHost.Create(_ => release.Task);
        await using var context = host.CreateContext();

        // Distinct queries, so deduplication does not kick in.
        for (var i = 0; i < SlowQueryInterceptor.MaxConcurrentAnalyses + 3; i++)
        {
            var sql = "SELECT * FROM \"Blogs\" WHERE \"Id\" > " + i.ToString(CultureInfo.InvariantCulture);
            await context.Blogs.FromSqlRaw(sql).ToListAsync();
        }

        await Task.Delay(300);
        Assert.Equal(SlowQueryInterceptor.MaxConcurrentAnalyses, host.Client.Requests.Count);
        release.SetResult(TestHost.ValidResponse);
    }

    [Fact]
    public async Task QueryOnTableAfterViewMappedEntity_IncludesSchema()
    {
        // BlogSummary (mapped to a view, so GetTableName() is null) sorts between Blog and Post.
        using var host = TestHost.Create();
        await using var context = host.CreateContext();

        var posts = await context.Posts.Where(p => p.Title == "x").ToListAsync();

        Assert.Empty(posts);
        var prompt = await host.WaitForFirstRequestAsync();
        Assert.Contains("\"TableName\":\"Posts\"", prompt);
        Assert.Contains("IX_Posts_Title", prompt);
    }
}
