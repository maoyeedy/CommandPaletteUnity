using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace UnityExtension.Tests;

public sealed class ProjectOrderingTests
{
    private static readonly UnityProject[] SampleProjects =
    [
        new UnityProject
        {
            Path = @"C:\p\Beta",
            Title = "Beta",
            Version = "6000.0.2f1",
            LastModified = 200,
            IsFavorite = false,
        },
        new UnityProject
        {
            Path = @"C:\p\Alpha",
            Title = "Alpha",
            Version = "2022.3.50f1",
            LastModified = 300,
            IsFavorite = false,
        },
        new UnityProject
        {
            Path = @"C:\p\Gamma",
            Title = "Gamma",
            Version = "6000.0.10f1",
            LastModified = 100,
            IsFavorite = true,
        },
        new UnityProject
        {
            Path = @"C:\p\Delta",
            Title = "Delta",
            Version = "Unknown",
            LastModified = 400,
            IsFavorite = true,
        },
    ];

    [Theory]
    [InlineData("6000.0.10f1", "6000.0.2f1", -1)]
    [InlineData("6000.0.2f1", "6000.0.10f1", 1)]
    [InlineData("6000.0.2f1", "2022.3.50f1", -1)]
    [InlineData("2022.3.50f1", "2021.3.1f1", -1)]
    [InlineData("2021.3.1f1", "Unknown", -1)]
    [InlineData("Unknown", "2021.3.1f1", 1)]
    [InlineData("", "Unknown", 0)]
    [InlineData("6000.0.10f1", "6000.0.10f1", 0)]
    public void Compare_versions_descending(string a, string b, int expectedSign)
    {
        var cmp = UnityExtensionPage.CompareVersionsDescending(a, b);
        Assert.Equal(expectedSign, System.Math.Sign(cmp));
    }

    [Theory]
    [InlineData(SettingsManager.SortRecent, false, new[] { "Delta", "Alpha", "Beta", "Gamma" })]
    [InlineData(SettingsManager.SortRecent, true, new[] { "Delta", "Gamma", "Alpha", "Beta" })]
    [InlineData(SettingsManager.SortName, false, new[] { "Alpha", "Beta", "Delta", "Gamma" })]
    [InlineData(SettingsManager.SortName, true, new[] { "Delta", "Gamma", "Alpha", "Beta" })]
    [InlineData(SettingsManager.SortVersion, false, new[] { "Gamma", "Beta", "Alpha", "Delta" })]
    [InlineData(SettingsManager.SortVersion, true, new[] { "Gamma", "Delta", "Beta", "Alpha" })]
    public void Order_projects_respects_sort_and_favorites(
        string sortOrder,
        bool groupFavoritesFirst,
        string[] expectedTitles
    )
    {
        var ordered = UnityExtensionPage.OrderProjects(
            SampleProjects,
            sortOrder,
            groupFavoritesFirst
        );
        Assert.Equal(expectedTitles, ordered.Select(p => p.Title));
    }

    [Theory]
    [InlineData(
        "github",
        "Unity-Technologies",
        "Example",
        "https://github.com/Unity-Technologies/Example"
    )]
    [InlineData("GitLab", "my-org", "my-repo", "https://gitlab.com/my-org/my-repo")]
    [InlineData("github", "git://example.com/org", "repo", null)]
    [InlineData("github", "org", "git://example.com/repo", null)]
    [InlineData("github", "git@github.com:org", "repo", null)]
    [InlineData("bitbucket", "org", "repo", null)]
    [InlineData("", "org", "repo", null)]
    [InlineData("github", "", "repo", null)]
    [InlineData("github", "org", "", null)]
    public void Try_build_repo_uri_validates_https_provider_and_segments(
        string provider,
        string org,
        string repo,
        string? expected
    )
    {
        var project = new UnityProject
        {
            VcsProvider = provider,
            GitOrganization = org,
            GitRepository = repo,
        };

        var uri = UnityExtensionPage.TryBuildRepoUri(project);
        Assert.Equal(expected, uri?.AbsoluteUri);
    }

    [Theory]
    [InlineData(new string[0], "Title", null)]
    [InlineData(new[] { "Project-Psyqualogy.sln" }, "Psyqualogy", "Project-Psyqualogy.sln")]
    [InlineData(new[] { "Other.sln", "CustomTitle.sln" }, "CustomTitle", "CustomTitle.sln")]
    [InlineData(new[] { "Zeta.sln", "Alpha.slnx" }, "Other", "Alpha.slnx")]
    [InlineData(new[] { "App.slnx" }, "Other", "App.slnx")]
    public void Try_find_solution_file_selects_expected_candidate(
        string[] files,
        string projectTitle,
        string? expectedFileName
    )
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            foreach (var file in files)
            {
                File.WriteAllText(Path.Combine(dir.FullName, file), string.Empty);
            }

            var actual = OpenSolutionCommand.TryFindSolutionFile(dir.FullName, projectTitle);
            var expected = expectedFileName is null
                ? null
                : Path.Combine(dir.FullName, expectedFileName);
            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Try_find_solution_file_prefers_folder_name_when_present()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var folderMatch = $"{dir.Name}.sln";
            File.WriteAllText(Path.Combine(dir.FullName, "Alpha.sln"), string.Empty);
            File.WriteAllText(Path.Combine(dir.FullName, "TitleMatch.sln"), string.Empty);
            File.WriteAllText(Path.Combine(dir.FullName, folderMatch), string.Empty);

            var actual = OpenSolutionCommand.TryFindSolutionFile(dir.FullName, "TitleMatch");
            Assert.Equal(Path.Combine(dir.FullName, folderMatch), actual);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Try_find_solution_file_returns_null_for_missing_directory()
    {
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Assert.Null(OpenSolutionCommand.TryFindSolutionFile(missing, "Anything"));
    }

    [Theory]
    [InlineData(0L, null)]
    [InlineData(-1L, null)]
    [InlineData(30L, "Just now")]
    [InlineData(60L, "1 minute ago")]
    [InlineData(5L * 60, "5 minutes ago")]
    [InlineData(60L * 60, "1 hour ago")]
    [InlineData(3L * 60 * 60, "3 hours ago")]
    [InlineData(24L * 60 * 60, "1 day ago")]
    [InlineData(12L * 24 * 60 * 60, "12 days ago")]
    [InlineData(30L * 24 * 60 * 60, "1 month ago")]
    [InlineData(120L * 24 * 60 * 60, "4 months ago")]
    [InlineData(365L * 24 * 60 * 60, "1 year ago")]
    [InlineData(730L * 24 * 60 * 60, "2 years ago")]
    public void Format_relative_time_formats_unix_ms(long ageSeconds, string? expected)
    {
        var now = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        var ms =
            ageSeconds <= 0 ? ageSeconds : now.AddSeconds(-ageSeconds).ToUnixTimeMilliseconds();
        Assert.Equal(expected, UnityExtensionPage.FormatRelativeTime(ms, now));
    }

    [Theory]
    [InlineData(@"E:\thesis\workspaces\unity", "file:///E:/thesis/workspaces/unity")]
    [InlineData(@"C:\Users\alice\My Game", "file:///C:/Users/alice/My%20Game")]
    [InlineData(@"relative\path", null)]
    [InlineData("", null)]
    public void Try_build_folder_uri_handles_rooted_paths(string path, string? expected)
    {
        var uri = UnityExtensionPage.TryBuildFolderUri(path);
        Assert.Equal(expected, uri?.AbsoluteUri);
    }
}
