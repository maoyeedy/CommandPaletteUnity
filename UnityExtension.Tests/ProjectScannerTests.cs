using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace UnityExtension.Tests;

public sealed class ProjectScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("scantest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TheoryData<string, bool, string?, string?, string?> CandidateCases =>
        new()
        {
            {
                "version with revision",
                true,
                "m_EditorVersion: 2022.3.42f1\nm_EditorVersionWithRevision: 2022.3.42f1 (2dcb6a0abc42)\n",
                "2022.3.42f1",
                "2dcb6a0abc42"
            },
            { "version without revision", true, "m_EditorVersion: 6000.0.1f1\n", "6000.0.1f1", "" },
            { "missing Assets folder -> null", false, "m_EditorVersion: 6000.0.1f1\n", null, null },
            { "missing ProjectVersion.txt -> null", true, null, null, null },
            { "empty version -> null", true, "m_EditorVersion:   \n", null, null },
        };

    [Theory]
    [MemberData(nameof(CandidateCases))]
    public void TryReadProjectCandidate_validates_structure_and_version(
        string name,
        bool createAssets,
        string? versionText,
        string? expectedVersion,
        string? expectedChangeset
    )
    {
        var dir = Path.Combine(_root, Sanitize(name));
        Directory.CreateDirectory(dir);
        if (createAssets)
        {
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
        }

        if (versionText is not null)
        {
            Directory.CreateDirectory(Path.Combine(dir, "ProjectSettings"));
            File.WriteAllText(
                Path.Combine(dir, "ProjectSettings", "ProjectVersion.txt"),
                versionText
            );
        }

        var candidate = ProjectScanner.TryReadProjectCandidate(dir);

        if (expectedVersion is null)
        {
            Assert.Null(candidate);
        }
        else
        {
            Assert.NotNull(candidate);
            Assert.Equal(ProjectScanner.NormalizePath(dir), candidate.Path);
            Assert.Equal(expectedVersion, candidate.Version);
            Assert.Equal(expectedChangeset, candidate.Changeset);
        }
    }

    [Fact]
    public void InspectFolders_deduplicates_skips_existing_and_counts_invalid()
    {
        var existingDir = CreateUnityProject(Path.Combine(_root, "Existing"), "6000.0.1f1");
        var newDir = CreateUnityProject(Path.Combine(_root, "NewProject"), "2022.3.42f1");
        var invalidDir = Directory.CreateDirectory(Path.Combine(_root, "NotUnity")).FullName;

        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProjectScanner.NormalizePath(existingDir).ToLowerInvariant(),
        };

        var result = ProjectScanner.InspectFolders(
            [existingDir.Replace('\\', '/') + "/", newDir, newDir, invalidDir],
            existingPaths
        );

        Assert.Single(result.Candidates);
        Assert.Equal("NewProject", result.Candidates[0].Title);
        Assert.Equal(1, result.AlreadyInHubCount);
        Assert.Equal(1, result.InvalidFolderCount);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void ScanRecursively_skips_ignored_dirs_and_stops_descending_into_projects()
    {
        var existing = CreateUnityProject(Path.Combine(_root, "AlreadyHub"), "6000.0.1f1");
        var alpha = CreateUnityProject(Path.Combine(_root, "Alpha"), "6000.0.2f1");
        // Nested project inside Alpha should not be discovered because recursion stops at Alpha.
        CreateUnityProject(Path.Combine(alpha, "Samples", "NestedInsideProject"), "6000.0.2f1");
        var nested = CreateUnityProject(Path.Combine(_root, "Sub", "Beta"), "2022.3.42f1");

        // Ignored folder names and dot-prefixed folders should never be scanned.
        CreateUnityProject(Path.Combine(_root, ".git", "HiddenProj"), "2022.3.1f1");
        CreateUnityProject(Path.Combine(_root, "Library", "CachedProj"), "2022.3.1f1");
        CreateUnityProject(Path.Combine(_root, "Temp", "TempProj"), "2022.3.1f1");

        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ProjectScanner.NormalizePath(existing),
        };

        var result = ProjectScanner.ScanRecursively(_root, existingPaths);

        Assert.Equal(["Alpha", "Beta"], result.Candidates.Select(c => c.Title).ToArray());
        Assert.Equal(ProjectScanner.NormalizePath(nested), result.Candidates[1].Path);
        Assert.Equal(1, result.AlreadyInHubCount);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void ScanRecursively_respects_maxDepth_and_maxCandidates()
    {
        CreateUnityProject(Path.Combine(_root, "D1"), "6000.0.1f1");
        CreateUnityProject(Path.Combine(_root, "A", "D2"), "6000.0.1f1");
        CreateUnityProject(Path.Combine(_root, "B", "C", "TooDeep"), "6000.0.1f1");

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var depthLimited = ProjectScanner.ScanRecursively(_root, existing, maxDepth: 2);
        Assert.Equal(["D1", "D2"], depthLimited.Candidates.Select(c => c.Title).ToArray());
        Assert.False(depthLimited.Truncated);

        var countLimited = ProjectScanner.ScanRecursively(
            _root,
            existing,
            maxDepth: 4,
            maxCandidates: 1
        );
        Assert.Single(countLimited.Candidates);
        Assert.True(countLimited.Truncated);
    }

    private static string CreateUnityProject(string path, string version)
    {
        Directory.CreateDirectory(Path.Combine(path, "Assets"));
        Directory.CreateDirectory(Path.Combine(path, "ProjectSettings"));
        File.WriteAllText(
            Path.Combine(path, "ProjectSettings", "ProjectVersion.txt"),
            $"m_EditorVersion: {version}\n"
        );
        return path;
    }

    private static string Sanitize(string name) =>
        new(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
