using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

/// <summary>
/// Deletes every projects row from Hub 4's hub.db. Project folders on disk are never touched.
/// The settings page confirms first with an inline two-step card before invoking this command;
/// a CommandResult.Confirm dialog would belong to the palette window, not the settings window.
/// </summary>
internal sealed partial class RemoveAllProjectsCommand : InvokableCommand
{
    public RemoveAllProjectsCommand()
    {
        Name = "Remove all projects from Unity Hub";
        Icon = Resources.IconRemove;
    }

    public override CommandResult Invoke()
    {
        var dbPath = Path.Combine(Resources.UnityHubDir, "hub.db");
        var removed = HubDatabase.TryRemoveAllProjects(dbPath);

        var message =
            removed > 0
                ? $"Removed {removed} projects from Unity Hub"
                : "Nothing removed. Unity Hub may be busy, or your Hub version is older than v4.";

        return CommandResult.ShowToast(
            new ToastArgs { Message = message, Result = CommandResult.KeepOpen() }
        );
    }
}
