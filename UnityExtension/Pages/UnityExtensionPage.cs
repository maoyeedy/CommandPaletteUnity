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

internal sealed partial class UnityExtensionPage : ListPage
{
    private readonly SettingsManager _settingsManager;

    public UnityExtensionPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = Resources.IconUnity;
        Title = "Recent Unity Projects";
        Name = "Recent Unity Projects";
        ShowDetails = false;
    }

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
                    new RemoveProjectsCommand([project], () => RaiseItemsChanged())
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
            tags.Add(new Tag("⭐ Favorite") { Foreground = ColorHelpers.FromRgb(222, 186, 56) });
        }

        if (!project.Exists)
        {
            tags.Add(new Tag("Missing") { Foreground = ColorHelpers.FromRgb(224, 108, 117) });
        }

        tags.Add(new Tag(project.Version));

        return new ListItem(defaultCommand)
        {
            Title = project.Title,
            Subtitle = project.Exists ? project.Path : $"{project.Path}  (folder not found)",
            Icon = project.Exists ? Resources.IconUnity : Resources.IconMissing,
            Tags = tags.ToArray(),
            MoreCommands = contextCommands.ToArray(),
        };
    }
}
