namespace Supprocom.TypeSafeAI;

/// <summary>Names and values shared with TypeSafe's official client libraries.</summary>
public static class TypeSafeDefaults
{
    /// <summary>The environment variable that contains the API key.</summary>
    public const string ApiKeyEnvironmentVariable = "TYPESAFE_API_KEY";

    /// <summary>The environment variable that overrides the API root.</summary>
    public const string BaseUrlEnvironmentVariable = "TYPESAFE_BASE_URL";

    /// <summary>The environment variable that overrides the default model.</summary>
    public const string DefaultModelEnvironmentVariable = "TYPESAFE_DEFAULT_MODEL";

    /// <summary>The default TypeSafe API root.</summary>
    public const string BaseUrl = "https://api.typesafe.ai";

    /// <summary>The default System One model alias.</summary>
    public const string Model = "jev-latest";

    /// <summary>The default timeout for one HTTP attempt.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
}
