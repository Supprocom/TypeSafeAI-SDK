# Supprocom TypeSafe AI SDK for .NET

An independent, strongly typed .NET client for [TypeSafe AI](https://typesafe.ai/). It supports the
System One API's noul, choice, and score primitives, model discovery, structured JSON state,
configurable retries, typed failures, Microsoft dependency injection, and cancellation.

The SDK targets .NET 8 and .NET 10 and is built against TypeSafe API specification `0.2.0`.

> This project is maintained by Supprocom. It is not an official TypeSafe AI SDK and is not
> affiliated with or endorsed by TypeSafe AI.

## Install

Install the core package from your configured NuGet source:

```bash
dotnet add package Supprocom.TypeSafeAI
```

For `IServiceCollection` and `IHttpClientFactory` integration:

```bash
dotnet add package Supprocom.TypeSafeAI.DependencyInjection
```

For source-based development, reference the projects in this repository directly:

```xml
<ProjectReference Include="path/to/TypeSafeAI-SDK/Supprocom.TypeSafeAI/Supprocom.TypeSafeAI.csproj" />
```

## API key

Create an API key in your TypeSafe AI account and keep it on the server. The default constructor
reads it from `TYPESAFE_API_KEY`:

```bash
export TYPESAFE_API_KEY="your-api-key"
```

Never put a TypeSafe API key in browser, desktop-distributed, or mobile client code. Send those
applications through a server you control.

## Quick start

```csharp
using Supprocom.TypeSafeAI;

using var client = new TypeSafeClient();

var result = await client.SystemOneAsync(
    state: "I was charged twice. Please help me get a refund.",
    questions: new Dictionary<string, TypeSafeQuestion>
    {
        ["billing"] = Question.Noul("Is this message about billing?"),
        ["tone"] = Question.Choice(
            "What is the customer's tone?",
            new Dictionary<string, string?>
            {
                ["calm"] = "Neutral or polite",
                ["frustrated"] = "Dissatisfied but constructive",
                ["angry"] = "Hostile or threatening",
            }),
        ["urgency"] = Question.Score(
            "How urgently should an agent respond?",
            "Can wait",
            "Needs attention this week",
            "Needs attention today"),
    });

double billingProbability = result.Noul("billing").Probability;
string tone = result.Choice("tone").Choice;
double urgency = result.Score("urgency").Score;
long tokens = result.Usage.TotalTokens;
```

Each answer also exposes confidence or its full probability distribution where the TypeSafe API
returns them. `SystemOneResponse.RawJson` and `TypeSafeAnswer.RawJson` provide forward-compatible
access to fields added by the service later.

## Structured state and criteria

State, instructions, and descriptions can be JSON strings, objects, or arrays. Instructions are
optional; pass `null` (or call `Question.Noul()` without arguments) to omit the `instructions` wire
property. Use `JsonNode` for direct control:

```csharp
using System.Text.Json.Nodes;

var state = new JsonObject
{
    ["subject"] = "Duplicate charge",
    ["message"] = "Please help.",
    ["customer"] = new JsonObject { ["plan"] = "business" },
};

var question = new ChoiceQuestion(
    instructions: new JsonObject
    {
        ["task"] = "Route this support request",
        ["rule"] = "Choose the primary owner",
    },
    criteria: new Dictionary<string, JsonNode?>
    {
        ["billing"] = JsonValue.Create("Charges, invoices, or payment problems"),
        ["technical"] = JsonValue.Create("Product behavior or failures"),
    });

var request = new SystemOneRequest(
    state,
    new Dictionary<string, TypeSafeQuestion> { ["route"] = question });

var result = await client.SystemOneAsync(request);
```

`SystemOneRequest.From(...)` can serialize an ordinary CLR value. For trimming or Native AOT,
prefer its `JsonTypeInfo<T>` overload.

## Configuration

Explicit options take precedence over environment variables, followed by SDK defaults.

| Setting | Environment variable | Default |
| --- | --- | --- |
| API key | `TYPESAFE_API_KEY` | Required |
| Base URL | `TYPESAFE_BASE_URL` | `https://api.typesafe.ai` |
| Model | `TYPESAFE_DEFAULT_MODEL` | `jev-latest` |
| Per-attempt timeout | — | 10 seconds |

```csharp
using var client = new TypeSafeClient(new TypeSafeClientOptions
{
    ApiKey = secretProvider.GetSecret("TypeSafeApiKey"),
    DefaultModel = "jev-latest",
    Timeout = TimeSpan.FromSeconds(15),
    Retry = new TypeSafeRetryOptions
    {
        MaxRetries = 3,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaximumDelay = TimeSpan.FromSeconds(5),
    },
    UserAgent = "my-service/2.1",
});
```

The default retry policy makes two retries after the initial attempt for HTTP 408, 429, and 5xx,
as well as connection failures and per-attempt timeouts. Backoff starts at 500 ms, is capped at five
seconds, and uses subtractive jitter. Valid `retry-after-ms` and `Retry-After` headers are honored up
to 60 seconds. Set `Retry = TypeSafeRetryOptions.None` to disable retries.

Per-call overrides and cancellation are available on every operation:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));

var result = await client.SystemOneAsync(
    request,
    new TypeSafeRequestOptions
    {
        Timeout = TimeSpan.FromSeconds(5),
        Retry = TypeSafeRetryOptions.None,
        Headers = new Dictionary<string, string>
        {
            ["X-Correlation-ID"] = correlationId,
        },
    },
    cancellation.Token);
```

Authentication, content type, SDK identity, runtime identity, and retry-count headers are protected
from caller overrides. Logs contain request summaries, not API keys, headers, or request bodies.

## Dependency injection

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddTypeSafeAI(options =>
{
    options.ApiKey = builder.Configuration["TypeSafeAI:ApiKey"];
    options.DefaultModel = builder.Configuration["TypeSafeAI:DefaultModel"];
});

// Inject ITypeSafeClient into an application service.
```

You can bind an entire section instead:

```csharp
builder.Services.AddTypeSafeAI(builder.Configuration.GetSection("TypeSafeAI"));
```

For proxy, handler, or connection-pool customization, configure the named client after registration:

```csharp
builder.Services
    .AddHttpClient(TypeSafeServiceCollectionExtensions.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    });
```

```json
{
  "TypeSafeAI": {
    "ApiKey": "use-a-secret-provider-in-production",
    "DefaultModel": "jev-latest",
    "Timeout": "00:00:10"
  }
}
```

## Models

Discover model names available to the authenticated account rather than hard-coding a dated name:

```csharp
IReadOnlyList<TypeSafeModel> models = await client.ListModelsAsync();
foreach (var model in models)
{
    Console.WriteLine($"{model.Name} ({model.ReleaseDate}): {model.Description}");
}
```

## Errors

Configuration, serialization, transport, and protocol failures derive from `TypeSafeException`.
Request constructors use standard argument exceptions to reject invalid values immediately.

- `TypeSafeConfigurationException`: invalid or missing local configuration.
- `TypeSafeRequestSerializationException`: request state or criteria could not be encoded as JSON.
- `TypeSafeConnectionException`: DNS, TLS, connection, or interrupted-body failure.
- `TypeSafeTimeoutException`: one HTTP attempt exceeded its timeout.
- `TypeSafeResponseValidationException`: a successful response did not match the API contract.
- `TypeSafeApiException`: a non-success HTTP response, with status, body, response headers, endpoint,
  and `x-typesafe-request-id`.

HTTP 400, 401, 403, 404, 422, 429, and 5xx responses have dedicated subclasses. Caller cancellation
remains an `OperationCanceledException`, so application cancellation logic continues to work normally.

## Development

The repository uses an isolated, centrally versioned .NET solution:

```bash
dotnet restore Supprocom.TypeSafeAI.slnx
dotnet build Supprocom.TypeSafeAI.slnx --configuration Release --no-restore
dotnet test Supprocom.TypeSafeAI.slnx --configuration Release --no-build
dotnet pack Supprocom.TypeSafeAI.slnx --configuration Release --no-build --output artifacts
```

The protocol tests use an in-memory HTTP handler and do not require an API key or consume TypeSafe
credits. To run the sample against the live service:

```bash
TYPESAFE_API_KEY="your-api-key" dotnet run --project Supprocom.TypeSafeAI.Sample
```

See the [protocol compatibility notes](https://github.com/Supprocom/TypeSafeAI-SDK/blob/main/docs/protocol-compatibility.md)
for the researched contract, source versions, deliberate schema decisions, and the maintenance
checklist.

## References

- [TypeSafe AI documentation](https://docs.typesafe.ai/)
- [System One](https://docs.typesafe.ai/concepts/system-one)
- [Noul](https://docs.typesafe.ai/primitives/noul)
- [Choice](https://docs.typesafe.ai/primitives/choice)
- [Score](https://docs.typesafe.ai/primitives/score)
- [Live OpenAPI specification](https://api.typesafe.ai/openapi.json)
- [Official JavaScript SDK](https://github.com/typesafe-ai/typesafe-sdk-js)
- [Official Python SDK](https://github.com/typesafe-ai/typesafe-sdk-python)

## License

This project uses [AGPL-3.0-only](LICENSE). See [NOTICE](NOTICE) for the
project notice and source offer.
