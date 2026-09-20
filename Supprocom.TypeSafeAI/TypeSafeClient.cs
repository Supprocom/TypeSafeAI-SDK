using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>An asynchronous, thread-safe client for the TypeSafe AI API.</summary>
public sealed partial class TypeSafeClient : ITypeSafeClient, IDisposable
{
    private const string SystemOnePath = "v1/systemone";
    private const string ModelsPath = "v1/models";
    private const string RetryCountHeader = "X-TypeSafe-Retry-Count";

    private static readonly HashSet<string> ProtectedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Accept",
        "Content-Type",
        "User-Agent",
        "X-TypeSafe-SDK",
        "X-TypeSafe-Runtime",
        RetryCountHeader,
    };

    private static readonly string SdkVersion = ResolveSdkVersion();
    private static readonly string SdkToken = $"supprocom-typesafe-dotnet/{SdkVersion}";
    private static readonly string RuntimeToken =
        $"dotnet/{Environment.Version} ({RuntimeInformation.RuntimeIdentifier})";

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _apiKey;
    private readonly TypeSafeRetryOptions _retry;
    private readonly IReadOnlyDictionary<string, string> _defaultHeaders;
    private readonly bool _disposeHttpClient;
    private long _requestCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a client from <c>TYPESAFE_API_KEY</c> and the optional TypeSafe environment
    /// variables.
    /// </summary>
    public TypeSafeClient()
        : this(new TypeSafeClientOptions())
    {
    }

    /// <summary>Initializes a client with an API key and default settings.</summary>
    /// <param name="apiKey">The secret TypeSafe API key.</param>
    public TypeSafeClient(string apiKey)
        : this(new TypeSafeClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Initializes a client with SDK-managed HTTP resources.</summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="logger">An optional Microsoft.Extensions.Logging logger.</param>
    public TypeSafeClient(TypeSafeClientOptions options, ILogger<TypeSafeClient>? logger = null)
        : this(CreateHttpClient(), options, logger, disposeHttpClient: true)
    {
    }

    /// <summary>Initializes a client over a caller-owned <see cref="HttpClient"/>.</summary>
    /// <param name="httpClient">
    /// The HTTP client to use. Its lifetime and existing timeout remain controlled by the caller.
    /// </param>
    /// <param name="options">Client configuration.</param>
    /// <param name="logger">An optional Microsoft.Extensions.Logging logger.</param>
    public TypeSafeClient(
        HttpClient httpClient,
        TypeSafeClientOptions options,
        ILogger<TypeSafeClient>? logger = null)
        : this(httpClient, options, logger, disposeHttpClient: false)
    {
    }

    internal TypeSafeClient(
        HttpClient httpClient,
        TypeSafeClientOptions options,
        ILogger<TypeSafeClient>? logger,
        bool disposeHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _logger = logger ?? NullLogger<TypeSafeClient>.Instance;
        _disposeHttpClient = disposeHttpClient;
        _apiKey = ResolveApiKey(options.ApiKey);
        BaseUrl = ResolveBaseUrl(options.BaseUrl);
        DefaultModel = ResolveDefaultModel(options.DefaultModel);
        Timeout = ValidateTimeout(options.Timeout, nameof(options.Timeout));
        _retry = (options.Retry ?? throw new TypeSafeConfigurationException("Retry options cannot be null.")).Copy();
        _defaultHeaders = CopyHeaders(options.DefaultHeaders, "default headers");
        UserAgent = ValidateUserAgent(options.UserAgent);
    }

    /// <summary>Gets the normalized API root.</summary>
    public Uri BaseUrl { get; }

    /// <summary>Gets the model used when a request does not specify one.</summary>
    public string DefaultModel { get; }

    /// <summary>Gets the timeout applied independently to each HTTP attempt.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Gets the client-level retry policy.</summary>
    public TypeSafeRetryOptions Retry => _retry;

    /// <summary>Gets the optional caller product token appended to the SDK User-Agent.</summary>
    public string? UserAgent { get; }

    /// <inheritdoc />
    public async Task<SystemOneResponse> SystemOneAsync(
        SystemOneRequest request,
        TypeSafeRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var model = request.Model ?? DefaultModel;
        byte[] body;
        try
        {
            body = TypeSafeRequestSerializer.Serialize(request, model);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or JsonException or NotSupportedException)
        {
            throw new TypeSafeRequestSerializationException(exception);
        }

        var response = await SendAsync(
            HttpMethod.Post,
            SystemOnePath,
            body,
            options,
            cancellationToken).ConfigureAwait(false);

        var result = TypeSafeResponseParser.ParseSystemOne(response.Body, response.RequestId);
        ValidateRequestedAnswers(request, result, response.Body);
        return result;
    }

    /// <summary>Answers named questions about text state without explicitly creating a request.</summary>
    /// <param name="state">The text all questions refer to.</param>
    /// <param name="questions">Questions keyed by caller-chosen IDs.</param>
    /// <param name="model">An optional model override.</param>
    /// <param name="options">Optional settings for this call.</param>
    /// <param name="cancellationToken">Cancels the active attempt or a pending retry.</param>
    /// <returns>The model's typed answers and token usage.</returns>
    public Task<SystemOneResponse> SystemOneAsync(
        string state,
        IReadOnlyDictionary<string, TypeSafeQuestion> questions,
        string? model = null,
        TypeSafeRequestOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SystemOneAsync(new SystemOneRequest(state, questions, model), options, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TypeSafeModel>> ListModelsAsync(
        TypeSafeRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var response = await SendAsync(
            HttpMethod.Get,
            ModelsPath,
            body: null,
            options,
            cancellationToken).ConfigureAwait(false);

        return TypeSafeResponseParser.ParseModels(response.Body, response.RequestId);
    }

    /// <summary>Releases SDK-owned HTTP resources.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<ResponsePayload> SendAsync(
        HttpMethod method,
        string path,
        byte[]? body,
        TypeSafeRequestOptions? options,
        CancellationToken cancellationToken)
    {
        var timeout = ValidateTimeout(options?.Timeout ?? Timeout, "request timeout");
        var retry = (options?.Retry ?? _retry).Copy();
        var headers = MergeHeaders(_defaultHeaders, options?.Headers);
        var endpoint = new Uri(BaseUrl, path);
        var requestNumber = Interlocked.Increment(ref _requestCount);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var started = Stopwatch.GetTimestamp();
            LogSending(_logger, requestNumber, method.Method, endpoint, attempt + 1);

            TimeSpan retryDelay;
            string retryReason;
            try
            {
                using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCancellation.CancelAfter(timeout);
                using var request = CreateRequest(method, endpoint, body, headers, attempt);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    attemptCancellation.Token).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(attemptCancellation.Token)
                    .ConfigureAwait(false);
                var requestId = TypeSafeErrorFactory.RequestId(response);
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                LogReceived(
                    _logger,
                    requestNumber,
                    (int)response.StatusCode,
                    elapsedMilliseconds,
                    requestId ?? "-");

                if (response.IsSuccessStatusCode)
                {
                    return new ResponsePayload(responseBody, requestId);
                }

                var exception = TypeSafeErrorFactory.Create(response, responseBody, endpoint);
                if (attempt >= retry.MaxRetries || !retry.StatusCodes.Contains((int)response.StatusCode))
                {
                    throw exception;
                }

                retryDelay = GetRetryDelay(retry, attempt, response);
                retryReason = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                var timeoutException = new TypeSafeTimeoutException(timeout, exception);
                if (attempt >= retry.MaxRetries || !retry.RetryTimeouts)
                {
                    throw timeoutException;
                }

                retryDelay = GetRetryDelay(retry, attempt, response: null);
                retryReason = "timeout";
            }
            catch (HttpRequestException exception)
            {
                var connectionException = new TypeSafeConnectionException(
                    $"Connection error: {exception.Message}",
                    exception);
                if (attempt >= retry.MaxRetries || !retry.RetryConnectionErrors)
                {
                    throw connectionException;
                }

                retryDelay = GetRetryDelay(retry, attempt, response: null);
                retryReason = "connection error";
            }
            catch (IOException exception)
            {
                var connectionException = new TypeSafeConnectionException(
                    $"Connection error: {exception.Message}",
                    exception);
                if (attempt >= retry.MaxRetries || !retry.RetryConnectionErrors)
                {
                    throw connectionException;
                }

                retryDelay = GetRetryDelay(retry, attempt, response: null);
                retryReason = "connection error";
            }

            LogRetrying(
                _logger,
                requestNumber,
                retryDelay.TotalMilliseconds,
                attempt + 1,
                retry.MaxRetries,
                retryReason);
            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri endpoint,
        byte[]? body,
        IReadOnlyDictionary<string, string> headers,
        int attempt)
    {
        var request = new HttpRequestMessage(method, endpoint);
        try
        {
            foreach (var (name, value) in headers)
            {
                if (ProtectedHeaders.Contains(name))
                {
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    throw new TypeSafeConfigurationException($"'{name}' is not a valid request header.");
                }
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent is null ? SdkToken : $"{SdkToken} {UserAgent}");
            request.Headers.TryAddWithoutValidation("X-TypeSafe-SDK", SdkToken);
            request.Headers.TryAddWithoutValidation("X-TypeSafe-Runtime", RuntimeToken);
            if (attempt > 0)
            {
                request.Headers.TryAddWithoutValidation(
                    RetryCountHeader,
                    attempt.ToString(CultureInfo.InvariantCulture));
            }

            if (body is not null)
            {
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    private static TimeSpan GetRetryDelay(
        TypeSafeRetryOptions retry,
        int zeroBasedRetry,
        HttpResponseMessage? response)
    {
        if (retry.RespectRetryAfter && response is not null)
        {
            var serverDelay = TypeSafeErrorFactory.ParseRetryAfter(response, DateTimeOffset.UtcNow);
            if (serverDelay is not null && serverDelay <= retry.MaximumRetryAfter)
            {
                return serverDelay.Value;
            }
        }

        var exponentialMilliseconds = zeroBasedRetry >= 63
            ? retry.MaximumDelay.TotalMilliseconds
            : retry.InitialDelay.TotalMilliseconds * Math.Pow(2, zeroBasedRetry);
        var cappedMilliseconds = Math.Min(exponentialMilliseconds, retry.MaximumDelay.TotalMilliseconds);
        var jitteredMilliseconds = cappedMilliseconds * (1 - (Random.Shared.NextDouble() * retry.JitterFactor));
        return TimeSpan.FromMilliseconds(Math.Round(jitteredMilliseconds));
    }

    private static void ValidateRequestedAnswers(
        SystemOneRequest request,
        SystemOneResponse response,
        string responseBody)
    {
        foreach (var (id, question) in request.Questions)
        {
            if (!response.Answers.TryGetValue(id, out var answer))
            {
                throw new TypeSafeResponseValidationException(
                    $"answers.{id}",
                    responseBody,
                    response.RequestId);
            }

            if (!string.Equals(answer.Type, question.Type, StringComparison.Ordinal))
            {
                throw new TypeSafeResponseValidationException(
                    $"answers.{id}.type",
                    responseBody,
                    response.RequestId);
            }
        }
    }

    private static Dictionary<string, string> MergeHeaders(
        IReadOnlyDictionary<string, string> defaults,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var result = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        if (overrides is null)
        {
            return result;
        }

        foreach (var (name, value) in CopyHeaders(overrides, "request headers"))
        {
            result[name] = value;
        }

        return result;
    }

    private static Dictionary<string, string> CopyHeaders(
        IReadOnlyDictionary<string, string>? headers,
        string source)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return copy;
        }

        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new TypeSafeConfigurationException($"A {source} name cannot be blank.");
            }

            if (value is null || value.Contains('\r') || value.Contains('\n'))
            {
                throw new TypeSafeConfigurationException($"Header '{name}' has an invalid value.");
            }

            copy[name] = value;
        }

        return copy;
    }

    private static string ResolveApiKey(string? configured)
    {
        var value = configured ?? ReadEnvironment(TypeSafeDefaults.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TypeSafeConfigurationException(
                $"No API key was provided. Set TypeSafeClientOptions.ApiKey or {TypeSafeDefaults.ApiKeyEnvironmentVariable}.");
        }

        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new TypeSafeConfigurationException("The API key contains invalid characters.");
        }

        value = value.Trim();
        if (!AuthenticationHeaderValue.TryParse($"Bearer {value}", out _))
        {
            throw new TypeSafeConfigurationException("The API key cannot be represented in an Authorization header.");
        }

        return value;
    }

    private static Uri ResolveBaseUrl(Uri? configured)
    {
        Uri value;
        var environmentValue = ReadEnvironment(TypeSafeDefaults.BaseUrlEnvironmentVariable);
        if (configured is not null)
        {
            value = configured;
        }
        else if (environmentValue is not null &&
                 Uri.TryCreate(environmentValue, UriKind.Absolute, out var environmentUri))
        {
            value = environmentUri;
        }
        else if (environmentValue is not null)
        {
            throw new TypeSafeConfigurationException(
                $"{TypeSafeDefaults.BaseUrlEnvironmentVariable} must be an absolute HTTP or HTTPS URL.");
        }
        else
        {
            value = new Uri(TypeSafeDefaults.BaseUrl, UriKind.Absolute);
        }

        if (!value.IsAbsoluteUri ||
            (value.Scheme != Uri.UriSchemeHttps && value.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(value.UserInfo) ||
            !string.IsNullOrEmpty(value.Query) ||
            !string.IsNullOrEmpty(value.Fragment))
        {
            throw new TypeSafeConfigurationException(
                "TypeSafeClientOptions.BaseUrl must be an absolute HTTP or HTTPS URL without user information, a query, or a fragment.");
        }

        var builder = new UriBuilder(value)
        {
            Path = string.Concat(value.AbsolutePath.TrimEnd('/'), "/"),
        };
        return builder.Uri;
    }

    private static string ResolveDefaultModel(string? configured)
    {
        var value = configured ??
            ReadEnvironment(TypeSafeDefaults.DefaultModelEnvironmentVariable) ??
            TypeSafeDefaults.Model;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new TypeSafeConfigurationException("The default model cannot be blank.");
        }

        return value.Trim();
    }

    private static TimeSpan ValidateTimeout(TimeSpan timeout, string setting)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new TypeSafeConfigurationException(
                $"The {setting} must be positive and no longer than {TimeSpan.FromMilliseconds(uint.MaxValue - 1)}.");
        }

        return timeout;
    }

    private static string? ValidateUserAgent(string? userAgent)
    {
        if (userAgent is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(userAgent) ||
            userAgent.Contains('\r') ||
            userAgent.Contains('\n'))
        {
            throw new TypeSafeConfigurationException("The UserAgent product token is invalid.");
        }

        return userAgent.Trim();
    }

    private static string? ReadEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value ? value : null;

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
    }

    private static string ResolveSdkVersion()
    {
        var version = typeof(TypeSafeClient).Assembly.GetName().Version;
        return version is null ? "0.0.0" : version.ToString(3);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "TypeSafe request {RequestNumber}: sending {Method} {Endpoint} (attempt {Attempt})")]
    private static partial void LogSending(
        ILogger logger,
        long requestNumber,
        string method,
        Uri endpoint,
        int attempt);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "TypeSafe request {RequestNumber}: received HTTP {StatusCode} in {ElapsedMilliseconds:F0} ms (request ID {RequestId})")]
    private static partial void LogReceived(
        ILogger logger,
        long requestNumber,
        int statusCode,
        double elapsedMilliseconds,
        string requestId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "TypeSafe request {RequestNumber}: retrying in {DelayMilliseconds:F0} ms (retry {Retry}/{MaximumRetries}) after {Reason}")]
    private static partial void LogRetrying(
        ILogger logger,
        long requestNumber,
        double delayMilliseconds,
        int retry,
        int maximumRetries,
        string reason);

    private sealed record ResponsePayload(string Body, string? RequestId);
}
