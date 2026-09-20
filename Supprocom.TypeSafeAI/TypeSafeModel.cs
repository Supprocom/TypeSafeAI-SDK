namespace Supprocom.TypeSafeAI;

/// <summary>Metadata for a model available to the authenticated account.</summary>
public sealed record TypeSafeModel
{
    internal TypeSafeModel(string name, string description, string releaseDate)
    {
        Name = name;
        Description = description;
        ReleaseDate = releaseDate;
    }

    /// <summary>Gets the model name or alias accepted by requests.</summary>
    public string Name { get; }

    /// <summary>Gets the service-provided description.</summary>
    public string Description { get; }

    /// <summary>Gets the release date in the service's YYYY-MM-DD representation.</summary>
    public string ReleaseDate { get; }
}
