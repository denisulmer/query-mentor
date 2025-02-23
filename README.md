# QueryMentor

**QueryMentor** is a NuGet package designed to help ASP.NET developers detect and optimize slow database queries using AI-powered analysis. By seamlessly integrating with Entity Framework Core and leveraging Azure OpenAI, QueryMentor automatically identifies slow queries and provides actionable suggestions to improve their performance.

## Table of Contents
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [License](#license)
- [Contact](#contact)

## Features
- Automatic detection of slow queries based on a configurable threshold.
- AI-powered analysis using Azure OpenAI to identify performance issues.
- Suggestions for optimizing queries, including estimated impact.
- Easy integration into ASP.NET Core applications.


## Prerequisites
Before using QueryMentor, ensure you have the following:
- An ASP.NET Core application (version 6.0 or later).
- Entity Framework Core (version 6.0 or later).
- An Azure OpenAI account with an API key and endpoint.
- .NET 6.0 or later.


## Installation
Install the QueryMentor NuGet package using the following command:

```bash
dotnet add package QueryMentor
```

Alternatively, use the NuGet Package Manager in Visual Studio to search for and install QueryMentor.
Configuration

In your Startup.cs or Program.cs, configure QueryMentor with your Azure OpenAI settings and add the interceptor to your DbContext:

## Configuration

In your Startup.cs or Program.cs, configure QueryMentor with your Azure OpenAI settings and add the interceptor to your DbContext:

```csharp
services.AddSlowQueryAnalyzer(options =>
{
    options.AzureOpenAIEndpoint = "https://your-endpoint.openai.azure.com/";
    options.AzureOpenAIKey = "your-api-key";
    options.SlowQueryThresholdMs = 500; // Detect queries slower than 500ms
});

services.AddDbContext<YourDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<SlowQueryInterceptor>();
    options.UseSqlServer("your-connection-string")
           .AddInterceptors(interceptor);
});
```

Make sure to replace the placeholders with your actual Azure OpenAI endpoint, API key, and database connection string.

## Usage

Once configured, QueryMentor will automatically detect slow queries during the execution of your application. When a slow query is identified, it will be sent to Azure OpenAI for analysis, and the results will be logged to the console in JSON format.


### Example Output

```json
{
  "problemFound": "Yes",
  "description": "The query is inefficient due to a full table scan.",
  "issues": [
    {
      "issue": "Missing index on the 'CustomerId' column",
      "suggestion": "Create an index on 'CustomerId' to speed up filtering.",
      "impactGuess": "High"
    }
  ]
}
```

You can customize the logging or integrate the analysis results into your application as needed.

**Note**: The analysis provided by Azure OpenAI is based on the information available and may not cover all possible scenarios. Always verify suggestions before applying them to your production environment.

## License

QueryMentor is licensed under the MIT License. See the LICENSE file for more details.
