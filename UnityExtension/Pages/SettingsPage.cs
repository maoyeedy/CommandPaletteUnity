using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

/// <summary>
/// Exposes our settings page through provider.Settings, so CmdPal shows it as the extension's
/// real settings page rather than a second top-level command.
/// </summary>
internal sealed partial class ExtensionSettings : ICommandSettings
{
    public ExtensionSettings(SettingsManager settingsManager)
    {
        SettingsPage = new SettingsPage(settingsManager);
    }

    public IContentPage SettingsPage { get; }
}

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = new IconInfo("\uE713");
        Title = "Settings";
        Name = "Settings";
        _settingsManager.Settings.SettingsChanged += (_, _) => RaiseItemsChanged();
    }

    public override IContent[] GetContent() =>
        [.. _settingsManager.Settings.ToContent(), new AddProjectsForm(), new DangerZoneForm()];

    private sealed partial class AddProjectsForm : FormContent
    {
        private IReadOnlyList<UnityProjectCandidate> _candidates = [];
        private string? _reviewSummary;
        private bool _allSelected = true;

        public AddProjectsForm() => TemplateJson = BuildIdleTemplate();

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            var action = ReadAction(data);
            switch (action)
            {
                case "pickFolders":
                    return HandleDiscover(pickMultiple: true);
                case "scanFolder":
                    return HandleDiscover(pickMultiple: false);
                case "selectAll":
                    _allSelected = true;
                    TemplateJson = BuildReviewTemplate();
                    return CommandResult.KeepOpen();
                case "deselectAll":
                    _allSelected = false;
                    TemplateJson = BuildReviewTemplate();
                    return CommandResult.KeepOpen();
                case "cancelAdd":
                    _candidates = [];
                    _reviewSummary = null;
                    TemplateJson = BuildIdleTemplate();
                    return CommandResult.KeepOpen();
                case "confirmAdd":
                    return HandleConfirmAdd(inputs);
                default:
                    return CommandResult.KeepOpen();
            }
        }

        private CommandResult HandleDiscover(bool pickMultiple)
        {
            var dbPath = Path.Combine(Resources.UnityHubDir, "hub.db");
            if (HubDatabase.TryReadProjects(dbPath, out var existingRows) != HubDbStatus.Ok)
            {
                TemplateJson = BuildIdleTemplate("Unity Hub v4 (hub.db) not found.", isError: true);
                return CommandResult.KeepOpen();
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in existingRows)
            {
                var normalized = ProjectScanner.NormalizePath(row.Key);
                if (!string.IsNullOrEmpty(normalized))
                {
                    existingPaths.Add(normalized);
                }
            }

            ProjectScanResult result;
            if (pickMultiple)
            {
                var picked = FolderPickerHelper.PickFolders("Select Unity project folders");
                if (picked.Count == 0)
                {
                    return CommandResult.KeepOpen();
                }

                result = ProjectScanner.InspectFolders(picked, existingPaths);
            }
            else
            {
                var root = FolderPickerHelper.PickFolder(
                    "Select folder to scan for Unity projects"
                );
                if (string.IsNullOrEmpty(root))
                {
                    return CommandResult.KeepOpen();
                }

                result = ProjectScanner.ScanRecursively(root, existingPaths);
            }

            if (result.Candidates.Count == 0)
            {
                TemplateJson = BuildIdleTemplate(FormatEmptyScanMessage(result, pickMultiple));
                return CommandResult.KeepOpen();
            }

            _candidates = result.Candidates;
            _reviewSummary = FormatReviewSummary(result);
            _allSelected = pickMultiple || result.Candidates.Count <= 15;
            TemplateJson = BuildReviewTemplate();
            return CommandResult.KeepOpen();
        }

        private CommandResult HandleConfirmAdd(string inputs)
        {
            var selected = ReadSelectedCandidates(inputs);
            if (selected.Count == 0)
            {
                return CommandResult.ShowToast(
                    new ToastArgs
                    {
                        Message = "Select at least one project to add.",
                        Result = CommandResult.KeepOpen(),
                    }
                );
            }

            var dbPath = Path.Combine(Resources.UnityHubDir, "hub.db");
            var added = HubDatabase.TryAddProjects(dbPath, selected);

            _candidates = [];
            _reviewSummary = null;

            string message;
            bool isError;
            if (added > 0)
            {
                message =
                    added == 1
                        ? "Added 1 project to Unity Hub."
                        : $"Added {added} projects to Unity Hub.";
                isError = false;
            }
            else
            {
                message =
                    "Nothing added. Unity Hub may be busy, or your Hub version is older than v4.";
                isError = true;
            }

            TemplateJson = BuildIdleTemplate(message, isError);
            return CommandResult.ShowToast(
                new ToastArgs { Message = message, Result = CommandResult.KeepOpen() }
            );
        }

        private List<UnityProjectCandidate> ReadSelectedCandidates(string inputs)
        {
            var selected = new List<UnityProjectCandidate>();
            if (string.IsNullOrWhiteSpace(inputs) || _candidates.Count == 0)
            {
                return selected;
            }

            try
            {
                using var doc = JsonDocument.Parse(inputs);
                if (
                    doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("selected", out var selectedEl)
                    || selectedEl.ValueKind != JsonValueKind.String
                )
                {
                    return selected;
                }

                var raw = selectedEl.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return selected;
                }

                var seen = new HashSet<int>();
                foreach (var token in raw.Split(','))
                {
                    if (
                        int.TryParse(
                            token.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var index
                        )
                        && index >= 0
                        && index < _candidates.Count
                        && seen.Add(index)
                    )
                    {
                        selected.Add(_candidates[index]);
                    }
                }
            }
            catch (JsonException)
            {
                return [];
            }

            return selected;
        }

        private static string? ReadAction(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (
                    doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("action", out var actionEl)
                    && actionEl.ValueKind == JsonValueKind.String
                )
                {
                    return actionEl.GetString();
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        private static string FormatEmptyScanMessage(ProjectScanResult result, bool pickMultiple)
        {
            if (result.AlreadyInHubCount > 0 && result.InvalidFolderCount > 0)
            {
                return $"No new Unity projects found ({result.AlreadyInHubCount} already in Unity Hub, {result.InvalidFolderCount} not Unity projects).";
            }

            if (result.AlreadyInHubCount > 0)
            {
                return $"No new Unity projects found ({result.AlreadyInHubCount} already in Unity Hub).";
            }

            if (result.InvalidFolderCount > 0)
            {
                return "No Unity projects found in the selected folders (missing Assets/ or ProjectSettings/ProjectVersion.txt).";
            }

            return pickMultiple
                ? "No Unity projects found in the selected folders."
                : "No Unity projects found in that folder.";
        }

        private static string? FormatReviewSummary(ProjectScanResult result)
        {
            var parts = new List<string>();
            if (result.AlreadyInHubCount > 0)
            {
                parts.Add($"{result.AlreadyInHubCount} already in Unity Hub");
            }

            if (result.InvalidFolderCount > 0)
            {
                parts.Add($"{result.InvalidFolderCount} skipped (not Unity projects)");
            }

            if (result.Truncated)
            {
                parts.Add($"showing first {result.Candidates.Count}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static string BuildIdleTemplate(string? statusMessage = null, bool isError = false)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("$schema", "http://adaptivecards.io/schemas/adaptive-card.json");
                writer.WriteString("type", "AdaptiveCard");
                writer.WriteString("version", "1.5");

                writer.WriteStartArray("body");
                WriteTextBlock(
                    writer,
                    "Add projects to Unity Hub",
                    size: "large",
                    weight: "bolder"
                );
                WriteTextBlock(
                    writer,
                    "Pick project folders or scan a parent folder for Unity Hub v4. Nothing is added until you confirm.",
                    isSubtle: true,
                    spacing: "small"
                );

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    WriteTextBlock(
                        writer,
                        statusMessage,
                        color: isError ? "attention" : "good",
                        spacing: "small"
                    );
                }

                writer.WriteEndArray();

                writer.WriteStartArray("actions");
                WriteSubmitAction(writer, "Select folders...", "pickFolders");
                WriteSubmitAction(writer, "Scan folder...", "scanFolder");
                writer.WriteEndArray();

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private string BuildReviewTemplate()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("$schema", "http://adaptivecards.io/schemas/adaptive-card.json");
                writer.WriteString("type", "AdaptiveCard");
                writer.WriteString("version", "1.5");

                writer.WriteStartArray("body");
                var heading =
                    _candidates.Count == 1
                        ? "Found 1 new Unity project"
                        : $"Found {_candidates.Count} new Unity projects";
                WriteTextBlock(writer, heading, size: "large", weight: "bolder");

                if (!string.IsNullOrEmpty(_reviewSummary))
                {
                    WriteTextBlock(writer, _reviewSummary, isSubtle: true, spacing: "small");
                }

                writer.WriteStartObject();
                writer.WriteString("type", "Input.ChoiceSet");
                writer.WriteString("id", "selected");
                writer.WriteString("style", "expanded");
                writer.WriteBoolean("isMultiSelect", true);
                writer.WriteString(
                    "value",
                    _allSelected
                        ? string.Join(
                            ",",
                            Enumerable
                                .Range(0, _candidates.Count)
                                .Select(i => i.ToString(CultureInfo.InvariantCulture))
                        )
                        : string.Empty
                );

                writer.WriteStartArray("choices");
                for (var i = 0; i < _candidates.Count; i++)
                {
                    var candidate = _candidates[i];
                    writer.WriteStartObject();
                    writer.WriteString(
                        "title",
                        $"{candidate.Title} ({candidate.Version}) - {UnityExtensionPage.ShortenHomePath(candidate.Path)}"
                    );
                    writer.WriteString("value", i.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                writer.WriteEndArray();

                writer.WriteStartArray("actions");
                WriteSubmitAction(writer, "Cancel", "cancelAdd");
                WriteSubmitAction(
                    writer,
                    _allSelected ? "Deselect all" : "Select all",
                    _allSelected ? "deselectAll" : "selectAll"
                );
                WriteSubmitAction(writer, "Add selected", "confirmAdd");
                writer.WriteEndArray();

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteTextBlock(
            Utf8JsonWriter writer,
            string text,
            string? size = null,
            string? weight = null,
            string? color = null,
            bool isSubtle = false,
            string? spacing = null
        )
        {
            writer.WriteStartObject();
            writer.WriteString("type", "TextBlock");
            writer.WriteString("text", text);
            writer.WriteBoolean("wrap", true);
            if (size is not null)
            {
                writer.WriteString("size", size);
            }

            if (weight is not null)
            {
                writer.WriteString("weight", weight);
            }

            if (color is not null)
            {
                writer.WriteString("color", color);
            }

            if (isSubtle)
            {
                writer.WriteBoolean("isSubtle", true);
            }

            if (spacing is not null)
            {
                writer.WriteString("spacing", spacing);
            }

            writer.WriteEndObject();
        }

        private static void WriteSubmitAction(Utf8JsonWriter writer, string title, string action)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "Action.Submit");
            writer.WriteString("title", title);
            writer.WriteStartObject("data");
            writer.WriteString("action", action);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    private sealed partial class DangerZoneForm : FormContent
    {
        // CommandResult.Confirm is a ContentDialog owned by the palette window, so it cannot show
        // in the separate settings window. Confirm inline instead: assigning TemplateJson raises a
        // property change and the host re-renders the card in place.
        private const string IdleTemplate = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "TextBlock",
                  "text": "Danger zone",
                  "size": "large",
                  "weight": "bolder",
                  "wrap": true
                },
                {
                  "type": "TextBlock",
                  "text": "Clears the Unity Hub v4 project list. No files or folders are deleted.",
                  "wrap": true,
                  "isSubtle": true,
                  "spacing": "small"
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Remove all projects from Unity Hub",
                  "data": { "action": "removeAll" }
                }
              ]
            }
            """;

        private const string ConfirmTemplate = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "TextBlock",
                  "text": "Remove all projects?",
                  "size": "large",
                  "weight": "bolder",
                  "wrap": true
                },
                {
                  "type": "TextBlock",
                  "text": "Clears all projects from the Unity Hub v4 project list. No files or folders are deleted.",
                  "color": "attention",
                  "wrap": true,
                  "spacing": "small"
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Cancel",
                  "data": { "action": "cancelRemoveAll" }
                },
                {
                  "type": "Action.Submit",
                  "title": "Remove all projects",
                  "data": { "action": "confirmRemoveAll" }
                }
              ]
            }
            """;

        public DangerZoneForm() => TemplateJson = IdleTemplate;

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            // Check the specific actions first: both contain "removeAll" as a substring.
            if (data.Contains("cancelRemoveAll", StringComparison.Ordinal))
            {
                TemplateJson = IdleTemplate;
            }
            else if (data.Contains("confirmRemoveAll", StringComparison.Ordinal))
            {
                TemplateJson = IdleTemplate;
                return new RemoveAllProjectsCommand().Invoke();
            }
            else if (data.Contains("removeAll", StringComparison.Ordinal))
            {
                TemplateJson = ConfirmTemplate;
            }

            return CommandResult.KeepOpen();
        }
    }
}
