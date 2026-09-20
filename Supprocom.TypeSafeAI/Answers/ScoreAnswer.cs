using System.Collections.ObjectModel;
using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>A probability-weighted score with its rubric and level distribution.</summary>
public sealed class ScoreAnswer : TypeSafeAnswer
{
    private readonly ReadOnlyDictionary<int, JsonElement> _legend;
    private readonly ReadOnlyDictionary<int, double> _probabilities;

    internal ScoreAnswer(
        string id,
        double score,
        double confidence,
        IDictionary<int, JsonElement> legend,
        IDictionary<int, double> probabilities,
        JsonElement rawJson)
        : base(id, "score", rawJson)
    {
        Score = score;
        Confidence = confidence;
        _legend = new ReadOnlyDictionary<int, JsonElement>(
            legend.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone()));
        _probabilities = new ReadOnlyDictionary<int, double>(new Dictionary<int, double>(probabilities));
    }

    /// <summary>Gets the expected position on the rubric. The value may be fractional.</summary>
    public double Score { get; }

    /// <summary>Gets the API-reported confidence, from zero to one.</summary>
    public double Confidence { get; }

    /// <summary>Gets rubric descriptions keyed by zero-based level.</summary>
    public IReadOnlyDictionary<int, JsonElement> Legend => _legend;

    /// <summary>Gets level probabilities keyed by zero-based level.</summary>
    public IReadOnlyDictionary<int, double> Probabilities => _probabilities;

    /// <summary>Attempts to get the probability of a rubric level.</summary>
    /// <param name="level">The zero-based level.</param>
    /// <param name="probability">The probability when present.</param>
    /// <returns>True when the level was present in the response.</returns>
    public bool TryGetProbability(int level, out double probability) =>
        _probabilities.TryGetValue(level, out probability);
}
