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

public static class PluginLifecycle
{
    private static readonly Dictionary<PluginStatus, HashSet<PluginStatus>> AllowedTransitions =
        new()
        {
            [key: PluginStatus.Active] =
            [
                PluginStatus.Disabled,
                PluginStatus.Malfunctioned,
                PluginStatus.Deleted,
            ],
            [key: PluginStatus.Disabled] = [PluginStatus.Active, PluginStatus.Deleted],
            [key: PluginStatus.Malfunctioned] =
            [
                PluginStatus.Active,
                PluginStatus.Disabled,
                PluginStatus.Deleted,
            ],
            [key: PluginStatus.Deleted] = [],
        };

    public static bool CanTransition(PluginStatus from, PluginStatus to)
    {
        if (AllowedTransitions.TryGetValue(key: from, value: out HashSet<PluginStatus>? allowed))
        {
            return allowed.Contains(item: to);
        }

        return false;
    }

    public static void Transition(PluginInfo info, PluginStatus newStatus)
    {
        ArgumentNullException.ThrowIfNull(argument: info);

        if (!CanTransition(from: info.Status, to: newStatus))
        {
            throw new InvalidOperationException(
                message: $"Cannot transition plugin '{info.Name}' from {info.Status} to {newStatus}."
            );
        }

        info.Status = newStatus;
    }
}
