using System.Collections.ObjectModel;
using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>A selected label with confidence and a full label distribution.</summary>
public sealed class ChoiceAnswer : TypeSafeAnswer
{
    private readonly ReadOnlyDictionary<string, double> _probabilities;

    internal ChoiceAnswer(
        string id,
        string choice,
        double confidence,
        IDictionary<string, double> probabilities,
        JsonElement rawJson)
        : base(id, "choice", rawJson)
    {
        Choice = choice;
        Confidence = confidence;
        _probabilities = new ReadOnlyDictionary<string, double>(
            new Dictionary<string, double>(probabilities, StringComparer.Ordinal));
    }

    /// <summary>Gets the label with the highest reported probability.</summary>
    public string Choice { get; }

    /// <summary>Gets the API-reported confidence, from zero to one.</summary>
    public double Confidence { get; }

    /// <summary>Gets the probability of every label returned by the API.</summary>
    public IReadOnlyDictionary<string, double> Probabilities => _probabilities;

    /// <summary>Attempts to get the probability of a label.</summary>
    /// <param name="label">The label to find.</param>
    /// <param name="probability">The probability when present.</param>
    /// <returns>True when the label was present in the response.</returns>
    public bool TryGetProbability(string label, out double probability) =>
        _probabilities.TryGetValue(label, out probability);
}
