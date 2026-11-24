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
    private readonly string _changeset;
    private string? _editorPath;

    public OpenUnityCommand(UnityProject project)
    {
        Name = "Open in Unity";
        Icon = Resources.IconUnity;

        _projectPath = project.Path;
        _projectVersion = project.Version;
        _changeset = project.Changeset;
    }

    public override CommandResult Invoke()
    {
        // Dead reference: the project's folder no longer exists on disk. Don't try to
        // launch Unity against a missing path (also guards against the folder being
        // deleted after the list was built).
        if (!Directory.Exists(_projectPath))
        {
            return CommandResult.ShowToast($"Project folder no longer exists:\n{_projectPath}");
        }

        // Lazy-load and re-check on miss so newly installed editors work immediately.
        if (string.IsNullOrEmpty(_editorPath) || !File.Exists(_editorPath))
        {
            _editorPath = EditorParser.GetExecutablePathForVersion(_projectVersion);
        }

        if (string.IsNullOrEmpty(_editorPath) || !File.Exists(_editorPath))
        {
            var installUri = TryBuildHubInstallUri(_projectVersion, _changeset);
            if (installUri is not null)
            {
                return CommandResult.Confirm(
                    new ConfirmationArgs
                    {
                        Title = $"Unity {_projectVersion} is not installed",
                        Description = $"Open Unity Hub to install Unity {_projectVersion}?",
                        PrimaryCommand = new InstallEditorCommand(installUri),
                    }
                );
            }

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

    internal static string? TryBuildHubInstallUri(string version, string changeset)
    {
        if (
            string.IsNullOrEmpty(version)
            || string.Equals(version, "Unknown", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(changeset)
        )
        {
            return null;
        }

        foreach (var c in version)
        {
            if (
                c
                is not (
                    (>= 'a' and <= 'z')
                    or (>= 'A' and <= 'Z')
                    or (>= '0' and <= '9')
                    or '.'
                    or '-'
                    or '_'
                )
            )
            {
                return null;
            }
        }

        foreach (var c in changeset)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            {
                return null;
            }
        }

        return $"unityhub://{version}/{changeset}";
    }

    private sealed partial class InstallEditorCommand : InvokableCommand
    {
        private readonly string _installUri;

        public InstallEditorCommand(string installUri)
        {
            _installUri = installUri;
            Name = "Install in Unity Hub";
            Icon = Resources.IconUnity;
        }

        public override CommandResult Invoke()
        {
            try
            {
                ShellHelpers.OpenInShell(_installUri);
                return CommandResult.Hide();
            }
            catch (Exception ex)
            {
                return CommandResult.ShowToast($"Error opening Unity Hub: {ex.Message}");
            }
        }
    }
}
