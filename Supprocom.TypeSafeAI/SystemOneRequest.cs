using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Supprocom.TypeSafeAI.Internal;

namespace Supprocom.TypeSafeAI;

/// <summary>State and named questions for one System One evaluation.</summary>
public sealed class SystemOneRequest
{
    private readonly ReadOnlyDictionary<string, TypeSafeQuestion> _questions;

    /// <summary>Initializes a request with text state.</summary>
    /// <param name="state">The text every question refers to.</param>
    /// <param name="questions">Questions keyed by caller-chosen IDs.</param>
    /// <param name="model">An optional model override.</param>
    public SystemOneRequest(
        string state,
        IReadOnlyDictionary<string, TypeSafeQuestion> questions,
        string? model = null)
        : this(JsonValue.Create(state ?? throw new ArgumentNullException(nameof(state)))!, questions, model)
    {
    }

    /// <summary>Initializes a request with string, object, or array JSON state.</summary>
    /// <param name="state">The JSON state every question refers to.</param>
    /// <param name="questions">Questions keyed by caller-chosen IDs.</param>
    /// <param name="model">An optional model override.</param>
    public SystemOneRequest(
        JsonNode state,
        IReadOnlyDictionary<string, TypeSafeQuestion> questions,
        string? model = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);

        if (questions.Count == 0)
        {
            throw new ArgumentException("At least one question is required.", nameof(questions));
        }

        var copy = new Dictionary<string, TypeSafeQuestion>(questions.Count, StringComparer.Ordinal);
        foreach (var (id, question) in questions)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Question IDs cannot be blank.", nameof(questions));
            }

            ArgumentNullException.ThrowIfNull(question);
            copy.Add(id, question);
        }

        if (model is not null && string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("The model cannot be blank.", nameof(model));
        }

        State = JsonContentGuard.Clone(state, nameof(state));
        _questions = new ReadOnlyDictionary<string, TypeSafeQuestion>(copy);
        Model = model?.Trim();
    }

    /// <summary>Gets the state shared by all questions.</summary>
    public JsonNode State { get; }

    /// <summary>Gets questions keyed by the IDs used to find their answers.</summary>
    public IReadOnlyDictionary<string, TypeSafeQuestion> Questions => _questions;

    /// <summary>Gets the optional model override.</summary>
    public string? Model { get; }

    /// <summary>Serializes a CLR value into structured state using reflection-based JSON metadata.</summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="state">The value to serialize.</param>
    /// <param name="questions">Questions keyed by caller-chosen IDs.</param>
    /// <param name="model">An optional model override.</param>
    /// <param name="serializerOptions">Optional JSON serialization settings.</param>
    /// <returns>A System One request.</returns>
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    [RequiresUnreferencedCode("JSON serialization may require members removed by trimming.")]
    public static SystemOneRequest From<TState>(
        TState state,
        IReadOnlyDictionary<string, TypeSafeQuestion> questions,
        string? model = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        var node = JsonSerializer.SerializeToNode(state, serializerOptions)
            ?? throw new ArgumentNullException(nameof(state));
        return new SystemOneRequest(node, questions, model);
    }

    /// <summary>Serializes a CLR value into structured state using generated JSON metadata.</summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="state">The value to serialize.</param>
    /// <param name="questions">Questions keyed by caller-chosen IDs.</param>
    /// <param name="typeInfo">Generated metadata for the state type.</param>
    /// <param name="model">An optional model override.</param>
    /// <returns>A System One request.</returns>
    public static SystemOneRequest From<TState>(
        TState state,
        IReadOnlyDictionary<string, TypeSafeQuestion> questions,
        JsonTypeInfo<TState> typeInfo,
        string? model = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var node = JsonSerializer.SerializeToNode(state, typeInfo)
            ?? throw new ArgumentNullException(nameof(state));
        return new SystemOneRequest(node, questions, model);
    }
}
