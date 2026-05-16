// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

public partial class UnityExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly SettingsManager _settingsManager = new();

    public UnityExtensionCommandsProvider()
    {
        DisplayName = "Unity Projects";
        Icon = Resources.IconUnity;

        Settings = _settingsManager.Settings;
        _commands =
        [
            new CommandItem(new UnityExtensionPage(_settingsManager)) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
