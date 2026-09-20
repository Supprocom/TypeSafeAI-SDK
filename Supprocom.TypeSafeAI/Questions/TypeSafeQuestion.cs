using System.Text.Json.Nodes;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>Base type for a typed System One question.</summary>
public abstract class TypeSafeQuestion
{
    /// <summary>Initializes a question with optional text or structured JSON instructions.</summary>
    /// <param name="instructions">
    /// The judgment the model should make, or <see langword="null"/> to omit instructions.
    /// </param>
    private protected TypeSafeQuestion(JsonNode? instructions)
    {
        Instructions = JsonContentGuard.CloneOrNull(instructions, nameof(instructions));
    }

    /// <summary>
    /// Gets the instructions sent to the model, or <see langword="null"/> when the wire property is omitted.
    /// </summary>
    public JsonNode? Instructions { get; }

    /// <summary>Gets the API discriminator for this question.</summary>
    public abstract string Type { get; }
}
