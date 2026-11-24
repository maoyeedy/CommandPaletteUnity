using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

/// <summary>
/// Asks for confirmation, then deletes every projects row from Hub 4's hub.db. Project folders
/// on disk are never touched.
/// </summary>
internal sealed partial class RemoveAllProjectsCommand : InvokableCommand
{
    public RemoveAllProjectsCommand()
    {
        Name = "Remove all projects from Unity Hub list";
        Icon = Resources.IconRemove;
    }

    public override CommandResult Invoke() =>
        CommandResult.Confirm(
            new ConfirmationArgs
            {
                Title = "Remove all projects from Unity Hub?",
                Description =
                    "Clears every entry from Hub's list, including projects still on disk. Your project files stay untouched.",
                PrimaryCommand = new ConfirmedCommand(),
                IsPrimaryCommandCritical = true,
            }
        );

    private sealed partial class ConfirmedCommand : InvokableCommand
    {
        public ConfirmedCommand()
        {
            Name = "Remove all projects";
            Icon = Resources.IconRemove;
        }

        public override CommandResult Invoke()
        {
            var dbPath = Path.Combine(Resources.UnityHubDir, "hub.db");
            var removed = HubDatabase.TryRemoveAllProjects(dbPath);

            var message =
                removed > 0
                    ? $"Removed {removed} projects from Unity Hub's list"
                    : "Nothing removed. Unity Hub may be busy, or this is Hub 3.";

            return CommandResult.ShowToast(
                new ToastArgs { Message = message, Result = CommandResult.KeepOpen() }
            );
        }
    }
}
