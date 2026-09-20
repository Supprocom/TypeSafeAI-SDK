namespace Supprocom.TypeSafeAI;

/// <summary>Token usage reported for one System One request.</summary>
public sealed record TypeSafeUsage
{
    internal TypeSafeUsage(long inputTokens, long outputTokens)
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the billable input-token count.</summary>
    public long InputTokens { get; }

    /// <summary>Gets the output-token count.</summary>
    public long OutputTokens { get; }

    /// <summary>Gets the combined input and output count.</summary>
    public long TotalTokens => checked(InputTokens + OutputTokens);
}
