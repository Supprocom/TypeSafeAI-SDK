using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Supprocom.TypeSafeAI.Tests;

public sealed class TypeSafeClientTests
{
    private const string SystemOneJson = """
        {
          "model": "jev-2026-09-15",
          "answers": {
            "billing": { "type": "noul", "noul": 0.98 },
            "tone": {
              "type": "choice",
              "choice": "calm",
              "confidence": 0.9,
              "probabilities": { "calm": 0.9, "angry": 0.1 }
            },
            "urgency": {
              "type": "score",
              "score": 1.7,
              "confidence": 0.8,
              "legend": { "0": "Can wait", "1": "This week", "2": { "label": "Today" } },
              "probabilities": { "0": 0.1, "1": 0.1, "2": 0.8 }
            }
          },
          "usage": { "input_tokens": 120, "output_tokens": 12 }
        }
        """;

    [Fact]
    public async Task SystemOneSerializesRequestAndParsesTypedAnswers()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(
                HttpStatusCode.OK,
                SystemOneJson,
                "request-123")));
        using var httpClient = CreateHttpClient(handler);
        using var client = new TypeSafeClient(httpClient, new TypeSafeClientOptions
        {
            ApiKey = "secret-key",
            BaseUrl = new Uri("https://unit.test/proxy"),
            DefaultModel = "jev-test",
        });

        var state = JsonNode.Parse("""{"subject":"Duplicate charge","messages":["Please help"]}""")!;
        var request = new SystemOneRequest(
            state,
            new Dictionary<string, TypeSafeQuestion>
            {
                ["billing"] = Question.Noul("Is this about billing?"),
                ["tone"] = Question.Choice(
                    "What is the tone?",
                    new Dictionary<string, string?>
                    {
                        ["calm"] = "Neutral or polite",
                        ["angry"] = null,
                    }),
                ["urgency"] = Question.Score("How urgent is this?", "Can wait", "This week", "Today"),
            });

        var result = await client.SystemOneAsync(request);

        Assert.Equal("jev-2026-09-15", result.Model);
        Assert.Equal("request-123", result.RequestId);
        Assert.Equal(0.98, result.Noul("billing").Probability, 5);
        Assert.Equal("calm", result.Choice("tone").Choice);
        Assert.Equal(0.1, result.Choice("tone").Probabilities["angry"], 5);
        Assert.Equal(1.7, result.Score("urgency").Score, 5);
        Assert.Equal("Today", result.Score("urgency").Legend[2].GetProperty("label").GetString());
        Assert.Equal(0.8, result.Score("urgency").Probabilities[2], 5);
        Assert.Equal(132, result.Usage.TotalTokens);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://unit.test/proxy/v1/systemone", sent.Uri.AbsoluteUri);
        Assert.Equal("Bearer secret-key", sent.Header("Authorization"));
        Assert.StartsWith("supprocom-typesafe-dotnet/", sent.Header("User-Agent"), StringComparison.Ordinal);
        Assert.StartsWith("supprocom-typesafe-dotnet/", sent.Header("X-TypeSafe-SDK"), StringComparison.Ordinal);
        Assert.StartsWith("dotnet/", sent.Header("X-TypeSafe-Runtime"), StringComparison.Ordinal);
        Assert.Null(sent.Header("X-TypeSafe-Retry-Count"));

        using var document = JsonDocument.Parse(Assert.IsType<string>(sent.Body));
        var root = document.RootElement;
        Assert.Equal("jev-test", root.GetProperty("model").GetString());
        Assert.Equal("Duplicate charge", root.GetProperty("state").GetProperty("subject").GetString());
        Assert.Equal("noul", root.GetProperty("questions").GetProperty("billing").GetProperty("type").GetString());
        Assert.Equal(
            "Neutral or polite",
            root.GetProperty("questions").GetProperty("tone").GetProperty("criteria").GetProperty("calm").GetString());
        Assert.Equal(
            3,
            root.GetProperty("questions").GetProperty("urgency").GetProperty("criteria").GetArrayLength());
    }

    [Fact]
    public async Task OmittedInstructionsAreNotSerialized()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);
        var request = new SystemOneRequest(
            "A support message",
            new Dictionary<string, TypeSafeQuestion>
            {
                ["billing"] = Question.Noul(),
                ["tone"] = Question.Choice(null, "calm", "angry"),
                ["urgency"] = Question.Score(null, "low", "medium", "high"),
            });

        await client.SystemOneAsync(request);

        var sent = Assert.Single(handler.Requests);
        using var document = JsonDocument.Parse(Assert.IsType<string>(sent.Body));
        var questions = document.RootElement.GetProperty("questions");
        Assert.False(questions.GetProperty("billing").TryGetProperty("instructions", out _));
        Assert.False(questions.GetProperty("tone").TryGetProperty("instructions", out _));
        Assert.False(questions.GetProperty("urgency").TryGetProperty("instructions", out _));
    }

    [Fact]
    public async Task ListModelsUsesGetAndParsesMetadata()
    {
        const string json = """
            {"models":[{"name":"jev-latest","description":"General purpose","release_date":"2026-09-15"}]}
            """;
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, json)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);

        var models = await client.ListModelsAsync();

        var model = Assert.Single(models);
        Assert.Equal("jev-latest", model.Name);
        Assert.Equal("General purpose", model.Description);
        Assert.Equal("2026-09-15", model.ReleaseDate);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://unit.test/v1/models", sent.Uri.AbsoluteUri);
        Assert.Null(sent.Body);
        Assert.Null(sent.Header("Content-Type"));
    }

    [Fact]
    public async Task TransientStatusIsRetriedWithRetryHeader()
    {
        using var handler = new TestHttpMessageHandler(static (_, requestNumber, _) =>
        {
            if (requestNumber == 1)
            {
                var response = TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.TooManyRequests,
                    "{\"error\":\"slow down\"}");
                response.Headers.TryAddWithoutValidation("retry-after-ms", "0");
                return Task.FromResult(response);
            }

            return Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson));
        });
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient, new TypeSafeRetryOptions
        {
            MaxRetries = 1,
            InitialDelay = TimeSpan.Zero,
            MaximumDelay = TimeSpan.Zero,
        });

        var result = await client.SystemOneAsync(BasicRequest());

        Assert.Equal("jev-2026-09-15", result.Model);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Null(handler.Requests[0].Header("X-TypeSafe-Retry-Count"));
        Assert.Equal("1", handler.Requests[1].Header("X-TypeSafe-Retry-Count"));
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
    }

    [Fact]
    public async Task ConnectionFailureIsRetried()
    {
        using var handler = new TestHttpMessageHandler(static (_, requestNumber, _) =>
            requestNumber == 1
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("network unavailable"))
                : Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient, new TypeSafeRetryOptions
        {
            MaxRetries = 1,
            InitialDelay = TimeSpan.Zero,
            MaximumDelay = TimeSpan.Zero,
        });

        var result = await client.SystemOneAsync(BasicRequest());

        Assert.Equal("jev-2026-09-15", result.Model);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AttemptTimeoutThrowsTypedException()
    {
        using var handler = new TestHttpMessageHandler(static async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson);
        });
        using var httpClient = CreateHttpClient(handler);
        using var client = new TypeSafeClient(httpClient, new TypeSafeClientOptions
        {
            ApiKey = "test-key",
            BaseUrl = new Uri("https://unit.test"),
            Timeout = TimeSpan.FromMilliseconds(10),
            Retry = TypeSafeRetryOptions.None,
        });

        var exception = await Assert.ThrowsAsync<TypeSafeTimeoutException>(
            () => client.SystemOneAsync(BasicRequest()));

        Assert.Equal(TimeSpan.FromMilliseconds(10), exception.Timeout);
    }

    [Fact]
    public async Task CallerCancellationRemainsOperationCanceledException()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SystemOneAsync(BasicRequest(), cancellationToken: cancellation.Token));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(529)]
    public async Task HttpErrorsMapToSpecificExceptionTypes(int statusCode)
    {
        using var handler = new TestHttpMessageHandler((_, _, _) =>
        {
            var response = TestHttpMessageHandler.JsonResponse(
                (HttpStatusCode)statusCode,
                "{\"detail\":[{\"loc\":[\"body\",\"questions\",\"tone\"],\"msg\":\"invalid\"}]}",
                "error-request");
            response.Headers.TryAddWithoutValidation("retry-after-ms", "250");
            return Task.FromResult(response);
        });
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient, TypeSafeRetryOptions.None);

        var exception = await Record.ExceptionAsync(() => client.ListModelsAsync());

        var expectedType = statusCode switch
        {
            400 => typeof(TypeSafeBadRequestException),
            401 => typeof(TypeSafeAuthenticationException),
            403 => typeof(TypeSafePermissionException),
            404 => typeof(TypeSafeNotFoundException),
            422 => typeof(TypeSafeValidationException),
            429 => typeof(TypeSafeRateLimitException),
            >= 500 => typeof(TypeSafeServerException),
            _ => typeof(TypeSafeApiException),
        };
        var apiException = Assert.IsAssignableFrom<TypeSafeApiException>(exception);
        Assert.IsType(expectedType, apiException);
        Assert.Equal((HttpStatusCode)statusCode, apiException.StatusCode);
        Assert.Equal("error-request", apiException.RequestId);
        Assert.Contains("questions.tone: invalid", apiException.Message, StringComparison.Ordinal);
        if (apiException is TypeSafeRateLimitException rateLimit)
        {
            Assert.Equal(TimeSpan.FromMilliseconds(250), rateLimit.RetryAfter);
        }
    }

    [Fact]
    public async Task InvalidSuccessBodyThrowsResponseValidationException()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(
                HttpStatusCode.OK,
                "{\"models\":{}}",
                "invalid-response")));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<TypeSafeResponseValidationException>(
            () => client.ListModelsAsync());

        Assert.Equal("models", exception.FieldPath);
        Assert.Equal("invalid-response", exception.RequestId);
    }

    [Fact]
    public async Task OutOfRangeProbabilityThrowsResponseValidationException()
    {
        const string invalidJson = """
            {
              "model":"jev-test",
              "answers":{"billing":{"type":"noul","noul":1.1}},
              "usage":{"input_tokens":1,"output_tokens":1}
            }
            """;
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, invalidJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<TypeSafeResponseValidationException>(
            () => client.SystemOneAsync(BasicRequest()));

        Assert.Equal("answers.billing.noul", exception.FieldPath);
    }

    [Theory]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"noul","noul":"0.5"}},"usage":{"input_tokens":1,"output_tokens":1}}""",
        "answers.billing.noul")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"choice","choice":"calm","confidence":null,"probabilities":{"calm":1}}},"usage":{"input_tokens":1,"output_tokens":1}}""",
        "answers.billing.confidence")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"choice","choice":"calm","confidence":1,"probabilities":{"calm":[]}}},"usage":{"input_tokens":1,"output_tokens":1}}""",
        "answers.billing.probabilities.calm")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"score","score":{},"confidence":1,"legend":{"0":"low","1":"high"},"probabilities":{"0":0.5,"1":0.5}}},"usage":{"input_tokens":1,"output_tokens":1}}""",
        "answers.billing.score")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"score","score":0.5,"confidence":1,"legend":{"0":"low","1":"high"},"probabilities":{"0":true,"1":0}}},"usage":{"input_tokens":1,"output_tokens":1}}""",
        "answers.billing.probabilities.0")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"noul","noul":0.5}},"usage":{"input_tokens":"1","output_tokens":1}}""",
        "usage.input_tokens")]
    [InlineData(
        """{"model":"jev-test","answers":{"billing":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":false}}""",
        "usage.output_tokens")]
    public async Task WrongKindNumericFieldsThrowResponseValidationException(
        string invalidJson,
        string expectedPath)
    {
        using var handler = new TestHttpMessageHandler((_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, invalidJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<TypeSafeResponseValidationException>(
            () => client.SystemOneAsync(BasicRequest()));

        Assert.Equal(expectedPath, exception.FieldPath);
    }

    [Fact]
    public async Task MissingRequestedAnswerThrowsResponseValidationException()
    {
        const string incompleteJson = """
            {
              "model":"jev-test",
              "answers":{"other":{"type":"noul","noul":0.5}},
              "usage":{"input_tokens":1,"output_tokens":1}
            }
            """;
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, incompleteJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<TypeSafeResponseValidationException>(
            () => client.SystemOneAsync(BasicRequest()));

        Assert.Equal("answers.billing", exception.FieldPath);
    }

    [Fact]
    public async Task InvalidOversizedRetryAfterIsIgnored()
    {
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
        {
            var response = TestHttpMessageHandler.JsonResponse(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.TryAddWithoutValidation("retry-after-ms", "1e300");
            return Task.FromResult(response);
        });
        using var httpClient = CreateHttpClient(handler);
        using var client = CreateClient(httpClient, TypeSafeRetryOptions.None);

        var exception = await Assert.ThrowsAsync<TypeSafeRateLimitException>(
            () => client.ListModelsAsync());

        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task RequestHeadersOverrideDefaultsButCannotReplaceProtectedHeaders()
    {
        const string modelsJson = "{\"models\":[]}";
        using var handler = new TestHttpMessageHandler(static (_, _, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, modelsJson)));
        using var httpClient = CreateHttpClient(handler);
        using var client = new TypeSafeClient(httpClient, new TypeSafeClientOptions
        {
            ApiKey = "real-key",
            BaseUrl = new Uri("https://unit.test"),
            UserAgent = "example-app/1.0",
            DefaultHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer wrong-key",
                ["X-Correlation-ID"] = "default",
            },
        });

        await client.ListModelsAsync(new TypeSafeRequestOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["User-Agent"] = "malicious-agent",
                ["X-Correlation-ID"] = "per-call",
            },
        });

        var sent = Assert.Single(handler.Requests);
        Assert.Equal("Bearer real-key", sent.Header("Authorization"));
        Assert.Contains("example-app/1.0", sent.Header("User-Agent"), StringComparison.Ordinal);
        Assert.DoesNotContain("malicious-agent", sent.Header("User-Agent"), StringComparison.Ordinal);
        Assert.Equal("per-call", sent.Header("X-Correlation-ID"));
    }

    [Fact]
    public async Task EnvironmentVariablesSupplyUnsetClientOptions()
    {
        var previousKey = Environment.GetEnvironmentVariable(TypeSafeDefaults.ApiKeyEnvironmentVariable);
        var previousBaseUrl = Environment.GetEnvironmentVariable(TypeSafeDefaults.BaseUrlEnvironmentVariable);
        var previousModel = Environment.GetEnvironmentVariable(TypeSafeDefaults.DefaultModelEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(TypeSafeDefaults.ApiKeyEnvironmentVariable, " environment-key ");
            Environment.SetEnvironmentVariable(TypeSafeDefaults.BaseUrlEnvironmentVariable, " https://environment.test/root/ ");
            Environment.SetEnvironmentVariable(TypeSafeDefaults.DefaultModelEnvironmentVariable, " jev-environment ");
            using var handler = new TestHttpMessageHandler(static (_, _, _) =>
                Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SystemOneJson)));
            using var httpClient = CreateHttpClient(handler);
            using var client = new TypeSafeClient(httpClient, new TypeSafeClientOptions
            {
                Retry = TypeSafeRetryOptions.None,
            });

            await client.SystemOneAsync(BasicRequest());

            var sent = Assert.Single(handler.Requests);
            Assert.Equal("https://environment.test/root/v1/systemone", sent.Uri.AbsoluteUri);
            Assert.Equal("Bearer environment-key", sent.Header("Authorization"));
            using var body = JsonDocument.Parse(Assert.IsType<string>(sent.Body));
            Assert.Equal("jev-environment", body.RootElement.GetProperty("model").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(TypeSafeDefaults.ApiKeyEnvironmentVariable, previousKey);
            Environment.SetEnvironmentVariable(TypeSafeDefaults.BaseUrlEnvironmentVariable, previousBaseUrl);
            Environment.SetEnvironmentVariable(TypeSafeDefaults.DefaultModelEnvironmentVariable, previousModel);
        }
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };

    private static TypeSafeClient CreateClient(
        HttpClient httpClient,
        TypeSafeRetryOptions? retry = null) =>
        new(httpClient, new TypeSafeClientOptions
        {
            ApiKey = "test-key",
            BaseUrl = new Uri("https://unit.test"),
            Retry = retry ?? TypeSafeRetryOptions.None,
        });

    private static SystemOneRequest BasicRequest() =>
        new(
            "I was charged twice.",
            new Dictionary<string, TypeSafeQuestion>
            {
                ["billing"] = Question.Noul("Is this about billing?"),
            });
}
