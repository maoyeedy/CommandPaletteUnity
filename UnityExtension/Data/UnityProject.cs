using System.Collections.Generic;

namespace UnityExtension;

internal sealed record UnityProject
{
    public string Path { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public long LastModified { get; init; }
    public bool IsFavorite { get; init; }

    /// <summary>Hub 4 project tags; empty for Hub 3 or untagged projects.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Whether the project's folder still exists on disk. Dead references
    /// (moved/deleted projects still listed by Unity Hub) have this set to false.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>Read from hub.db (Hub 4), so "Remove from list" can delete its row.</summary>
    public bool FromHubDb { get; init; }
}
