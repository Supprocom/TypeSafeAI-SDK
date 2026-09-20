namespace Supprocom.TypeSafeAI;

/// <summary>Operations exposed by the TypeSafe AI API.</summary>
public interface ITypeSafeClient
{
    /// <summary>Answers named questions about text or structured state.</summary>
    /// <param name="request">The state, questions, and optional model override.</param>
    /// <param name="options">Optional settings for this call.</param>
    /// <param name="cancellationToken">Cancels the active attempt or a pending retry.</param>
    /// <returns>The model's typed answers and token usage.</returns>
    Task<SystemOneResponse> SystemOneAsync(
        SystemOneRequest request,
        TypeSafeRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists models available to the authenticated account.</summary>
    /// <param name="options">Optional settings for this call.</param>
    /// <param name="cancellationToken">Cancels the active attempt or a pending retry.</param>
    /// <returns>Available model metadata.</returns>
    Task<IReadOnlyList<TypeSafeModel>> ListModelsAsync(
        TypeSafeRequestOptions? options = null,
        CancellationToken cancellationToken = default);
}
