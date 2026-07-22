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

using Microsoft.Extensions.Logging;

namespace NoMercy.NmSystem.Logging.Rendering;

/// <summary>The two console colour themes. Dark is the default.</summary>
public enum NoMercyConsoleTheme
{
    Dark,
    Light,
}

/// <summary>
/// Theme colours for the console renderer: message token colours, the dim colour
/// (timestamp, gutter, scope), per-category colours and per-level severity markers.
/// </summary>
public static class ConsoleThemeColors
{
    public static string Text(NoMercyConsoleTheme theme) =>
        theme == NoMercyConsoleTheme.Dark ? "#cdd6f4" : "#4c4f69";

    public static string Dim(NoMercyConsoleTheme theme) =>
        theme == NoMercyConsoleTheme.Dark ? "#6c7086" : "#9ca0b0";

    public static string Number(NoMercyConsoleTheme theme) =>
        theme == NoMercyConsoleTheme.Dark ? "#f5c2e7" : "#8839ef";

    public static string Str(NoMercyConsoleTheme theme) =>
        theme == NoMercyConsoleTheme.Dark ? "#a6e3a1" : "#40a02b";

    public static string Category(LogCategory category, NoMercyConsoleTheme theme) =>
        theme == NoMercyConsoleTheme.Dark ? category.DarkHex : category.LightHex;

    /// <summary>The severity marker glyph and its colour for a level. Info/Debug have no glyph.</summary>
    public static (string Marker, string Hex) Level(LogLevel level, NoMercyConsoleTheme theme)
    {
        return level switch
        {
            LogLevel.Critical => ("×", Category(category: LogCategories.Resolve(key: "fatal"), theme: theme)),
            LogLevel.Error => ("×", Category(category: LogCategories.Resolve(key: "error"), theme: theme)),
            LogLevel.Warning => ("!", Category(category: LogCategories.Resolve(key: "warning"), theme: theme)),
            _ => (" ", Category(category: LogCategories.Resolve(key: "info"), theme: theme)),
        };
    }
}
