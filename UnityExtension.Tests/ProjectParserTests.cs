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
}
