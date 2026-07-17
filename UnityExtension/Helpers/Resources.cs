using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

public static class Resources
{
    public static string ProjectsJsonPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnityHub\\projects-v1.json"
        );

    public static IconInfo IconUrl => new("\uE8A7");

    // Segoe Fluent "Warning" glyph \u2014 used to flag projects whose folder is gone from disk.
    public static IconInfo IconMissing => new("\uE7BA");

    public static IconInfo IconUnity => IconHelpers.FromRelativePath("Assets\\UnityLogo.png");
}
