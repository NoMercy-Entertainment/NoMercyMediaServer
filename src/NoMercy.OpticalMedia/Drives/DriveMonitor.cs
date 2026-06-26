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
/// Singleton monitor that wraps the OS-specific <see cref="IDriveBackend"/>
/// and surfaces drive events to the rest of the app. The worker
/// <c>DriveMonitorWorker</c> consumes <see cref="MonitorAsync"/> and
/// republishes events on the application event bus.
/// </summary>
public sealed class DriveMonitor(IDriveBackend backend) : IDriveMonitor
{
    public IReadOnlyList<DiscDrive> GetDrives() => backend.GetDrives();

    public IAsyncEnumerable<DriveEvent> MonitorAsync(CancellationToken ct) =>
        backend.ListenAsync(ct);
}
