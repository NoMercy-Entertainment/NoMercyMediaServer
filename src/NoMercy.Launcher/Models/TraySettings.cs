// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;

namespace NoMercy.Launcher.Models;

public class TraySettings
{
    [JsonProperty("show_on_startup")]
    public bool ShowOnStartup { get; set; }

    [JsonProperty("startup_arguments")]
    public string StartupArguments { get; set; } = string.Empty;

    /// <summary>
    /// When true the server is automatically started when the launcher opens.
    /// The installer update path reads this to decide whether to relaunch
    /// the launcher after a silent install.
    /// </summary>
    [JsonProperty("auto_start")]
    public bool AutoStart { get; set; }
}
