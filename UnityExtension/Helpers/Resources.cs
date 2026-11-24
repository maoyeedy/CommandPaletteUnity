using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

public static class Resources
{
    public static string UnityHubDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnityHub"
        );

    public static IconInfo IconUrl => new("\uE8A7");

    // Segoe Fluent "FolderOpen" glyph -- matches PowerToys Search Apps "Open file location".
    public static IconInfo IconFolder => new("\uE838");

    // Segoe Fluent "Warning" glyph -- used to flag projects whose folder is gone from disk.
    public static IconInfo IconMissing => new("\uE7BA");

    public static IconInfo IconRemove => new("\uE74D");

    public static IconInfo IconUnity => IconHelpers.FromRelativePath("Assets\\UnityLogo.png");
}
