namespace Supprocom.TypeSafeAI;

/// <summary>Base class for failures produced by the SDK.</summary>
public class TypeSafeException : Exception
{
    internal TypeSafeException(string message)
        : base(message)
    {
    }

    internal TypeSafeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A required client setting is missing or invalid.</summary>
public sealed class TypeSafeConfigurationException : TypeSafeException
{
    internal TypeSafeConfigurationException(string message)
        : base(message)
    {
    }
}

/// <summary>The request contains a value that cannot be encoded as JSON.</summary>
public sealed class TypeSafeRequestSerializationException : TypeSafeException
{
    internal TypeSafeRequestSerializationException(Exception innerException)
        : base("The TypeSafe request could not be encoded as JSON.", innerException)
    {
    }
}

/// <summary>The request failed before a complete HTTP response was received.</summary>
public class TypeSafeConnectionException : TypeSafeException
{
    internal TypeSafeConnectionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>One HTTP attempt exceeded its configured timeout.</summary>
public sealed class TypeSafeTimeoutException : TypeSafeConnectionException
{
    internal TypeSafeTimeoutException(TimeSpan timeout, Exception? innerException)
        : base($"The TypeSafe request timed out after {timeout}.", innerException)
    {
        Timeout = timeout;
    }

    /// <summary>Gets the timeout applied to the failed attempt.</summary>
    public TimeSpan Timeout { get; }
}

/// <summary>A successful response did not match the documented JSON contract.</summary>
public sealed class TypeSafeResponseValidationException : TypeSafeException
{
    internal TypeSafeResponseValidationException(
        string fieldPath,
        string responseBody,
        string? requestId,
        Exception? innerException = null)
        : base($"The TypeSafe response is invalid at '{fieldPath}'.", innerException)
    {
        FieldPath = fieldPath;
        ResponseBody = responseBody;
        RequestId = requestId;
    }

    /// <summary>Gets the path of the first missing or malformed field.</summary>
    public string FieldPath { get; }

    /// <summary>Gets the raw response body.</summary>
    public string ResponseBody { get; }

    /// <summary>Gets the service request ID when present.</summary>
    public string? RequestId { get; }
}
