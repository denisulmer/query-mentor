namespace QueryMentor.Tests;

public class SlowQueryAnalyzerServiceTests
{
    [Fact]
    public async Task AnalyzeQueryAsync_SendsSystemPromptAndQueryContext()
    {
        var client = new FakeChatCompletionClient("""{"ProblemFound":"No","Description":"Fine","Issues":[]}""");
        var service = new SlowQueryAnalyzerService(client);

        await service.AnalyzeQueryAsync(new QueryContext { CommandText = "SELECT * FROM Orders", ExecutionTime = 1234 });

        var request = Assert.Single(client.Requests);
        Assert.Equal(SlowQueryAnalyzerService.SystemPrompt, request.SystemPrompt);
        Assert.Contains("SELECT * FROM Orders", request.UserPrompt);
        Assert.Contains("1234", request.UserPrompt);
        Assert.Contains("\"problemFound\": \"Yes/No\"", request.UserPrompt);
    }
}

public class AnalysisResponseParsingTests
{
    private const string ValidResponse = """
        {
          "problemFound": "Yes",
          "description": "Full table scan.",
          "issues": [ { "issue": "Missing index", "suggestion": "Add an index", "impactGuess": "High" } ]
        }
        """;

    private static Task<AnalysisResult> AnalyzeWithResponse(string? response)
        => new SlowQueryAnalyzerService(new FakeChatCompletionClient(response)).AnalyzeQueryAsync(new QueryContext { CommandText = "SELECT 1" });

    [Fact]
    public async Task CamelCaseResponse_AsRequestedInPrompt_IsMapped()
    {
        var result = await AnalyzeWithResponse(ValidResponse);

        Assert.Equal("Yes", result.ProblemFound);
        Assert.Equal("Full table scan.", result.Description);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("Missing index", issue.Issue);
        Assert.Equal("Add an index", issue.Suggestion);
        Assert.Equal("High", issue.ImpactGuess);
    }

    [Fact]
    public async Task ResponseWrappedInMarkdownCodeFence_IsMapped()
    {
        var result = await AnalyzeWithResponse("Here you go:\n```json\n" + ValidResponse + "\n```");

        Assert.Equal("Yes", result.ProblemFound);
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task ResponseWithoutIssues_HasEmptyIssueList()
    {
        var result = await AnalyzeWithResponse("""{"problemFound":"No","description":"No significant issues detected"}""");

        Assert.Equal("No", result.ProblemFound);
        Assert.NotNull(result.Issues);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Sorry, I can't help with that.")]
    [InlineData("{\"problemFound\": \"Yes\", \"description\": \"trunc")]
    [InlineData("null")]
    public async Task InvalidResponse_ThrowsDescriptiveException(string? response)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AnalyzeWithResponse(response));

        Assert.Contains("could not be parsed", ex.Message);
    }

    [Fact]
    public async Task NullQueryContext_Throws()
    {
        var service = new SlowQueryAnalyzerService(new FakeChatCompletionClient(ValidResponse));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AnalyzeQueryAsync(null!));
    }
}
