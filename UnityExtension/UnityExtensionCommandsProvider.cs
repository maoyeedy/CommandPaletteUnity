// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace UnityExtension;

public partial class UnityExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly SettingsManager _settingsManager = new();
    private readonly UnityExtensionPage _page;

    public UnityExtensionCommandsProvider()
    {
        DisplayName = "Unity Projects";
        Icon = Resources.IconUnity;

        Settings = new ExtensionSettings(_settingsManager);
        _page = new UnityExtensionPage(_settingsManager);
        _commands = [new CommandItem(_page) { Title = DisplayName }];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override void Dispose()
    {
        _page.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
