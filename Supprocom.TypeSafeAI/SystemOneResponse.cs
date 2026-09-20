using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Supprocom.TypeSafeAI;

/// <summary>Named typed answers, model metadata, and usage for a System One request.</summary>
public sealed class SystemOneResponse
{
    private readonly ReadOnlyDictionary<string, TypeSafeAnswer> _answers;

    internal SystemOneResponse(
        string model,
        IDictionary<string, TypeSafeAnswer> answers,
        TypeSafeUsage usage,
        string? requestId,
        JsonElement rawJson)
    {
        Model = model;
        _answers = new ReadOnlyDictionary<string, TypeSafeAnswer>(
            new Dictionary<string, TypeSafeAnswer>(answers, StringComparer.Ordinal));
        Usage = usage;
        RequestId = requestId;
        RawJson = rawJson.Clone();
    }

    /// <summary>Gets the concrete model that answered the request.</summary>
    public string Model { get; }

    /// <summary>Gets answers keyed by question ID.</summary>
    public IReadOnlyDictionary<string, TypeSafeAnswer> Answers => _answers;

    /// <summary>Gets token usage reported by the service.</summary>
    public TypeSafeUsage Usage { get; }

    /// <summary>Gets the <c>x-typesafe-request-id</c> response header when present.</summary>
    public string? RequestId { get; }

    /// <summary>Gets the complete JSON response for forward-compatible access.</summary>
    public JsonElement RawJson { get; }

    /// <summary>Gets an answer and verifies its expected runtime type.</summary>
    /// <typeparam name="TAnswer">The expected answer type.</typeparam>
    /// <param name="id">The question ID.</param>
    /// <returns>The typed answer.</returns>
    public TAnswer Get<TAnswer>(string id)
        where TAnswer : TypeSafeAnswer
    {
        if (!_answers.TryGetValue(id, out var answer))
        {
            throw new KeyNotFoundException($"No answer was returned for question '{id}'.");
        }

        return answer as TAnswer
            ?? throw new InvalidOperationException(
                $"Question '{id}' returned answer type '{answer.Type}', not '{typeof(TAnswer).Name}'.");
    }

    /// <summary>Attempts to get a typed answer.</summary>
    /// <typeparam name="TAnswer">The expected answer type.</typeparam>
    /// <param name="id">The question ID.</param>
    /// <param name="answer">The typed answer when present and compatible.</param>
    /// <returns>True when a matching answer was found.</returns>
    public bool TryGet<TAnswer>(string id, [NotNullWhen(true)] out TAnswer? answer)
        where TAnswer : TypeSafeAnswer
    {
        if (_answers.TryGetValue(id, out var found) && found is TAnswer typed)
        {
            answer = typed;
            return true;
        }

        answer = null;
        return false;
    }

    /// <summary>Gets a noul answer by question ID.</summary>
    /// <param name="id">The question ID.</param>
    /// <returns>The noul answer.</returns>
    public NoulAnswer Noul(string id) => Get<NoulAnswer>(id);

    /// <summary>Gets a choice answer by question ID.</summary>
    /// <param name="id">The question ID.</param>
    /// <returns>The choice answer.</returns>
    public ChoiceAnswer Choice(string id) => Get<ChoiceAnswer>(id);

    /// <summary>Gets a score answer by question ID.</summary>
    /// <param name="id">The question ID.</param>
    /// <returns>The score answer.</returns>
    public ScoreAnswer Score(string id) => Get<ScoreAnswer>(id);
}
