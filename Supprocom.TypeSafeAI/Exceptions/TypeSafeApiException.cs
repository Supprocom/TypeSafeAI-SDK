using System.Collections.ObjectModel;
using System.Net;

namespace Supprocom.TypeSafeAI;

/// <summary>An unsuccessful HTTP response from the TypeSafe API.</summary>
public class TypeSafeApiException : TypeSafeException
{
    private readonly ReadOnlyDictionary<string, IReadOnlyList<string>> _headers;

    internal TypeSafeApiException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody,
        IDictionary<string, string[]> headers,
        string? requestId,
        Uri endpoint)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        _headers = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            headers.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<string>)Array.AsReadOnly([.. pair.Value]),
                StringComparer.OrdinalIgnoreCase));
        RequestId = requestId;
        Endpoint = endpoint;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Gets the response body, or null when the response was empty.</summary>
    public string? ResponseBody { get; }

    /// <summary>Gets response headers without credential-bearing request data.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers => _headers;

    /// <summary>Gets the <c>x-typesafe-request-id</c> header when present.</summary>
    public string? RequestId { get; }

    /// <summary>Gets the request endpoint without query data.</summary>
    public Uri Endpoint { get; }
}

/// <summary>The API rejected an invalid request with HTTP 400.</summary>
public sealed class TypeSafeBadRequestException : TypeSafeApiException
{
    internal TypeSafeBadRequestException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}

/// <summary>The API rejected the credential with HTTP 401.</summary>
public sealed class TypeSafeAuthenticationException : TypeSafeApiException
{
    internal TypeSafeAuthenticationException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}

/// <summary>The credential lacks permission and the API returned HTTP 403.</summary>
public sealed class TypeSafePermissionException : TypeSafeApiException
{
    internal TypeSafePermissionException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}

/// <summary>The requested API resource was not found and the API returned HTTP 404.</summary>
public sealed class TypeSafeNotFoundException : TypeSafeApiException
{
    internal TypeSafeNotFoundException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}

/// <summary>The request failed server validation with HTTP 422.</summary>
public sealed class TypeSafeValidationException : TypeSafeApiException
{
    internal TypeSafeValidationException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}

/// <summary>The account exceeded a service rate limit and the API returned HTTP 429.</summary>
public sealed class TypeSafeRateLimitException : TypeSafeApiException
{
    internal TypeSafeRateLimitException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint, TimeSpan? retryAfter)
        : base(statusCode, message, body, headers, requestId, endpoint)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>Gets the server-requested delay when one was supplied.</summary>
    public TimeSpan? RetryAfter { get; }
}

/// <summary>The service failed to handle the request and returned HTTP 5xx.</summary>
public sealed class TypeSafeServerException : TypeSafeApiException
{
    internal TypeSafeServerException(HttpStatusCode statusCode, string message, string? body, IDictionary<string, string[]> headers, string? requestId, Uri endpoint)
        : base(statusCode, message, body, headers, requestId, endpoint) { }
}
