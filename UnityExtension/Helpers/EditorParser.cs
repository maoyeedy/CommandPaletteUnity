using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace UnityExtension;

public static class EditorParser
{
    private static readonly Dictionary<string, string> _versionPathMap;

    static EditorParser()
    {
        _versionPathMap = GetVersionToPathMapping();
    }

    internal static Dictionary<string, string> GetVersionToPathMapping()
    {
        var versionPathMap = new Dictionary<string, string>();
        var regPaths = new[] { Registry.CurrentUser, Registry.LocalMachine };
        foreach (var hive in regPaths)
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Unity Technologies\Installer");
            if (key == null)
                continue;
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                var version = subKey?.GetValue("Version") as string;
                var installPath = subKey?.GetValue("Location x64") as string;
                if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(installPath))
                    continue;
                versionPathMap.TryAdd(version, installPath);
            }
        }
        return versionPathMap;
    }

    internal static string GetPathForVersion(string version)
    {
        if (_versionPathMap.TryGetValue(version, out var path))
        {
            return path;
        }

        return string.Empty;
    }

    internal static string GetExecutablePathForVersion(string version)
    {
        var path = GetPathForVersion(version);
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var executablePath = Path.Combine(path, "Editor", "Unity.exe");
        return File.Exists(executablePath) ? executablePath : string.Empty;
    }
}
