using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

/// <summary>
/// Command to open a Unity Project
/// </summary>
internal sealed partial class OpenUnityCommand : InvokableCommand
{
    private readonly string _projectPath;
    private readonly string _projectVersion;
    private string? _editorPath;

    public OpenUnityCommand(UnityProject project)
    {
        Name = "Open in Unity";
        Icon = Resources.IconUnity;

        _projectPath = project.Path;
        _projectVersion = project.Version;
    }

    public override CommandResult Invoke()
    {
        // Lazy-load only when invoking
        _editorPath ??= EditorParser.GetExecutablePathForVersion(_projectVersion);

        if (string.IsNullOrEmpty(_editorPath) || !File.Exists(_editorPath))
        {
            return CommandResult.ShowToast(
                $"Unity {_projectVersion} not found. Please install through Unity Hub."
            );
        }

        try
        {
            var arguments = $"-projectPath \"{_projectPath}\"";
            ShellHelpers.OpenInShell(_editorPath, arguments);
            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Error launching Unity: {ex.Message}");
        }
    }
}
