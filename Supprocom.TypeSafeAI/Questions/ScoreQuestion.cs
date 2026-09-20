using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>A question that assigns a position on an ordered, descriptive scale.</summary>
public sealed class ScoreQuestion : TypeSafeQuestion
{
    private readonly ReadOnlyCollection<JsonNode> _criteria;

    /// <summary>Initializes a score question with instructions omitted.</summary>
    /// <param name="criteria">Between two and ten ordered level descriptions.</param>
    public ScoreQuestion(IEnumerable<JsonNode> criteria)
        : this(null, criteria)
    {
    }

    /// <summary>Initializes a score question.</summary>
    /// <param name="instructions">
    /// What the model should rate, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="criteria">Between two and ten ordered level descriptions.</param>
    public ScoreQuestion(JsonNode? instructions, IEnumerable<JsonNode> criteria)
        : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var copy = new List<JsonNode>(10);
        foreach (var level in criteria)
        {
            ArgumentNullException.ThrowIfNull(level);
            if (copy.Count == 10)
            {
                throw new ArgumentException("A score question accepts at most 10 levels.", nameof(criteria));
            }

            copy.Add(JsonContentGuard.Clone(level, nameof(criteria)));
        }

        if (copy.Count < 2)
        {
            throw new ArgumentException("A score question requires at least two levels.", nameof(criteria));
        }

        _criteria = copy.AsReadOnly();
    }

    /// <summary>Gets the ordered level descriptions, numbered from zero.</summary>
    public IReadOnlyList<JsonNode> Criteria => _criteria;

    /// <inheritdoc />
    public override string Type => "score";
}
