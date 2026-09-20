namespace Supprocom.TypeSafeAI;

/// <summary>Overrides client settings for one API call.</summary>
public sealed class TypeSafeRequestOptions
{
    /// <summary>Gets or sets a per-attempt timeout override.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Gets or sets a retry-policy override.</summary>
    public TypeSafeRetryOptions? Retry { get; set; }

    /// <summary>Gets or sets request headers merged over the client defaults.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
