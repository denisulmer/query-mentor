public class SlowQueryAnalyzerService
{
    private readonly OpenAIClient _client;
    private readonly SlowQueryAnalyzerOptions _options;

    public SlowQueryAnalyzerService(SlowQueryAnalyzerOptions options)
    {
        _options = options;
        _client = new OpenAIClient(new Uri(options.AzureOpenAIEndpoint), new AzureKeyCredential(options.AzureOpenAIKey));
    }

    public async Task<string> AnalyzeQueryAsync(QueryContext queryContext)
    {
        string jsonContext = JsonSerializer.Serialize(queryContext);
        string prompt = $@"Analyze the following SQL query for performance issues. Provide your response in JSON format with this structure:
        {
          ""problemFound"": ""Yes/No"",
          ""description"": ""[Short description if a problem is found, otherwise 'No significant issues detected']"",
          ""issues"": [
            {
              ""issue"": ""[Description of the issue]"",
              ""suggestion"": ""[Suggestion to fix the issue]"",
              ""impactGuess"": ""[High/Medium/Low]""
            }
          ]
        }
        
        Example response:
        {
          ""problemFound"": ""Yes"",
          ""description"": ""The query is inefficient due to a full table scan."",
          ""issues"": [
            {
              ""issue"": ""Missing index on the 'CustomerId' column"",
              ""suggestion"": ""Create an index on 'CustomerId' to speed up filtering."",
              ""impactGuess"": ""High""
            }
          ]
        }
        
        Query Context: {jsonContext}";

        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            Messages = { new ChatMessage(ChatRole.User, prompt) },
            MaxTokens = 500,
            Temperature = 0.5f
        };

        Response<ChatCompletions> response = await _client.GetChatCompletionsAsync("your-model-name", chatCompletionsOptions);

        return response.Value.Choices[0].Message.Content;
    }
}

