using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>A question that selects one label from a caller-defined set.</summary>
public sealed class ChoiceQuestion : TypeSafeQuestion
{
    private readonly ReadOnlyDictionary<string, JsonNode?> _criteria;

    /// <summary>Initializes a choice question from labels with instructions omitted.</summary>
    /// <param name="labels">Between one and 255 unique labels.</param>
    public ChoiceQuestion(IEnumerable<string> labels)
        : this(null, labels)
    {
    }

    /// <summary>Initializes a choice question from labels with no descriptions.</summary>
    /// <param name="instructions">
    /// What the model should select, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="labels">Between one and 255 unique labels.</param>
    public ChoiceQuestion(JsonNode? instructions, IEnumerable<string> labels)
        : this(instructions, ToCriteria(labels))
    {
    }

    /// <summary>Initializes a choice question from criteria with instructions omitted.</summary>
    /// <param name="criteria">Labels mapped to descriptions, or null descriptions.</param>
    public ChoiceQuestion(IEnumerable<KeyValuePair<string, JsonNode?>> criteria)
        : this(null, criteria)
    {
    }

    /// <summary>Initializes a choice question from labels and JSON descriptions.</summary>
    /// <param name="instructions">
    /// What the model should select, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="criteria">Labels mapped to descriptions, or null descriptions.</param>
    public ChoiceQuestion(
        JsonNode? instructions,
        IEnumerable<KeyValuePair<string, JsonNode?>> criteria)
        : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var copy = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (label, description) in criteria)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Choice labels cannot be blank.", nameof(criteria));
            }

            if (copy.Count == 255)
            {
                throw new ArgumentException("A choice question accepts at most 255 labels.", nameof(criteria));
            }

            if (!copy.TryAdd(label, JsonContentGuard.CloneOrNull(description, nameof(criteria))))
            {
                throw new ArgumentException($"Choice label '{label}' appears more than once.", nameof(criteria));
            }
        }

        if (copy.Count == 0)
        {
            throw new ArgumentException("A choice question requires at least one label.", nameof(criteria));
        }

        _criteria = new ReadOnlyDictionary<string, JsonNode?>(copy);
    }

    /// <summary>Gets labels mapped to their optional descriptions.</summary>
    public IReadOnlyDictionary<string, JsonNode?> Criteria => _criteria;

    /// <inheritdoc />
    public override string Type => "choice";

    private static IEnumerable<KeyValuePair<string, JsonNode?>> ToCriteria(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return labels.Select(static label => new KeyValuePair<string, JsonNode?>(label, null));
    }
}
