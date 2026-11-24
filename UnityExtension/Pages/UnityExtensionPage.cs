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
        ShowDetails = true;
        _watcher = new HubDataWatcher(Resources.UnityHubDir, () => RaiseItemsChanged());
    }

    public void Dispose() => _watcher.Dispose();

    public override IListItem[] GetItems()
    {
        var items = new List<ListItem>();
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
                projects.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

                IEnumerable<UnityProject> ordered = _settingsManager.GroupFavoritesFirst
                    ? projects.Where(p => p.IsFavorite).Concat(projects.Where(p => !p.IsFavorite))
                    : projects;

                var missing = projects.Where(p => !p.Exists && p.FromHubDb).ToList();
                items.AddRange(ordered.Select(p => CreateProjectListItem(p, missing)));
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

    private ListItem CreateProjectListItem(UnityProject project, List<UnityProject> missing)
    {
        var defaultCommand = new OpenUnityCommand(project);

        var contextCommands = new List<IContextItem>
        {
            new CommandContextItem(new OpenExplorerCommand(project))
            {
                Title = "Open in File Explorer",
            },
        };

        // Only Hub 4 (hub.db) entries can be removed; Hub 3 JSON is never written.
        if (project.FromHubDb)
        {
            contextCommands.Add(
                new CommandContextItem(
                    new RemoveProjectsCommand(new[] { project }, () => RaiseItemsChanged())
                )
                {
                    IsCritical = true,
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
            Details = BuildDetails(project),
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

            if (
                (
                    string.Equals(project.VcsProvider, "github", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        project.VcsProvider,
                        "gitlab",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                && !string.IsNullOrEmpty(project.GitOrganization)
                && Uri.TryCreate(
                    $"https://{project.VcsProvider.ToLowerInvariant()}.com/{project.GitOrganization}/{project.GitRepository}",
                    UriKind.Absolute,
                    out var repoUri
                )
            )
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
            HeroImage = project.Exists ? Resources.IconUnity : Resources.IconMissing,
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
