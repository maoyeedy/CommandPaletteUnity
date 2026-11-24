using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

internal sealed partial class OpenUnityHubCommand : InvokableCommand
{
    public OpenUnityHubCommand()
    {
        Name = "Open Unity Hub";
        Icon = Resources.IconUnity;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            ShellHelpers.OpenInShell("unityhub://");
            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Error opening Unity Hub: {ex.Message}");
        }
    }
}
