using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Supprocom.TypeSafeAI.Internal;

internal static class TypeSafeErrorFactory
{
    private const string RequestIdHeader = "x-typesafe-request-id";

    public static TypeSafeApiException Create(HttpResponseMessage response, string? body, Uri endpoint)
    {
        body = string.IsNullOrEmpty(body) ? null : body;
        var headers = ReadHeaders(response);
        var requestId = ReadHeader(response, RequestIdHeader);
        var message = $"{(int)response.StatusCode} {ExtractMessage(body) ?? response.ReasonPhrase ?? "API request failed"}";

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new TypeSafeBadRequestException(response.StatusCode, message, body, headers, requestId, endpoint),
            HttpStatusCode.Unauthorized => new TypeSafeAuthenticationException(response.StatusCode, message, body, headers, requestId, endpoint),
            HttpStatusCode.Forbidden => new TypeSafePermissionException(response.StatusCode, message, body, headers, requestId, endpoint),
            HttpStatusCode.NotFound => new TypeSafeNotFoundException(response.StatusCode, message, body, headers, requestId, endpoint),
            HttpStatusCode.UnprocessableEntity => new TypeSafeValidationException(response.StatusCode, message, body, headers, requestId, endpoint),
            HttpStatusCode.TooManyRequests => new TypeSafeRateLimitException(response.StatusCode, message, body, headers, requestId, endpoint, ParseRetryAfter(response, DateTimeOffset.UtcNow)),
            >= HttpStatusCode.InternalServerError => new TypeSafeServerException(response.StatusCode, message, body, headers, requestId, endpoint),
            _ => new TypeSafeApiException(response.StatusCode, message, body, headers, requestId, endpoint),
        };
    }

    public static string? RequestId(HttpResponseMessage response) => ReadHeader(response, RequestIdHeader);

    public static TimeSpan? ParseRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        if (ReadHeader(response, "retry-after-ms") is { } milliseconds &&
            double.TryParse(milliseconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            double.IsFinite(value) &&
            value is >= 0 and <= uint.MaxValue - 1)
        {
            return TimeSpan.FromMilliseconds(value);
        }

        if (response.Headers.RetryAfter?.Delta is { } delta && delta >= TimeSpan.Zero)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            return date <= now ? TimeSpan.Zero : date - now;
        }

        return null;
    }

    private static string? ExtractMessage(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Truncate(body);
            }

            if (TryMessage(root, "error", out var error) ||
                TryMessage(root, "message", out error) ||
                TryMessage(root, "detail", out error))
            {
                return error;
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Array)
            {
                var message = new StringBuilder();
                foreach (var item in detail.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("msg", out var msg) ||
                        msg.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    if (message.Length > 0)
                    {
                        message.Append("; ");
                    }

                    if (item.TryGetProperty("loc", out var location) && location.ValueKind == JsonValueKind.Array)
                    {
                        var parts = location.EnumerateArray()
                            .Select(static part => part.ToString())
                            .Where(static part => !string.Equals(part, "body", StringComparison.Ordinal));
                        var path = string.Join('.', parts);
                        if (path.Length > 0)
                        {
                            message.Append(path).Append(": ");
                        }
                    }

                    message.Append(msg.GetString());
                }

                return message.Length == 0 ? null : message.ToString();
            }
        }
        catch (JsonException)
        {
            return Truncate(body);
        }

        return Truncate(body);
    }

    private static string Truncate(string value) =>
        value.Length <= 200 ? value : string.Concat(value.AsSpan(0, 200), "…");

    private static bool TryMessage(JsonElement root, string name, out string? message)
    {
        message = null;
        if (!root.TryGetProperty(name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            message = value.GetString();
            return !string.IsNullOrEmpty(message);
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("message", out var nested) &&
            nested.ValueKind == JsonValueKind.String)
        {
            message = nested.GetString();
            return !string.IsNullOrEmpty(message);
        }

        return false;
    }

    private static Dictionary<string, string[]> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }

        return headers;
    }

    private static string? ReadHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values) ||
            response.Content.Headers.TryGetValues(name, out values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }
}
