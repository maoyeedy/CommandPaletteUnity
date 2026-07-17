using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

internal sealed class SettingsManager : JsonSettingsManager
{
    private ToggleSetting GroupFavoritesFirstSetting { get; }
    public bool GroupFavoritesFirst => GroupFavoritesFirstSetting.Value;

    private ToggleSetting HideMissingProjectsSetting { get; }
    public bool HideMissingProjects => HideMissingProjectsSetting.Value;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("UnityExtension");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        GroupFavoritesFirstSetting = new ToggleSetting(
            key: "groupFavoritesFirst",
            label: "Group favorites first",
            description: "Display favorite Unity projects at the top of the list",
            defaultValue: true
        );

        HideMissingProjectsSetting = new ToggleSetting(
            key: "hideMissingProjects",
            label: "Hide projects missing from disk",
            description: "Don't list Unity projects whose folder no longer exists on disk",
            defaultValue: false
        );

        Settings.Add(GroupFavoritesFirstSetting);
        Settings.Add(HideMissingProjectsSetting);

        LoadSettings();

        Settings.SettingsChanged += (sender, args) =>
        {
            SaveSettings();
        };
    }
}
