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

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging.Rendering;

namespace NoMercy.NmSystem.Logging;

/// <summary>Options for the NoMercy console logger.</summary>
public sealed class NoMercyLoggerOptions
{
    /// <summary>Colour theme. Dark by default.</summary>
    public NoMercyConsoleTheme Theme { get; set; } = NoMercyConsoleTheme.Dark;

    /// <summary>Default minimum level when a category has no explicit override.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Per-category (by <see cref="LogCategory.Key"/>) minimum level overrides.</summary>
    public Dictionary<string, LogLevel> CategoryLevels { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Force colour on/off. Null auto-detects (off when redirected or NO_COLOR is set).</summary>
    public bool? Color { get; set; }

    /// <summary>Returns the current wrap width in columns; 0 disables wrapping.</summary>
    public Func<int> WidthProvider { get; set; } = static () => 0;
}
