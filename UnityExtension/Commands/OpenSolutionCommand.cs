using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

internal sealed partial class OpenSolutionCommand : InvokableCommand
{
    private readonly string _solutionPath;

    public OpenSolutionCommand(string solutionPath)
    {
        Name = "Open solution";
        Icon = Resources.IconCode;
        _solutionPath = solutionPath;
    }

    public override ICommandResult Invoke()
    {
        if (!File.Exists(_solutionPath))
        {
            return CommandResult.ShowToast($"Solution file no longer exists:\n{_solutionPath}");
        }

        try
        {
            ShellHelpers.OpenInShell(_solutionPath);
            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Error opening solution: {ex.Message}");
        }
    }

    internal static string? TryFindSolutionFile(string projectPath, string projectTitle)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
        {
            return null;
        }

        try
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pattern in new[] { "*.sln", "*.slnx" })
            {
                foreach (var file in Directory.EnumerateFiles(projectPath, pattern))
                {
                    var ext = Path.GetExtension(file);
                    if (
                        string.Equals(ext, ".sln", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ext, ".slnx", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        candidates.Add(file);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var folderName = Path.GetFileName(projectPath.TrimEnd('\\', '/'));
            var ordered = candidates.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

            return ordered.FirstOrDefault(f =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(f),
                    folderName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                ?? ordered.FirstOrDefault(f =>
                    !string.IsNullOrEmpty(projectTitle)
                    && string.Equals(
                        Path.GetFileNameWithoutExtension(f),
                        projectTitle,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?? ordered[0];
        }
        catch (Exception)
        {
            return null;
        }
    }
}
