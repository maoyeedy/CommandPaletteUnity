using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = new IconInfo("\uE713");
        Title = "Settings";
        Name = "Settings";
    }

    public override IContent[] GetContent() =>
        [
            .. _settingsManager.Settings.ToContent(),
            new MarkdownContent(
                """
                ## Danger zone

                Unity Hub has no way to clear its whole project history. This removes every entry from
                Hub's project list in one step, including projects that are still on disk. Your project
                folders and files are never touched. Hub 4 only; Hub 3 entries stay untouched.
                """
            ),
            new DangerZoneForm(),
        ];

    private sealed partial class DangerZoneForm : FormContent
    {
        public DangerZoneForm()
        {
            TemplateJson = """
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "body": [
                    {
                      "type": "TextBlock",
                      "text": "Clear years of accumulated entries without reinstalling Unity Hub.",
                      "wrap": true
                    }
                  ],
                  "actions": [
                    {
                      "type": "Action.Submit",
                      "title": "Remove all projects from Unity Hub list",
                      "style": "destructive",
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
