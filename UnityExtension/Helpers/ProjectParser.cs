using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityExtension;

public static class ProjectParser
{
    internal static List<UnityProject> GetUnityProjects()
    {
        var result = new List<UnityProject>();
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var projectsJsonFilePath = Path.Combine(appDataPath, Resources.ProjectsJsonPath);

        if (!File.Exists(projectsJsonFilePath))
        {
            return result;
        }

        try
        {
            var jsonContent = File.ReadAllText(projectsJsonFilePath);
            var projectsRoot = JsonDocument.Parse(jsonContent).RootElement;

            if (projectsRoot.TryGetProperty("data", out var data))
            {
                foreach (var property in data.EnumerateObject())
                {
                    var projectPath = property.Name;
                    var projectInfo = property.Value;

                    var project = new UnityProject
                    {
                        Path = projectPath,
                        Title =
                            projectInfo.GetProperty("title").GetString()
                            ?? Path.GetFileName(projectPath),
                        Version = projectInfo.GetProperty("version").GetString() ?? "Unknown",
                        LastModified = projectInfo.GetProperty("lastModified").GetInt64(),
                        IsFavorite =
                            projectInfo.TryGetProperty("isFavorite", out var isFavorite)
                            && isFavorite.GetBoolean(),
                    };

                    result.Add(project);
                }
            }
        }
        catch (Exception)
        {
            // Any error in parsing will result in an empty list
        }

        return result;
    }
}
