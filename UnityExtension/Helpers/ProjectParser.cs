using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityExtension;

public static class ProjectParser
{
    internal static List<UnityProject> GetUnityProjects() => LoadProjects(Resources.UnityHubDir);

    /// <summary>
    /// Loads the project list from a Unity Hub data folder. Hub 4: hub.db when
    /// meta.projects_json_migrated = '1', unless projects-v1.json is newer (Hub 3 after a
    /// downgrade). Otherwise projects-v1.json minus excludedProjectFolders.json.
    /// Never throws; any failure gives an empty list.
    /// </summary>
    internal static List<UnityProject> LoadProjects(string hubDir)
    {
        try
        {
            var dbPath = Path.Combine(hubDir, "hub.db");
            var jsonPath = Path.Combine(hubDir, "projects-v1.json");

            if (File.Exists(dbPath) && !IsJsonNewer(jsonPath, dbPath))
            {
                switch (HubDatabase.TryReadProjects(dbPath, out var rows))
                {
                    case HubDbStatus.Ok:
                        return ParseDbRows(rows);
                    case HubDbStatus.Failed:
                        return [];
                    case HubDbStatus.NotMigrated:
                    default:
                        break;
                }
            }

            if (!File.Exists(jsonPath))
            {
                return [];
            }

            var excluded = ReadExcluded(Path.Combine(hubDir, "excludedProjectFolders.json"));
            return ParseProjects(File.ReadAllText(jsonPath), excluded);
        }
        catch (Exception)
        {
            // Unreadable file, bad path, ...: the page shows "No projects found".
            return [];
        }
    }

    /// <summary>
    /// Parses the contents of Unity Hub's projects-v1.json into a list of projects.
    /// Each entry is parsed independently: a single malformed or incomplete entry
    /// (e.g. a dead reference to a moved/deleted project) is skipped rather than
    /// aborting the whole list. Paths in <paramref name="excluded"/> are dropped.
    /// </summary>
    internal static List<UnityProject> ParseProjects(
        string jsonContent,
        HashSet<string>? excluded = null
    )
    {
        var result = new List<UnityProject>();

        JsonElement projectsRoot;
        try
        {
            projectsRoot = JsonDocument.Parse(jsonContent).RootElement;
        }
        catch (JsonException)
        {
            return result;
        }

        if (
            projectsRoot.ValueKind != JsonValueKind.Object
            || !projectsRoot.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
        )
        {
            return result;
        }

        foreach (var property in data.EnumerateObject())
        {
            if (excluded is not null && excluded.Contains(NormalizePath(property.Name)))
            {
                continue;
            }

            var project = TryParseEntry(property.Name, property.Value);
            if (project is not null)
            {
                result.Add(project);
            }
        }

        return result;
    }

    /// <summary>Parses hub.db rows (path, data JSON); a malformed row is skipped.</summary>
    internal static List<UnityProject> ParseDbRows(IEnumerable<KeyValuePair<string, string>> rows)
    {
        var result = new List<UnityProject>();
        foreach (var (path, data) in rows)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var project = TryParseEntry(path, doc.RootElement);
                if (project is not null)
                {
                    result.Add(project with { FromHubDb = true });
                }
            }
            catch (Exception)
            {
                // Skip only this row.
            }
        }

        return result;
    }

    internal static HashSet<string> ReadExcluded(string excludedJsonPath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(excludedJsonPath))
            {
                return result;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(excludedJsonPath));
            if (
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        result.Add(NormalizePath(item.GetString()!));
                    }
                }
            }
        }
        catch (Exception)
        {
            // Unreadable exclusions: show everything rather than nothing.
        }

        return result;
    }

    private static string NormalizePath(string path) => path.Replace('/', '\\').TrimEnd('\\');

    private static UnityProject? TryParseEntry(string projectPath, JsonElement projectInfo)
    {
        try
        {
            if (projectInfo.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

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

            var changeset = GetOptionalString(projectInfo, "changeset");

            // lastModified is null for projects never opened.
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

            var tags = new List<string>();
            if (
                projectInfo.TryGetProperty("tags", out var tagsEl)
                && tagsEl.ValueKind == JsonValueKind.Array
            )
            {
                foreach (var tag in tagsEl.EnumerateArray())
                {
                    if (
                        tag.ValueKind == JsonValueKind.String
                        && !string.IsNullOrEmpty(tag.GetString())
                    )
                    {
                        tags.Add(tag.GetString()!);
                    }
                }
            }

            return new UnityProject
            {
                Path = projectPath,
                Title = title,
                Version = version,
                Changeset = changeset,
                LastModified = lastModified,
                IsFavorite = isFavorite,
                Tags = tags,
                RenderPipeline = GetOptionalString(projectInfo, "renderPipeline"),
                BuildTarget = GetOptionalString(projectInfo, "buildTarget"),
                VcsProvider = GetOptionalString(projectInfo, "vcsProvider"),
                GitOrganization = GetOptionalString(projectInfo, "organizationName"),
                GitRepository = GetOptionalString(projectInfo, "repositoryName"),
                Exists = SafeDirectoryExists(projectPath),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
    {
        if (
            element.TryGetProperty(propertyName, out var valueEl)
            && valueEl.ValueKind == JsonValueKind.String
        )
        {
            return valueEl.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    // Hub 4 never writes projects-v1.json after migration, so a JSON newer than hub.db
    // (and its WAL) means Hub 3 is in use again.
    private static bool IsJsonNewer(string jsonPath, string dbPath)
    {
        if (!File.Exists(jsonPath))
        {
            return false;
        }

        var dbTime = File.GetLastWriteTimeUtc(dbPath);
        var walPath = dbPath + "-wal";
        if (File.Exists(walPath))
        {
            var walTime = File.GetLastWriteTimeUtc(walPath);
            if (walTime > dbTime)
            {
                dbTime = walTime;
            }
        }

        return File.GetLastWriteTimeUtc(jsonPath) > dbTime;
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
