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

public static class PluginAbi
{
    /// <summary>
    /// What this server's plugin contract can do.
    /// <para>
    /// The minor goes up whenever something is <em>added</em> that a plugin can ask for
    /// and an older server cannot answer. A plugin declaring the higher minor is then
    /// refused by the older server with a sentence naming the ABI, which is the whole
    /// point: without the bump it loads, runs, and fails on a member that is not there —
    /// a <c>MissingMethodException</c> from inside plugin code, which reads like the
    /// plugin's bug and is not.
    /// </para>
    /// <para>
    /// 10.1 added <see cref="PluginLibraryShow.Status"/>.
    /// </para>
    /// </summary>
    public static Version Current { get; } = new(10, 1);

    public static bool IsCompatible(string? targetAbi)
    {
        if (string.IsNullOrWhiteSpace(targetAbi))
        {
            return true;
        }

        if (!Version.TryParse(targetAbi, out Version? requested))
        {
            return false;
        }

        return requested.Major == Current.Major && requested.Minor <= Current.Minor;
    }
}
