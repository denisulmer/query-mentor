# QueryMentor

[![CI](https://github.com/denisulmer/query-mentor/actions/workflows/ci.yml/badge.svg)](https://github.com/denisulmer/query-mentor/actions/workflows/ci.yml)

**QueryMentor** helps ASP.NET Core / .NET developers find and fix slow database queries. It plugs into
Entity Framework Core as a command interceptor, notices queries that take longer than a configurable
threshold, and asks an Azure OpenAI model to analyze them, together with the schema of the tables they use.
The model's findings (problems found, suggestions such as missing indexes, and an estimated impact) are
written to your application's log.

> **Note:** AI suggestions can be wrong or incomplete. Always verify them (for example with the actual
> execution plan) before applying them to a production database.

## Table of contents

- [Features](#features)
- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [Usage](#usage)
- [Security and privacy](#security-and-privacy)
- [Architecture](#architecture)
- [Development](#development)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Automatic slow query detection** for every command EF Core executes (queries, `SaveChanges`,
  `ExecuteUpdate`/`ExecuteDelete`, scalar queries; sync and async) above a configurable threshold.
- **AI-powered analysis** with Azure OpenAI. The model receives the SQL, its execution time and the
  relevant table definitions from your EF Core model (columns, store types, nullability, primary keys
  and indexes).
- **Actionable output**: whether a problem was found, a short description, and a list of issues, each with
  a suggestion and an impact estimate (High/Medium/Low), logged as JSON through `ILogger`.
- **Safe to leave on**: the analysis runs in the background and never delays or fails your queries;
  each distinct query is analyzed once; concurrent analyses are capped; schema changes, migrations and
  EF Core's own catalog queries are ignored.
- **Fail-fast configuration**: missing or invalid settings are reported when the application starts.
- **Simple integration**: one `AddSlowQueryAnalyzer(...)` call and one `AddInterceptors(...)` call.

<!-- Screenshot placeholder: add a screenshot of a QueryMentor analysis in the console/log viewer here. -->

## How it works

1. `SlowQueryInterceptor` is notified by EF Core after each database command and checks its duration.
2. If the command is slower than `SlowQueryThresholdMs` (and is a query or DML statement that has not been
   analyzed yet), QueryMentor collects the definitions of the referenced tables from the EF Core model.
3. In the background, `SlowQueryAnalyzerService` sends the SQL, the execution time and the table
   definitions to your Azure OpenAI deployment and asks for a JSON answer.
4. The answer is parsed into an `AnalysisResult` and logged as a warning.

## Requirements

- .NET 8 or .NET 10 (the package targets `net8.0` and `net10.0`)
- Entity Framework Core 8 or 10 with a relational provider (SQL Server, PostgreSQL, SQLite, MySQL, ...)
- An Azure OpenAI resource with a chat model deployment (for example `gpt-4o` or `gpt-4o-mini`),
  its endpoint and an API key

## Installation

```bash
dotnet add package QueryMentor
```

> The package is not yet published on nuget.org. Until then, reference the project directly
> (`dotnet add reference path/to/src/QueryMentor/QueryMentor.csproj`) or build a package with
> `dotnet pack` (see [Development](#development)).

## Quick start

`Program.cs` of an ASP.NET Core application:

```csharp
using Microsoft.EntityFrameworkCore;
using QueryMentor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSlowQueryAnalyzer(options =>
    builder.Configuration.GetSection("QueryMentor").Bind(options));

builder.Services.AddDbContext<YourDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
           .AddInterceptors(sp.GetRequiredService<SlowQueryInterceptor>());
});

var app = builder.Build();
// ...
app.Run();
```

`appsettings.json` (keep the key itself out of source control, see below):

```json
{
  "QueryMentor": {
    "AzureOpenAIEndpoint": "https://your-resource.openai.azure.com/",
    "ModelName": "your-deployment-name",
    "SlowQueryThresholdMs": 500
  }
}
```

Store the API key with [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) during
development and with environment variables or Azure Key Vault in production:

```bash
dotnet user-secrets set "QueryMentor:AzureOpenAIKey" "<your-api-key>"
# or
export QueryMentor__AzureOpenAIKey="<your-api-key>"
```

The options can also be set in code:

```csharp
builder.Services.AddSlowQueryAnalyzer(options =>
{
    options.AzureOpenAIEndpoint = "https://your-resource.openai.azure.com/";
    options.AzureOpenAIKey = builder.Configuration["QueryMentor:AzureOpenAIKey"]!;
    options.ModelName = "your-deployment-name";
    options.SlowQueryThresholdMs = 500; // analyze queries slower than 500 ms
});
```

## Configuration

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `AzureOpenAIEndpoint` | yes | | Your Azure OpenAI endpoint, e.g. `https://your-resource.openai.azure.com/`. Must be an absolute http(s) URL. |
| `AzureOpenAIKey` | yes | | API key of the Azure OpenAI resource. |
| `ModelName` | yes | | Name of the chat model **deployment** to use. |
| `SlowQueryThresholdMs` | no | `500` | Commands that take longer than this many milliseconds are analyzed. Must be `>= 0`; `0` analyzes every command (useful for trying it out). |

The options are validated when the host starts. If something is missing, startup fails with an
`OptionsValidationException` that lists every problem, for example
`AzureOpenAIEndpoint is required.`

Fixed behavior (not configurable at the moment):

- Each distinct SQL text is analyzed at most once per application run. If an analysis fails, the query is
  analyzed again the next time it is slow.
- At most `SlowQueryInterceptor.MaxConcurrentAnalyses` (2) analyses run at the same time; slow queries
  that occur while the limit is reached are skipped (and analyzed the next time they are slow).
- Commands that start with DDL/maintenance keywords (`CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `PRAGMA`, ...),
  that touch `__EFMigrationsHistory`, or that read system catalogs are not analyzed.
- The model is called with a temperature of 0.5 and a limit of 500 output tokens.

## Usage

Once configured, there is nothing else to do: run your application and use it as usual. When a slow query
is analyzed, QueryMentor writes a log entry with category `QueryMentor.SlowQueryInterceptor`, level
`Warning` and event id `1`:

```text
warn: QueryMentor.SlowQueryInterceptor[1]
      Slow query detected (1834 ms). QueryMentor analysis:
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
      Query:
      SELECT [o].[Id], [o].[CustomerId], [o].[Total]
      FROM [Orders] AS [o]
      WHERE [o].[CustomerId] = @__customerId_0
```

Because the output goes through `ILogger`, it ends up wherever your logs go (console, Application
Insights, Seq, ...) and can be filtered by category:

```json
{
  "Logging": {
    "LogLevel": {
      "QueryMentor": "Warning"
    }
  }
}
```

Other log events (category `QueryMentor.SlowQueryInterceptor`):

| Event id | Level | Meaning |
| --- | --- | --- |
| 1 | Warning | A slow query was analyzed (see above). |
| 2 | Warning | The analysis failed (e.g. network error, invalid key, unparseable answer). Includes the exception. |
| 3 | Debug | A slow query was skipped because the maximum number of concurrent analyses was reached. |
| 4 | Debug | A slow query was skipped because it has already been analyzed. |
| 5 | Debug | A slow query was detected and sent for analysis. |
| 6 | Debug | Table definitions could not be collected; the query is analyzed without them. |
| 7 | Error | Unexpected error inside the interceptor. The database command itself is not affected. |

### Analyzing a query yourself

`SlowQueryAnalyzerService` can also be used directly, for example from a test or an admin endpoint:

```csharp
var analyzer = app.Services.GetRequiredService<SlowQueryAnalyzerService>();

AnalysisResult result = await analyzer.AnalyzeQueryAsync(new QueryContext
{
    CommandText = "SELECT * FROM Orders WHERE CustomerId = @p0",
    ExecutionTime = 1834,
});

foreach (var issue in result.Issues)
{
    Console.WriteLine($"{issue.ImpactGuess}: {issue.Issue} -> {issue.Suggestion}");
}
```

`AnalyzeQueryAsync` throws an `InvalidOperationException` if the model's answer cannot be parsed, and the
Azure SDK's exceptions (e.g. `ClientResultException`) if the request fails.

## Security and privacy

- **Data sent to Azure OpenAI**: the SQL text of slow queries, their execution time, and the names, types
  and indexes of the referenced tables. **Parameter values are not sent**; EF Core uses parameters
  (`@p0`) for values in LINQ queries. Values that are embedded directly in the SQL text, for example with
  `FromSqlRaw` and string concatenation or with `EF.Constant(...)`, *are* sent. Make sure this is
  acceptable for your data and your Azure OpenAI data-processing terms.
- **Data written to logs**: the SQL text of analyzed queries (same caveat as above).
- **API key**: do not commit it. Use user secrets, environment variables or a secret store such as Azure
  Key Vault (see [Quick start](#quick-start)).
- **AI output** is only logged; QueryMentor never executes SQL suggested by the model.

## Architecture

```text
src/QueryMentor/
├── SlowQueryAnalyzerExtensions.cs      AddSlowQueryAnalyzer(): DI registration + options validation
├── SlowQueryAnalyzerOptions.cs         Public options
├── SlowQueryAnalyzerOptionsValidator.cs
├── SlowQueryInterceptor.cs             EF Core DbCommandInterceptor: threshold check, dedup,
│                                       concurrency limit, background analysis, logging
├── CommandClassifier.cs                Skips DDL, migrations and catalog queries
├── QuerySchemaExtractor.cs             Finds referenced tables in the SQL and describes them
│                                       from EF Core's relational model
├── SlowQueryAnalyzerService.cs         Builds the prompt and parses the answer (public API)
├── AnalysisResponseParser.cs           Tolerant JSON parsing of the model's answer
├── IChatCompletionClient.cs            Internal seam for the LLM call (faked in tests)
├── AzureOpenAIChatCompletionClient.cs  Azure OpenAI implementation (Azure.AI.OpenAI 2.x)
├── Log.cs                              Source-generated log messages
└── AnalysisResult.cs, QueryIssue.cs, QueryContext.cs, TableDefinition.cs   Data contracts
```

```text
EF Core command executed
        │
        ▼
SlowQueryInterceptor ── fast, DDL/migration, already analyzed or at capacity? ──► ignore
        │ slow query
        ▼
QuerySchemaExtractor (on the calling thread, from DbContext.Model)
        │ QueryContext { CommandText, ExecutionTime, Tables }
        ▼  background task
SlowQueryAnalyzerService ──► Azure OpenAI chat completion ──► AnalysisResponseParser
        │ AnalysisResult
        ▼
ILogger<SlowQueryInterceptor> (Warning, JSON)
```

## Development

Prerequisites: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (needed to build the `net10.0`
target; see `global.json`) and the .NET 8 runtime (to run the `net8.0` tests).

```bash
git clone https://github.com/denisulmer/query-mentor.git
cd query-mentor

dotnet restore
dotnet build                              # warnings are treated as errors
dotnet test                               # runs on net8.0 and net10.0
dotnet format --verify-no-changes         # code style (see .editorconfig)
dotnet pack src/QueryMentor/QueryMentor.csproj -c Release -o artifacts
```

Testing notes:

- The tests do not call Azure OpenAI. `SlowQueryAnalyzerService` talks to the model through the internal
  `IChatCompletionClient`, which the tests replace with a fake; the Azure client itself is tested against a
  recording HTTP handler.
- Interceptor tests run real EF Core queries against an in-memory SQLite database.

Continuous integration (`.github/workflows/ci.yml`) runs the same restore, format check, build, test and pack
steps for every push to `main` and every pull request. Dependabot keeps NuGet packages and GitHub Actions
up to date.

## Contributing

Contributions are welcome:

1. Open an issue to discuss bigger changes first.
2. Create a branch, keep commits focused, and add or update tests.
3. Make sure `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass.
4. Open a pull request describing the change and how you tested it.

## License

No license file has been added to this repository yet, so default copyright applies. A license (for example
MIT) still needs to be chosen and added as a `LICENSE` file before the package is published.
