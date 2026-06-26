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
/// OS-specific source of drive state and insert/eject events.
/// <see cref="DriveMonitor"/> picks one at DI time:
/// WMI on Windows where available, polling fallback otherwise.
/// </summary>
public interface IDriveBackend : IAsyncDisposable
{
    IReadOnlyList<DiscDrive> GetDrives();
    IAsyncEnumerable<DriveEvent> ListenAsync(CancellationToken ct);
}
