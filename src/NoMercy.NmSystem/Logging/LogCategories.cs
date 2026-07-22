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

using System.Globalization;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// Registry of console log categories with their dark/light colours. Curated
/// colours cover the level/system/worker/networking groups; the provider group is
/// coloured from an evenly-spaced, dark- and light-legible hue ramp so every
/// provider (TheMovieDB, MusicBrainz, TheTVDB, ...) is visually distinct.
/// <see cref="ResolveSource"/> maps a logger's source context (namespace/type) to a
/// category so call sites never pass a category by hand.
/// </summary>
public static class LogCategories
{
    private static readonly string[] ProviderKeys =
    {
        "youtube",
        "acoustid",
        "anidb",
        "audiodb",
        "coverart",
        "fanart",
        "fingerprint",
        "lrclib",
        "moviedb",
        "musicbrainz",
        "musixmatch",
        "opensubs",
        "tvdb",
    };

    private static readonly Dictionary<string, string> ProviderDisplayNames = new()
    {
        [key: "youtube"] = "YouTube",
        [key: "acoustid"] = "AcoustID",
        [key: "anidb"] = "AniDB",
        [key: "audiodb"] = "AudioDB",
        [key: "coverart"] = "CoverArt",
        [key: "fanart"] = "Fanart",
        [key: "fingerprint"] = "Fingerprint",
        [key: "lrclib"] = "Lrclib",
        [key: "moviedb"] = "TheMovieDB",
        [key: "musicbrainz"] = "MusicBrainz",
        [key: "musixmatch"] = "MusixMatch",
        [key: "opensubs"] = "OpenSubs",
        [key: "tvdb"] = "TheTVDB",
    };

    // Ordered longest/most-specific first so "moviedb" wins before "movie", etc.
    private static readonly (string Fragment, string Key)[] SourceFragments =
    {
        ("moviedb", "moviedb"),
        ("tmdb", "moviedb"),
        ("musicbrainz", "musicbrainz"),
        ("musixmatch", "musixmatch"),
        ("audiodb", "audiodb"),
        ("tadb", "audiodb"),
        ("coverart", "coverart"),
        ("acoustid", "acoustid"),
        ("anidb", "anidb"),
        ("lrclib", "lrclib"),
        ("fanart", "fanart"),
        ("fingerprint", "fingerprint"),
        ("opensub", "opensubs"),
        ("youtube", "youtube"),
        ("tvdb", "tvdb"),
        ("encoder", "encoder"),
        ("queue", "queue"),
        ("ripper", "ripper"),
        ("optical", "ripper"),
        ("certificate", "certificate"),
        ("registration", "register"),
        ("setup", "setup"),
        ("auth", "auth"),
        ("socket", "socket"),
        ("hub", "socket"),
        ("notify", "notify"),
        ("http", "http"),
        ("seed", "system"),
        ("configuration", "configuration"),
    };

    private static readonly Dictionary<string, LogCategory> Map = Build();

    /// <summary>The fallback category used when nothing else matches.</summary>
    public static LogCategory Default => Map[key: "app"];

    /// <summary>Resolves a category by its key, falling back to <see cref="Default"/>.</summary>
    public static LogCategory Resolve(string? key)
    {
        if (
            !string.IsNullOrEmpty(value: key)
            && Map.TryGetValue(key: key.ToLowerInvariant(), value: out LogCategory? category)
        )
            return category;
        return Default;
    }

    /// <summary>
    /// Maps a logger source context (e.g. a full type name) to a category by scanning
    /// for known subsystem fragments. Falls back to <see cref="Default"/>.
    /// </summary>
    public static LogCategory ResolveSource(string? sourceContext)
    {
        if (string.IsNullOrEmpty(value: sourceContext))
            return Default;

        string haystack = sourceContext.ToLowerInvariant();
        foreach ((string fragment, string key) in SourceFragments)
        {
            if (haystack.Contains(value: fragment, comparisonType: StringComparison.Ordinal))
                return Map[key: key];
        }

        return Default;
    }

    private static Dictionary<string, LogCategory> Build()
    {
        Dictionary<string, LogCategory> map = new(comparer: StringComparer.OrdinalIgnoreCase);

        void Add(string key, string display, string group, string dark, string light) =>
            map[key: key] = new(Key: key, DisplayName: display, Group: group, DarkHex: dark, LightHex: light);

        // Levels
        Add(key: "debug", display: "Debug", group: "Level", dark: "#6c7086", light: "#8c8fa1");
        Add(key: "verbose", display: "Verbose", group: "Level", dark: "#585b70", light: "#9ca0b0");
        Add(key: "info", display: "Info", group: "Level", dark: "#cdd6f4", light: "#4c4f69");
        Add(key: "warning", display: "Warning", group: "Level", dark: "#f9e2af", light: "#df8e1d");
        Add(key: "error", display: "Error", group: "Level", dark: "#f38ba8", light: "#d20f39");
        Add(key: "fatal", display: "Fatal", group: "Level", dark: "#ff5370", light: "#e64553");

        // System
        Add(key: "app", display: "App", group: "System", dark: "#b4befe", light: "#7287fd");
        Add(key: "access", display: "Access", group: "System", dark: "#cba6f7", light: "#8839ef");
        Add(key: "configuration", display: "Configuration", group: "System", dark: "#89b4fa", light: "#1e66f5");
        Add(key: "setup", display: "Setup", group: "System", dark: "#74c7ec", light: "#209fb5");
        Add(key: "system", display: "System", group: "System", dark: "#89dceb", light: "#04a5e5");
        Add(key: "service", display: "Service", group: "System", dark: "#94e2d5", light: "#179299");
        Add(key: "auth", display: "Auth", group: "System", dark: "#f5c2e7", light: "#ea76cb");
        Add(key: "register", display: "Register", group: "System", dark: "#f2cdcd", light: "#dd7878");
        Add(key: "certificate", display: "Certificate", group: "System", dark: "#f5e0dc", light: "#dc8a78");

        // Workers
        Add(key: "queue", display: "Queue", group: "Workers", dark: "#fab387", light: "#fe640b");
        Add(key: "encoder", display: "Encoder", group: "Workers", dark: "#eba0ac", light: "#e64553");
        Add(key: "ripper", display: "Ripper", group: "Workers", dark: "#e5b0c5", light: "#b4377f");

        // Networking
        Add(key: "http", display: "Http", group: "Networking", dark: "#a6e3a1", light: "#40a02b");
        Add(key: "notify", display: "Notify", group: "Networking", dark: "#b5e8a0", light: "#5a9e1f");
        Add(key: "ping", display: "Ping", group: "Networking", dark: "#94e2d5", light: "#0a7fa8");
        Add(key: "socket", display: "Socket", group: "Networking", dark: "#7fc8f0", light: "#1e66f5");
        Add(key: "request", display: "Request", group: "Networking", dark: "#89dceb", light: "#04a5e5");

        // Providers (deterministic hue ramp -> matches the approved swatch sheets)
        for (int i = 0; i < ProviderKeys.Length; i++)
        {
            double hue = (double)i / ProviderKeys.Length;
            string dark = Hsl(hue: hue, lightness: 0.72, saturation: 0.55);
            string light = Hsl(hue: hue, lightness: 0.42, saturation: 0.68);
            Add(key: ProviderKeys[i], display: ProviderDisplayNames[key: ProviderKeys[i]], group: "Providers", dark: dark, light: light);
        }

        return map;
    }

    /// <summary>HSL (matching Python colorsys.hls_to_rgb) to a #rrggbb hex string.</summary>
    private static string Hsl(double hue, double lightness, double saturation)
    {
        double m2 =
            lightness <= 0.5
                ? lightness * (1.0 + saturation)
                : lightness + saturation - lightness * saturation;
        double m1 = 2.0 * lightness - m2;

        double r = Channel(m1: m1, m2: m2, hue: hue + 1.0 / 3.0);
        double g = Channel(m1: m1, m2: m2, hue: hue);
        double b = Channel(m1: m1, m2: m2, hue: hue - 1.0 / 3.0);

        return string.Format(
            provider: CultureInfo.InvariantCulture,
            format: "#{0:x2}{1:x2}{2:x2}",
            arg0: (int)Math.Round(a: r * 255.0),
            arg1: (int)Math.Round(a: g * 255.0),
            arg2: (int)Math.Round(a: b * 255.0)
        );
    }

    private static double Channel(double m1, double m2, double hue)
    {
        hue %= 1.0;
        if (hue < 0.0)
            hue += 1.0;

        if (hue < 1.0 / 6.0)
            return m1 + (m2 - m1) * 6.0 * hue;
        if (hue < 0.5)
            return m2;
        if (hue < 2.0 / 3.0)
            return m1 + (m2 - m1) * (2.0 / 3.0 - hue) * 6.0;
        return m1;
    }
}
