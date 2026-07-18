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

public static class PluginHookCapability
{
    public const string MediaSource = "mediaSource";
    public const string Metadata = "metadata";
    public const string ScheduledTask = "scheduledTask";
    public const string Auth = "auth";
    public const string Encoder = "encoder";
    public const string Ui = "ui";
}
