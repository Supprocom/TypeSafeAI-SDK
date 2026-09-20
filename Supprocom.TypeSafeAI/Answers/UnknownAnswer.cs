using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>An answer kind introduced after this SDK version.</summary>
public sealed class UnknownAnswer : TypeSafeAnswer
{
    internal UnknownAnswer(string id, string type, JsonElement rawJson)
        : base(id, type, rawJson)
    {
    }
}
