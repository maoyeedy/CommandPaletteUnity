using System.Collections.Generic;
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
}
