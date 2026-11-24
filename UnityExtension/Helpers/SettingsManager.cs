using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

internal sealed class SettingsManager : JsonSettingsManager
{
    public const string SortRecent = "recent";
    public const string SortName = "name";
    public const string SortVersion = "version";

    private ChoiceSetSetting SortOrderSetting { get; }
    public string SortOrder => SortOrderSetting.Value ?? SortRecent;

    private ToggleSetting GroupFavoritesFirstSetting { get; }
    public bool GroupFavoritesFirst => GroupFavoritesFirstSetting.Value;

    private ToggleSetting GroupByEditorVersionSetting { get; }
    public bool GroupByEditorVersion => GroupByEditorVersionSetting.Value;

    private ToggleSetting HideMissingProjectsSetting { get; }
    public bool HideMissingProjects => HideMissingProjectsSetting.Value;

    private ToggleSetting ShowDetailsSetting { get; }
    public bool ShowDetails => ShowDetailsSetting.Value;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("UnityExtension");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        SortOrderSetting = new ChoiceSetSetting(
            key: "sortOrder",
            label: "Sort projects by",
            description: "Order used in the project list",
            choices: new List<ChoiceSetSetting.Choice>
            {
                new("Recently modified", SortRecent),
                new("Name (A-Z)", SortName),
                new("Editor version", SortVersion),
            }
        )
        {
            IgnoreUnknownValue = true,
        };

        GroupFavoritesFirstSetting = new ToggleSetting(
            key: "groupFavoritesFirst",
            label: "Group favorites first",
            description: "Pin Unity Hub favorites to the top of the list",
            defaultValue: true
        );

        GroupByEditorVersionSetting = new ToggleSetting(
            key: "groupByEditorVersion",
            label: "Group by editor version",
            description: "Organize projects under section headers by Unity editor version",
            defaultValue: true
        );

        HideMissingProjectsSetting = new ToggleSetting(
            key: "hideMissingProjects",
            label: "Hide missing projects",
            description: "Hide projects whose folder no longer exists on disk",
            defaultValue: false
        );

        ShowDetailsSetting = new ToggleSetting(
            key: "showDetails",
            label: "Show details pane",
            description: "Show editor version, path, tags, Git repo, and pipeline beside the list",
            defaultValue: true
        );

        Settings.Add(SortOrderSetting);
        Settings.Add(GroupFavoritesFirstSetting);
        Settings.Add(GroupByEditorVersionSetting);
        Settings.Add(HideMissingProjectsSetting);
        Settings.Add(ShowDetailsSetting);

        LoadSettings();

        Settings.SettingsChanged += (sender, args) =>
        {
            SaveSettings();
        };
    }
}
