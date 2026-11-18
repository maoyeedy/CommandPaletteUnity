using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

/// <summary>
/// Asks for confirmation, then deletes the projects' rows from Hub 4's hub.db (like
/// `unity projects remove`). Project folders on disk are never touched.
/// </summary>
internal sealed partial class RemoveProjectsCommand : InvokableCommand
{
    private readonly IReadOnlyList<UnityProject> _projects;
    private readonly Action _onRemoved;

    public RemoveProjectsCommand(IReadOnlyList<UnityProject> projects, Action onRemoved)
    {
        _projects = projects;
        _onRemoved = onRemoved;
        Name =
            projects.Count == 1
                ? "Remove from Unity Hub list"
                : $"Remove all missing projects ({projects.Count})";
        Icon = Resources.IconRemove;
    }

    public override CommandResult Invoke()
    {
        var description =
            _projects.Count == 1
                ? $"{_projects[0].Title}\n{_projects[0].Path}\n\nRemoves the entry from Unity Hub's project list. The project folder is not deleted."
                : $"Removes {_projects.Count} projects whose folders are gone from Unity Hub's project list.";

        return CommandResult.Confirm(
            new ConfirmationArgs
            {
                Title = _projects.Count == 1 ? "Remove from Unity Hub list?" : $"{Name}?",
                Description = description,
                PrimaryCommand = new ConfirmedCommand(_projects, _onRemoved),
                IsPrimaryCommandCritical = true,
            }
        );
    }

    private sealed partial class ConfirmedCommand : InvokableCommand
    {
        private readonly IReadOnlyList<UnityProject> _projects;
        private readonly Action _onRemoved;

        public ConfirmedCommand(IReadOnlyList<UnityProject> projects, Action onRemoved)
        {
            _projects = projects;
            _onRemoved = onRemoved;
            Name = "Remove";
            Icon = Resources.IconRemove;
        }

        public override CommandResult Invoke()
        {
            var dbPath = Path.Combine(Resources.UnityHubDir, "hub.db");
            var removed = _projects.Count(p => HubDatabase.TryRemoveProject(dbPath, p.Path));
            _onRemoved();

            var message =
                removed == _projects.Count
                    ? (
                        removed == 1
                            ? $"Removed {_projects[0].Title} from Unity Hub's list"
                            : $"Removed {removed} projects from Unity Hub's list"
                    )
                    : $"Removed {removed} of {_projects.Count}. Unity Hub may be busy; try again.";

            return CommandResult.ShowToast(
                new ToastArgs { Message = message, Result = CommandResult.KeepOpen() }
            );
        }
    }
}
