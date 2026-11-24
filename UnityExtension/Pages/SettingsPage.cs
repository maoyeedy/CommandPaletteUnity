using System;
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
        [.. _settingsManager.Settings.ToContent(), new DangerZoneForm()];

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
                  "text": "Clears Hub 4's project list. No files or folders are deleted.",
                  "wrap": true,
                  "isSubtle": true,
                  "spacing": "small"
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Remove all projects from Unity Hub list",
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
                  "text": "Clears all projects from Hub's list. No files or folders are deleted.",
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
