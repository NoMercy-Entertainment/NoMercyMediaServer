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

namespace NoMercy.Events.DriveMonitor;

/// <summary>
/// Typed payload for drive-monitor state changes broadcast on
/// <c>ripperHub</c> / <c>drivesHub</c> via <see cref="DriveStateChangedEvent"/>.
/// </summary>
/// <param name="Method">
/// One of: <c>drive_added</c>, <c>drive_removed</c>,
/// <c>disc_inserted</c>, <c>disc_ejected</c>, <c>drive_changed</c>,
/// <c>rip_started</c>, <c>rip_progress</c>, <c>rip_complete</c>, <c>rip_error</c>,
/// <c>rip_pending</c>.
/// </param>
/// <param name="Drive">OS device path of the drive (e.g. <c>D:\</c> or <c>/dev/sr0</c>).</param>
/// <param name="VolumeLabel">Volume label of the inserted disc, or <c>null</c> when no disc.</param>
/// <param name="HasDisc">Whether a disc is currently in the drive.</param>
/// <param name="DiscType">Lower-case disc type string: <c>bluray</c>, <c>dvd</c>, <c>cd</c>, or <c>none</c>.</param>
/// <param name="Timestamp">UTC timestamp of the event.</param>
/// <param name="JobId">Rip-job identifier — populated only for rip-progress methods, otherwise <c>null</c>.</param>
/// <param name="Message">Human-readable detail — populated for error / pending / progress methods, otherwise <c>null</c>.</param>
public record DriveStatePayload(
    string Method,
    string Drive,
    string? VolumeLabel,
    bool HasDisc,
    string DiscType,
    DateTime Timestamp,
    string? JobId = null,
    string? Message = null
);
