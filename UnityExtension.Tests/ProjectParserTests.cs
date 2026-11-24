using System.IO;
using System.Linq;
using Xunit;

namespace UnityExtension.Tests;

public sealed class ProjectParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"data": []}""")]
    public void Unusable_input_gives_empty_list(string json)
    {
        Assert.Empty(ProjectParser.ParseProjects(json));
    }

    [Fact]
    public void Incomplete_entry_does_not_hide_other_projects()
    {
        const string json = """
            {"data": {
              "C:/gone/Dead": {},
              "C:/p/NoVersion": {"title": "NoVersion", "lastModified": 5},
              "C:/p/Good": {"title": "Good", "version": "6000.0.1f1", "lastModified": 7, "isFavorite": true}
            }}
            """;

        var projects = ProjectParser.ParseProjects(json);

        Assert.Equal(["Dead", "NoVersion", "Good"], projects.Select(p => p.Title));
        var good = projects.Single(p => p.Path == "C:/p/Good");
        Assert.Equal("6000.0.1f1", good.Version);
        Assert.Equal(7, good.LastModified);
        Assert.True(good.IsFavorite);
    }

    [Fact]
    public void Missing_fields_fall_back_to_defaults()
    {
        const string json = """{"data": {"C:/p/Folder/": {"lastModified": "oops"}}}""";

        var project = Assert.Single(ProjectParser.ParseProjects(json));

        Assert.Equal("Folder", project.Title);
        Assert.Equal("Unknown", project.Version);
        Assert.Equal(0, project.LastModified);
        Assert.False(project.IsFavorite);
    }

    [Fact]
    public void Exists_reflects_whether_the_folder_is_on_disk()
    {
        var present = Directory.CreateTempSubdirectory().FullName;
        var absent = Path.Combine(present, "missing");
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    data = new System.Collections.Generic.Dictionary<string, object>
                    {
                        [present] = new { },
                        [absent] = new { },
                    },
                }
            );

            var projects = ProjectParser.ParseProjects(json);

            Assert.True(projects.Single(p => p.Path == present).Exists);
            Assert.False(projects.Single(p => p.Path == absent).Exists);
        }
        finally
        {
            Directory.Delete(present);
        }
    }

    [Theory]
    [InlineData(@"C:\Users\alice", @"C:\Users\alice", "~")]
    [InlineData(@"C:\Users\alice\", @"C:\Users\alice", "~")]
    [InlineData(@"C:/Users/alice/", @"C:\Users\alice", "~")]
    [InlineData(@"C:\Users\alice\Projects\Game", @"C:\Users\alice", @"~\Projects\Game")]
    [InlineData(@"C:/Users/alice/Projects/Game", @"C:\Users\alice", "~/Projects/Game")]
    [InlineData(@"c:\users\ALICE\Projects\Game", @"C:\Users\alice\", @"~\Projects\Game")]
    [InlineData(
        @"C:\Users\alice2\Projects\Game",
        @"C:\Users\alice",
        @"C:\Users\alice2\Projects\Game"
    )]
    [InlineData(@"D:\Projects\Game", @"C:\Users\alice", @"D:\Projects\Game")]
    [InlineData("", @"C:\Users\alice", "")]
    public void Shorten_home_path(string path, string homeDir, string expected)
    {
        Assert.Equal(expected, UnityExtensionPage.ShortenHomePath(path, homeDir));
    }

    [Theory]
    [InlineData("6000.0.1f1", "7f159b6136da", "unityhub://6000.0.1f1/7f159b6136da")]
    [InlineData("2022.3.10f1-alpha", "abc123DEF", "unityhub://2022.3.10f1-alpha/abc123DEF")]
    [InlineData("6000.0.1f1", "", null)]
    [InlineData("Unknown", "7f159b6136da", null)]
    [InlineData("", "7f159b6136da", null)]
    [InlineData("6000.0.1f1/extra", "7f159b6136da", null)]
    [InlineData("6000.0.1f1", "7f159b6136da?x=1", null)]
    [InlineData("6000.0.1f1", "7f15 9b61", null)]
    public void Build_hub_install_uri(string version, string changeset, string? expected)
    {
        Assert.Equal(expected, OpenUnityCommand.TryBuildHubInstallUri(version, changeset));
    }
}
