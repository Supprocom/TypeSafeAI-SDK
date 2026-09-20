using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>The probability that a noul question's answer is yes.</summary>
public sealed class NoulAnswer : TypeSafeAnswer
{
    internal NoulAnswer(string id, double probability, JsonElement rawJson)
        : base(id, "noul", rawJson)
    {
        Probability = probability;
    }

    /// <summary>Gets the probability of yes, from zero to one.</summary>
    public double Probability { get; }

    /// <summary>Gets the API's <c>noul</c> value, equivalent to <see cref="Probability"/>.</summary>
    public double Noul => Probability;
}
