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
        public DangerZoneForm()
        {
            // One card keeps the heading, explanation and button aligned with the settings card
            // above it. The default action style makes the button look like its Save button; the
            // danger is signalled at the point of commitment by the critical confirm dialog.
            TemplateJson = """
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
                      "text": "Clears every entry from Hub 4's project list, including ones still on disk. Your project files stay untouched.",
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
        }

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            if (
                !inputs.Contains("removeAll", StringComparison.Ordinal)
                && !data.Contains("removeAll", StringComparison.Ordinal)
            )
            {
                return CommandResult.KeepOpen();
            }

            return new RemoveAllProjectsCommand().Invoke();
        }
    }
}
