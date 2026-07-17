namespace UnityExtension;

internal sealed record UnityProject
{
    public string Path { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long LastModified { get; init; }
    public bool IsFavorite { get; init; }

    /// <summary>
    /// Whether the project's folder still exists on disk. Dead references
    /// (moved/deleted projects still listed by Unity Hub) have this set to false.
    /// </summary>
    public bool Exists { get; init; }
}
