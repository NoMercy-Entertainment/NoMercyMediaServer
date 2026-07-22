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
using NoMercy.Launcher.Models;
using NoMercy.NmSystem.Information;

namespace NoMercy.Launcher.Services;

public static class LauncherSettings
{
    private static string SettingsFile => AppFiles.TraySettingsFile;

    public static TraySettings Load()
    {
        try
        {
            if (!File.Exists(path: SettingsFile))
                return new();

            string json = File.ReadAllText(path: SettingsFile);
            return JsonConvert.DeserializeObject<TraySettings>(value: json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void Save(TraySettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path: SettingsFile);
            if (directory is not null && !Directory.Exists(path: directory))
                Directory.CreateDirectory(path: directory);

            string json = JsonConvert.SerializeObject(value: settings, formatting: Formatting.Indented);
            File.WriteAllText(path: SettingsFile, contents: json);
        }
        catch
        {
            // Ignore write failures
        }
    }
}
