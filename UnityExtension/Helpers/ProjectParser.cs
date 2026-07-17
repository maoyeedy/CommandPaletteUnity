using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityExtension;

public static class ProjectParser
{
    internal static List<UnityProject> GetUnityProjects()
    {
        var projectsJsonFilePath = Resources.ProjectsJsonPath;

        if (!File.Exists(projectsJsonFilePath))
        {
            return new List<UnityProject>();
        }

        string jsonContent;
        try
        {
            jsonContent = File.ReadAllText(projectsJsonFilePath);
        }
        catch (Exception)
        {
            // File exists but couldn't be read (locked, permissions, etc.)
            return new List<UnityProject>();
        }

        return ParseProjects(jsonContent);
    }

    /// <summary>
    /// Parses the contents of Unity Hub's projects-v1.json into a list of projects.
    /// Each entry is parsed independently: a single malformed or incomplete entry
    /// (e.g. a dead reference to a moved/deleted project) is skipped rather than
    /// aborting the whole list.
    /// </summary>
    internal static List<UnityProject> ParseProjects(string jsonContent)
    {
        var result = new List<UnityProject>();

        JsonElement projectsRoot;
        try
        {
            projectsRoot = JsonDocument.Parse(jsonContent).RootElement;
        }
        catch (JsonException)
        {
            // Corrupt/unreadable JSON → empty list; the page shows "No projects found".
            return result;
        }

        if (
            !projectsRoot.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
        )
        {
            return result;
        }

        foreach (var property in data.EnumerateObject())
        {
            try
            {
                var projectPath = property.Name;
                var projectInfo = property.Value;

                var fallbackTitle = Path.GetFileName(projectPath.TrimEnd('/', '\\'));

                var title =
                    projectInfo.TryGetProperty("title", out var titleEl)
                    && titleEl.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(titleEl.GetString())
                        ? titleEl.GetString()!
                        : fallbackTitle;

                var version =
                    projectInfo.TryGetProperty("version", out var versionEl)
                    && versionEl.ValueKind == JsonValueKind.String
                        ? versionEl.GetString()!
                        : "Unknown";

                long lastModified = 0;
                if (
                    projectInfo.TryGetProperty("lastModified", out var lastModifiedEl)
                    && lastModifiedEl.ValueKind == JsonValueKind.Number
                    && lastModifiedEl.TryGetInt64(out var parsedLastModified)
                )
                {
                    lastModified = parsedLastModified;
                }

                var isFavorite =
                    projectInfo.TryGetProperty("isFavorite", out var isFavoriteEl)
                    && isFavoriteEl.ValueKind == JsonValueKind.True;

                result.Add(
                    new UnityProject
                    {
                        Path = projectPath,
                        Title = title,
                        Version = version,
                        LastModified = lastModified,
                        IsFavorite = isFavorite,
                        Exists = SafeDirectoryExists(projectPath),
                    }
                );
            }
            catch (Exception)
            {
                // Skip only this malformed entry; keep parsing the rest.
            }
        }

        return result;
    }

    private static bool SafeDirectoryExists(string path)
    {
        try
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
