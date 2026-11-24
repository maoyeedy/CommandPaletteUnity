// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace UnityExtension;

internal sealed partial class UnityExtensionPage : ListPage, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly HubDataWatcher _watcher;

    public UnityExtensionPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = Resources.IconUnity;
        Title = "Recent Unity Projects";
        Name = "Recent Unity Projects";
        ShowDetails = _settingsManager.ShowDetails;
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;
        _watcher = new HubDataWatcher(Resources.UnityHubDir, () => RaiseItemsChanged());
    }

    public void Dispose()
    {
        _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
        _watcher.Dispose();
    }

    private void OnSettingsChanged(object sender, Settings args)
    {
        ShowDetails = _settingsManager.ShowDetails;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        IsLoading = true;

        try
        {
            var projects = ProjectParser.GetUnityProjects();

            if (_settingsManager.HideMissingProjects)
            {
                projects = projects.Where(p => p.Exists).ToList();
            }

            if (projects.Count == 0)
            {
                items.Add(
                    new ListItem(new NoOpCommand())
                    {
                        Title = "No Unity projects found",
                        Subtitle =
                            "Make sure you have Unity Hub installed and have opened projects with it",
                        Icon = Resources.IconUrl,
                    }
                );
            }
            else
            {
                var missing = projects.Where(p => !p.Exists && p.FromHubDb).ToList();

                if (_settingsManager.GroupByEditorVersion)
                {
                    var groups = projects
                        .GroupBy(
                            p => string.IsNullOrWhiteSpace(p.Version) ? "Unknown" : p.Version,
                            StringComparer.OrdinalIgnoreCase
                        )
                        .ToList();

                    groups.Sort((a, b) => CompareVersionsDescending(a.Key, b.Key));

                    foreach (var group in groups)
                    {
                        var orderedGroup = OrderProjects(
                            group.ToList(),
                            _settingsManager.SortOrder,
                            _settingsManager.GroupFavoritesFirst
                        );
                        var groupItems = orderedGroup
                            .Select(p => (IListItem)CreateProjectListItem(p, missing))
                            .ToArray();
                        items.AddRange(new Section(group.Key, groupItems));
                    }
                }
                else
                {
                    var ordered = OrderProjects(
                        projects,
                        _settingsManager.SortOrder,
                        _settingsManager.GroupFavoritesFirst
                    );
                    items.AddRange(ordered.Select(p => CreateProjectListItem(p, missing)));
                }
            }
        }
        catch (Exception ex)
        {
            items.Add(
                new ListItem(new NoOpCommand())
                {
                    Title = "Error loading Unity projects",
                    Subtitle = ex.Message,
                    Icon = Resources.IconUrl,
                }
            );
        }

        IsLoading = false;
        return items.ToArray();
    }

    internal static List<UnityProject> OrderProjects(
        IReadOnlyList<UnityProject> projects,
        string sortOrder,
        bool groupFavoritesFirst
    )
    {
        var sorted = projects.ToList();
        sorted.Sort(
            (a, b) =>
            {
                var primary = sortOrder switch
                {
                    SettingsManager.SortName => StringComparer.OrdinalIgnoreCase.Compare(
                        a.Title,
                        b.Title
                    ),
                    SettingsManager.SortVersion => CompareVersionsDescending(a.Version, b.Version),
                    _ => b.LastModified.CompareTo(a.LastModified),
                };

                return primary != 0 ? primary : b.LastModified.CompareTo(a.LastModified);
            }
        );

        if (!groupFavoritesFirst)
        {
            return sorted;
        }

        return sorted.Where(p => p.IsFavorite).Concat(sorted.Where(p => !p.IsFavorite)).ToList();
    }

    internal static int CompareVersionsDescending(string a, string b)
    {
        var aUnknown =
            string.IsNullOrWhiteSpace(a)
            || string.Equals(a, "Unknown", StringComparison.OrdinalIgnoreCase);
        var bUnknown =
            string.IsNullOrWhiteSpace(b)
            || string.Equals(b, "Unknown", StringComparison.OrdinalIgnoreCase);

        if (aUnknown && bUnknown)
        {
            return 0;
        }

        if (aUnknown)
        {
            return 1;
        }

        if (bUnknown)
        {
            return -1;
        }

        int i = 0;
        int j = 0;
        while (i < a.Length && j < b.Length)
        {
            var aIsDigit = char.IsAsciiDigit(a[i]);
            var bIsDigit = char.IsAsciiDigit(b[j]);

            if (aIsDigit && bIsDigit)
            {
                long numA = ReadNumber(a, ref i);
                long numB = ReadNumber(b, ref j);
                var cmp = numB.CompareTo(numA);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else if (!aIsDigit && !bIsDigit)
            {
                var startA = i;
                while (i < a.Length && !char.IsAsciiDigit(a[i]))
                {
                    i++;
                }

                var startB = j;
                while (j < b.Length && !char.IsAsciiDigit(b[j]))
                {
                    j++;
                }

                var cmp = MemoryExtensions.CompareTo(
                    b.AsSpan(startB, j - startB),
                    a.AsSpan(startA, i - startA),
                    StringComparison.OrdinalIgnoreCase
                );
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else
            {
                // Digits sort before non-digits in descending order.
                return aIsDigit ? -1 : 1;
            }
        }

        return (b.Length - j).CompareTo(a.Length - i);

        static long ReadNumber(string s, ref int index)
        {
            long value = 0;
            while (index < s.Length && char.IsAsciiDigit(s[index]))
            {
                value = (value * 10) + (s[index] - '0');
                index++;
            }

            return value;
        }
    }

    private ListItem CreateProjectListItem(UnityProject project, List<UnityProject> missing)
    {
        var defaultCommand = new OpenUnityCommand(project);

        var contextCommands = new List<IContextItem>();

        if (project.Exists)
        {
            var consoleCommand = OpenInConsoleCommand.FromDirectory(project.Path);
            consoleCommand.Name = "Open in Console";
            contextCommands.Add(
                new CommandContextItem(consoleCommand)
                {
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true,
                        shift: true,
                        vkey: VirtualKey.R
                    ),
                }
            );
            contextCommands.Add(
                new CommandContextItem(new OpenExplorerCommand(project))
                {
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true,
                        shift: true,
                        vkey: VirtualKey.E
                    ),
                }
            );
        }

        contextCommands.Add(
            new CommandContextItem(new CopyPathCommand(project.Path))
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(
                    ctrl: true,
                    shift: true,
                    vkey: VirtualKey.C
                ),
            }
        );

        if (TryBuildRepoUri(project) is { } repoUri)
        {
            contextCommands.Add(
                new CommandContextItem(
                    new OpenUrlCommand(repoUri.AbsoluteUri)
                    {
                        Name = RepoCommandName(project.VcsProvider),
                        Icon = Resources.IconUrl,
                    }
                )
            );
        }

        // Only Hub 4 (hub.db) entries can be removed; Hub 3 JSON is never written.
        if (project.FromHubDb)
        {
            contextCommands.Add(
                new CommandContextItem(
                    new RemoveProjectsCommand(new[] { project }, () => RaiseItemsChanged())
                )
                {
                    IsCritical = true,
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true,
                        shift: true,
                        vkey: VirtualKey.Delete
                    ),
                }
            );

            if (!project.Exists && missing.Count > 1)
            {
                contextCommands.Add(
                    new CommandContextItem(
                        new RemoveProjectsCommand(missing, () => RaiseItemsChanged())
                    )
                    {
                        IsCritical = true,
                    }
                );
            }
        }

        var tags = new List<Tag>();
        if (project.IsFavorite)
        {
            tags.Add(
                new Tag("\u2B50")
                {
                    Foreground = ColorHelpers.FromRgb(222, 186, 56),
                    ToolTip = "Favorite",
                }
            );
        }

        if (!project.Exists)
        {
            tags.Add(new Tag("Missing") { Foreground = ColorHelpers.FromRgb(224, 108, 117) });
        }

        tags.Add(new Tag(project.Version));

        var displayPath = ShortenHomePath(project.Path);

        return new ListItem(defaultCommand)
        {
            Title = project.Title,
            Subtitle = project.Exists ? displayPath : $"{displayPath}  (folder not found)",
            Icon = project.Exists ? Resources.IconUnity : Resources.IconMissing,
            Tags = tags.ToArray(),
            Details = _settingsManager.ShowDetails ? BuildDetails(project) : null,
            MoreCommands = contextCommands.ToArray(),
        };
    }

    private static Details BuildDetails(UnityProject project)
    {
        var metadata = new List<IDetailsElement>
        {
            TextDetail("Editor", project.Version),
            TextDetail("Path", project.Path),
        };

        if (project.Tags.Count > 0)
        {
            metadata.Add(
                new DetailsElement
                {
                    Key = "Tags",
                    Data = new DetailsTags
                    {
                        Tags = project.Tags.Select(t => (ITag)new Tag(t)).ToArray(),
                    },
                }
            );
        }

        if (!string.IsNullOrEmpty(project.GitRepository))
        {
            var repoLabel = string.IsNullOrEmpty(project.GitOrganization)
                ? project.GitRepository
                : $"{project.GitOrganization}/{project.GitRepository}";

            if (TryBuildRepoUri(project) is { } repoUri)
            {
                metadata.Add(
                    new DetailsElement
                    {
                        Key = "Repository",
                        Data = new DetailsLink { Link = repoUri, Text = repoLabel },
                    }
                );
            }
            else
            {
                metadata.Add(TextDetail("Repository", repoLabel));
            }
        }

        if (!string.IsNullOrEmpty(project.RenderPipeline))
        {
            metadata.Add(TextDetail("Render pipeline", project.RenderPipeline));
        }

        if (!string.IsNullOrEmpty(project.BuildTarget))
        {
            metadata.Add(TextDetail("Build target", project.BuildTarget));
        }

        return new Details
        {
            Title = project.Title,
            Body = project.Exists ? string.Empty : "Project folder was not found on disk.",
            Metadata = metadata.ToArray(),
        };
    }

    private static DetailsElement TextDetail(string key, string value) =>
        new()
        {
            Key = key,
            Data = new DetailsTags { Tags = [new Tag(value)] },
        };

    internal static Uri? TryBuildRepoUri(UnityProject project)
    {
        if (
            !string.Equals(project.VcsProvider, "github", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(project.VcsProvider, "gitlab", StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        if (
            !IsSafeRepoSegment(project.GitOrganization) || !IsSafeRepoSegment(project.GitRepository)
        )
        {
            return null;
        }

        var url =
            $"https://{project.VcsProvider.ToLowerInvariant()}.com/{project.GitOrganization}/{project.GitRepository}";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;
    }

    private static bool IsSafeRepoSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || c is ':' or '?' or '#')
            {
                return false;
            }
        }

        return true;
    }

    private static string RepoCommandName(string vcsProvider) =>
        string.Equals(vcsProvider, "gitlab", StringComparison.OrdinalIgnoreCase)
            ? "Open on GitLab"
            : "Open on GitHub";

    internal static string ShortenHomePath(string path, string? homeDir = null)
    {
        homeDir ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        homeDir = homeDir.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(homeDir))
        {
            return path;
        }

        if (
            path.Length < homeDir.Length
            || !string.Equals(
                path[..homeDir.Length].Replace('/', '\\'),
                homeDir.Replace('/', '\\'),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return path;
        }

        if (path.Length == homeDir.Length)
        {
            return "~";
        }

        if (path[homeDir.Length] is '\\' or '/')
        {
            return path.Length == homeDir.Length + 1 ? "~" : "~" + path[homeDir.Length..];
        }

        return path;
    }
}
