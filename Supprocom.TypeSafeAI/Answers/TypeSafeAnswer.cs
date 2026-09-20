using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>Base type for an answer returned under a question ID.</summary>
public abstract class TypeSafeAnswer
{
    internal TypeSafeAnswer(string id, string type, JsonElement rawJson)
    {
        Id = id;
        Type = type;
        RawJson = rawJson.Clone();
    }

    /// <summary>Gets the caller-chosen question ID.</summary>
    public string Id { get; }

    /// <summary>Gets the answer discriminator returned by the API.</summary>
    public string Type { get; }

    /// <summary>Gets the complete answer object for forward-compatible access.</summary>
    public JsonElement RawJson { get; }
}
