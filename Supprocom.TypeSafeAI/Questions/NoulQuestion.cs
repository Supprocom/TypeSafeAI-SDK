using System.Text.Json.Nodes;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>Optional descriptions of what yes and no mean for a noul question.</summary>
public sealed class NoulCriteria
{
    /// <summary>Initializes outcome descriptions.</summary>
    /// <param name="whenTrue">What a probability near one means.</param>
    /// <param name="whenFalse">What a probability near zero means.</param>
    public NoulCriteria(JsonNode? whenTrue = null, JsonNode? whenFalse = null)
    {
        WhenTrue = JsonContentGuard.CloneOrNull(whenTrue, nameof(whenTrue));
        WhenFalse = JsonContentGuard.CloneOrNull(whenFalse, nameof(whenFalse));
    }

    /// <summary>Gets the description of the yes outcome.</summary>
    public JsonNode? WhenTrue { get; }

    /// <summary>Gets the description of the no outcome.</summary>
    public JsonNode? WhenFalse { get; }
}

/// <summary>A yes/no question whose answer is the probability of yes.</summary>
public sealed class NoulQuestion : TypeSafeQuestion
{
    /// <summary>Initializes a noul question.</summary>
    /// <param name="instructions">
    /// The yes/no question or statement to evaluate, or <see langword="null"/> to omit instructions.
    /// </param>
    /// <param name="criteria">Optional descriptions of the two outcomes.</param>
    public NoulQuestion(JsonNode? instructions = null, NoulCriteria? criteria = null)
        : base(instructions)
    {
        Criteria = criteria;
    }

    /// <summary>Gets optional descriptions of the yes and no outcomes.</summary>
    public NoulCriteria? Criteria { get; }

    /// <inheritdoc />
    public override string Type => "noul";
}
