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
/// What a client does when the user activates a component. Named for the same
/// reason as <see cref="PluginComponentType"/>: an unrecognised type is a
/// silent no-op on both clients.
/// </summary>
public static class PluginActionType
{
    public const string PlayMedia = "playMedia";
    public const string Enqueue = "enqueue";
    public const string Navigate = "navigate";
    public const string CallPlugin = "callPlugin";
    public const string OpenWebView = "openWebView";
    public const string RefreshView = "refreshView";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            PlayMedia,
            Enqueue,
            Navigate,
            CallPlugin,
            OpenWebView,
            RefreshView,
        };
}
