using System.Collections.ObjectModel;

namespace Supprocom.TypeSafeAI;

/// <summary>Controls retries for transient TypeSafe API failures.</summary>
public sealed class TypeSafeRetryOptions
{
    private static readonly ReadOnlyCollection<int> DefaultStatusCodes =
        Array.AsReadOnly([408, 429, .. Enumerable.Range(500, 100)]);

    private int _maxRetries = 2;
    private TimeSpan _initialDelay = TimeSpan.FromMilliseconds(500);
    private TimeSpan _maximumDelay = TimeSpan.FromSeconds(5);
    private double _jitterFactor = 0.25;
    private TimeSpan _maximumRetryAfter = TimeSpan.FromSeconds(60);
    private IReadOnlyCollection<int> _statusCodes = DefaultStatusCodes;

    /// <summary>Gets a new policy with the SDK defaults.</summary>
    public static TypeSafeRetryOptions Default => new();

    /// <summary>Gets a new policy that disables retries.</summary>
    public static TypeSafeRetryOptions None => new() { MaxRetries = 0 };

    /// <summary>Gets or initializes the number of retries after the first attempt. The default is 2.</summary>
    public int MaxRetries
    {
        get => _maxRetries;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxRetries = value;
        }
    }

    /// <summary>Gets or initializes the delay before the first retry. The default is 500 milliseconds.</summary>
    public TimeSpan InitialDelay
    {
        get => _initialDelay;
        init
        {
            ThrowIfNegative(value);
            _initialDelay = value;
        }
    }

    /// <summary>Gets or initializes the exponential-backoff ceiling. The default is 5 seconds.</summary>
    public TimeSpan MaximumDelay
    {
        get => _maximumDelay;
        init
        {
            ThrowIfNegative(value);
            _maximumDelay = value;
        }
    }

    /// <summary>
    /// Gets or initializes the fraction randomly subtracted from each backoff delay, from 0 to 1.
    /// The default is 0.25.
    /// </summary>
    public double JitterFactor
    {
        get => _jitterFactor;
        init
        {
            if (!double.IsFinite(value) || value is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Jitter must be between 0 and 1.");
            }

            _jitterFactor = value;
        }
    }

    /// <summary>Gets or initializes the HTTP status codes that are retried.</summary>
    public IReadOnlyCollection<int> StatusCodes
    {
        get => _statusCodes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(static status => status is < 100 or > 599))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Retry status codes must be between 100 and 599.");
            }

            _statusCodes = Array.AsReadOnly([.. value]);
        }
    }

    /// <summary>Gets or initializes whether server Retry-After headers are honored.</summary>
    public bool RespectRetryAfter { get; init; } = true;

    /// <summary>
    /// Gets or initializes the largest server-requested delay that will be honored. Longer values
    /// fall back to exponential backoff. The default is 60 seconds.
    /// </summary>
    public TimeSpan MaximumRetryAfter
    {
        get => _maximumRetryAfter;
        init
        {
            ThrowIfNegative(value);
            _maximumRetryAfter = value;
        }
    }

    /// <summary>Gets or initializes whether connection failures are retried.</summary>
    public bool RetryConnectionErrors { get; init; } = true;

    /// <summary>Gets or initializes whether per-attempt timeouts are retried.</summary>
    public bool RetryTimeouts { get; init; } = true;

    internal TypeSafeRetryOptions Copy() => new()
    {
        MaxRetries = MaxRetries,
        InitialDelay = InitialDelay,
        MaximumDelay = MaximumDelay,
        JitterFactor = JitterFactor,
        StatusCodes = StatusCodes,
        RespectRetryAfter = RespectRetryAfter,
        MaximumRetryAfter = MaximumRetryAfter,
        RetryConnectionErrors = RetryConnectionErrors,
        RetryTimeouts = RetryTimeouts,
    };

    private static void ThrowIfNegative(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"The duration must be between zero and {TimeSpan.FromMilliseconds(uint.MaxValue - 1)}.");
        }
    }
}
