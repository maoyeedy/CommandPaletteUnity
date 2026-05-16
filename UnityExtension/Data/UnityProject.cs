namespace UnityExtension;

internal sealed record UnityProject
{
    public string Path { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long LastModified { get; init; }
    public bool IsFavorite { get; init; }
}
