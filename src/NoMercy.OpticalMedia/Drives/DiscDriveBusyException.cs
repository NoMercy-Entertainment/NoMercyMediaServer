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

using NoMercy.Encoder.Errors;

namespace NoMercy.OpticalMedia.Drives;

/// <summary>
/// Thrown by <see cref="DiscRipper"/> when a rip is requested for a drive
/// that already has an active rip in progress. Callers (API controllers, job
/// runners) should surface this as HTTP 409 with rule id
/// <see cref="EncoderRuleId.DiscDriveBusy"/>.
/// </summary>
public sealed class DiscDriveBusyException(string driveKey)
    : InvalidOperationException(
        $"Drive '{driveKey}' is already being ripped. "
            + "Wait for the in-progress rip to finish or eject and re-insert the disc into a different drive."
    )
{
    /// <summary>
    /// The lock key (volume UUID or device path) of the busy drive.
    /// </summary>
    public string DriveKey { get; } = driveKey;

    /// <summary>
    /// Stable rule ID for the dashboard chip renderer and docs deep-linking.
    /// </summary>
    public string RuleId { get; } = EncoderRuleId.DiscDriveBusy;
}
