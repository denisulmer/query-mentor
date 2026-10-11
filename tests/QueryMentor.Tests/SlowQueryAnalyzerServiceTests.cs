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
