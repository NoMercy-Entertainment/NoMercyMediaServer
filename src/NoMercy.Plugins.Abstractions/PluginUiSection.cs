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

namespace NoMercy.Plugins.Abstractions;

/// <summary>
/// Where a plugin's UI mount appears.
/// <para>
/// <see cref="PluginUiMount.Section"/> was a required string with no stated
/// values, so an author had to guess and guessing wrong was silent — the mount
/// rendered nowhere, or somewhere unintended, and neither is an error anyone
/// sees. Naming the values makes a typo a compile error instead.
/// </para>
/// <para>
/// An unknown section is NOT rejected. A client that does not know a section
/// falls back to <see cref="Tools"/>, because the alternative is that adding a
/// section to one client turns every plugin using it into a validation failure
/// on the other. Every section here needs a home in both the web sidebar and
/// the KMP navigation; a section one client knows and the other does not is the
/// same silent drift in a new place.
/// </para>
/// </summary>
public static class PluginUiSection
{
    /// <summary>Alongside the music library.</summary>
    public const string Music = "music";

    /// <summary>Alongside films.</summary>
    public const string Movies = "movies";

    /// <summary>Alongside shows.</summary>
    public const string Shows = "shows";

    /// <summary>
    /// Utilities that are not tied to one media type — a downloader, an
    /// importer, a maintenance panel. The fallback for an unrecognised section.
    /// </summary>
    public const string Tools = "tools";

    /// <summary>Server administration, beside the other owner-only panels.</summary>
    public const string Dashboard = "dashboard";

    /// <summary>The plugin's own page under the plugin settings list.</summary>
    public const string Settings = "settings";

    /// <summary>The sections every client is expected to render.</summary>
    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Music,
            Movies,
            Shows,
            Tools,
            Dashboard,
            Settings,
        };

    /// <summary>
    /// <paramref name="section"/> when a client knows it, otherwise
    /// <see cref="Tools"/>. Renders the mount somewhere real instead of
    /// dropping it.
    /// </summary>
    public static string OrFallback(string? section) =>
        section is not null && All.Contains(section) ? section : Tools;
}
