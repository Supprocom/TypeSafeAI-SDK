namespace Supprocom.TypeSafeAI;

/// <summary>Configures a <see cref="TypeSafeClient"/>.</summary>
public sealed class TypeSafeClientOptions
{
    /// <summary>
    /// Gets or sets the API key. When unset, <c>TYPESAFE_API_KEY</c> is read from the environment.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API root. When unset, <c>TYPESAFE_BASE_URL</c> or the official API root is used.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the model used by requests that do not name one. Defaults to <c>jev-latest</c>.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>Gets or sets the timeout for each HTTP attempt. Defaults to 10 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TypeSafeDefaults.Timeout;

    /// <summary>Gets or sets the retry policy.</summary>
    public TypeSafeRetryOptions Retry { get; set; } = TypeSafeRetryOptions.Default;

    /// <summary>Gets or sets headers included with every request.</summary>
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; set; }

    /// <summary>Gets or sets an optional product token appended to the User-Agent header.</summary>
    public string? UserAgent { get; set; }
}
