using System;
using System.Collections.Generic;
using System.IO;

namespace UnityExtension;

internal sealed record UnityProjectCandidate(
    string Path,
    string Title,
    string Version,
    string Changeset,
    long LastModified
);

internal sealed record ProjectScanResult(
    IReadOnlyList<UnityProjectCandidate> Candidates,
    int AlreadyInHubCount,
    int InvalidFolderCount,
    bool Truncated
);

/// <summary>
/// Validates and discovers Unity project folders on disk by checking for Assets/ and
/// ProjectSettings/ProjectVersion.txt.
/// </summary>
internal static class ProjectScanner
{
    private static readonly HashSet<string> SkippedDirectoryNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Library",
        "Temp",
        "Logs",
        "obj",
        "bin",
        "node_modules",
    };

    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\');
        }
        catch (Exception)
        {
            return path.Replace('/', '\\').TrimEnd('\\');
        }
    }

    internal static UnityProjectCandidate? TryReadProjectCandidate(string folderPath)
    {
        var fullPath = NormalizePath(folderPath);
        if (string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        try
        {
            if (!Directory.Exists(Path.Combine(fullPath, "Assets")))
            {
                return null;
            }

            var versionFile = Path.Combine(fullPath, "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(versionFile))
            {
                return null;
            }

            var version = string.Empty;
            var changeset = string.Empty;

            foreach (var rawLine in File.ReadLines(versionFile))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))
                {
                    version = line["m_EditorVersion:".Length..].Trim();
                }
                else if (line.StartsWith("m_EditorVersionWithRevision:", StringComparison.Ordinal))
                {
                    var value = line["m_EditorVersionWithRevision:".Length..].Trim();
                    var openParen = value.IndexOf('(');
                    var closeParen = value.IndexOf(')');
                    if (openParen >= 0 && closeParen > openParen + 1)
                    {
                        changeset = value[(openParen + 1)..closeParen].Trim();
                    }
                }
            }

            if (string.IsNullOrEmpty(version))
            {
                return null;
            }

            var title = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(title))
            {
                title = fullPath;
            }

            var lastModified = new DateTimeOffset(
                Directory.GetLastWriteTimeUtc(fullPath)
            ).ToUnixTimeMilliseconds();

            return new UnityProjectCandidate(fullPath, title, version, changeset, lastModified);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static ProjectScanResult InspectFolders(
        IEnumerable<string> folderPaths,
        IReadOnlySet<string> existingPaths
    )
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<UnityProjectCandidate>();
        var alreadyInHub = 0;
        var invalid = 0;

        foreach (var rawPath in folderPaths)
        {
            var normalized = NormalizePath(rawPath);
            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            if (existingPaths.Contains(normalized))
            {
                alreadyInHub++;
                continue;
            }

            var candidate = TryReadProjectCandidate(normalized);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
            else
            {
                invalid++;
            }
        }

        SortCandidates(candidates);
        return new ProjectScanResult(candidates, alreadyInHub, invalid, Truncated: false);
    }

    internal static ProjectScanResult ScanRecursively(
        string rootFolder,
        IReadOnlySet<string> existingPaths,
        int maxDepth = 4,
        int maxCandidates = 50
    )
    {
        var normalizedRoot = NormalizePath(rootFolder);
        if (string.IsNullOrEmpty(normalizedRoot) || !Directory.Exists(normalizedRoot))
        {
            return new ProjectScanResult([], 0, 0, Truncated: false);
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<UnityProjectCandidate>();
        var alreadyInHub = 0;
        var truncated = false;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((normalizedRoot, 0));

        while (queue.Count > 0)
        {
            var (dir, depth) = queue.Dequeue();
            if (!visited.Add(dir))
            {
                continue;
            }

            var candidate = TryReadProjectCandidate(dir);
            if (candidate is not null)
            {
                if (existingPaths.Contains(candidate.Path))
                {
                    alreadyInHub++;
                }
                else if (candidates.Count >= maxCandidates)
                {
                    truncated = true;
                    break;
                }
                else
                {
                    candidates.Add(candidate);
                }

                continue;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var rawSub in subdirs)
            {
                var sub = NormalizePath(rawSub);
                if (string.IsNullOrEmpty(sub) || ShouldSkipDirectory(sub))
                {
                    continue;
                }

                queue.Enqueue((sub, depth + 1));
            }
        }

        SortCandidates(candidates);
        return new ProjectScanResult(candidates, alreadyInHub, InvalidFolderCount: 0, truncated);
    }

    private static bool ShouldSkipDirectory(string dirPath)
    {
        var name = Path.GetFileName(dirPath);
        if (string.IsNullOrEmpty(name))
        {
            return true;
        }

        if (name.StartsWith('.') || SkippedDirectoryNames.Contains(name))
        {
            return true;
        }

        try
        {
            return (File.GetAttributes(dirPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static void SortCandidates(List<UnityProjectCandidate> candidates) =>
        candidates.Sort(
            static (a, b) =>
            {
                var cmp = StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title);
                return cmp != 0 ? cmp : StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
            }
        );
}
