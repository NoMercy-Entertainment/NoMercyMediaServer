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

namespace NoMercy.OpticalMedia.Drives;

/// <summary>
/// Watches optical drives for insert/eject and surfaces them as a stream of
/// <see cref="DriveEvent"/>. Singleton: holds the last-seen drive set so it
/// can diff between iterations.
/// </summary>
public interface IDriveMonitor
{
    IReadOnlyList<DiscDrive> GetDrives();
    IAsyncEnumerable<DriveEvent> MonitorAsync(CancellationToken ct);
}
