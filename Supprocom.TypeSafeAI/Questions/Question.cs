using System.Text.Json.Nodes;

namespace Supprocom.TypeSafeAI;

/// <summary>Convenience factories for TypeSafe question types.</summary>
public static class Question
{
    /// <summary>Creates a yes/no question.</summary>
    /// <param name="instructions">
    /// The yes/no question or statement, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="whenTrue">Optional description of yes.</param>
    /// <param name="whenFalse">Optional description of no.</param>
    /// <returns>A noul question.</returns>
    public static NoulQuestion Noul(
        string? instructions = null,
        string? whenTrue = null,
        string? whenFalse = null)
    {
        var criteria = whenTrue is null && whenFalse is null
            ? null
            : new NoulCriteria(AsNode(whenTrue), AsNode(whenFalse));
        return new NoulQuestion(AsNode(instructions), criteria);
    }

    /// <summary>Creates a choice question from bare labels.</summary>
    /// <param name="instructions">
    /// What the model should select, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="labels">The allowed answer labels.</param>
    /// <returns>A choice question.</returns>
    public static ChoiceQuestion Choice(string? instructions, params string[] labels) =>
        new(AsNode(instructions), labels);

    /// <summary>Creates a choice question from labels and text descriptions.</summary>
    /// <param name="instructions">
    /// What the model should select, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="criteria">Labels mapped to optional descriptions.</param>
    /// <returns>A choice question.</returns>
    public static ChoiceQuestion Choice(
        string? instructions,
        IReadOnlyDictionary<string, string?> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return new ChoiceQuestion(
            AsNode(instructions),
            criteria.Select(static pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, AsNode(pair.Value))));
    }

    /// <summary>Creates a score question from ordered text levels.</summary>
    /// <param name="instructions">
    /// What the model should rate, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="levels">Ordered descriptions, from the zero level upward.</param>
    /// <returns>A score question.</returns>
    public static ScoreQuestion Score(string? instructions, params string[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        return new ScoreQuestion(
            AsNode(instructions),
            levels.Select(static level =>
                AsNode(level) ?? throw new ArgumentException("Score levels cannot be null.", nameof(levels))));
    }

    private static JsonValue? AsNode(string? value) => value is null ? null : JsonValue.Create(value);
}
