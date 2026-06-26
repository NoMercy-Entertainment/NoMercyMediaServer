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

namespace NoMercy.OpticalMedia.Live;

/// <summary>
/// Tracks the live HLS session id that is currently active for each optical
/// drive path. Used by OpticalMediaController to map a drive path to the
/// session id so StopMedia can tear down the right session.
/// </summary>
public interface IDiscSessionRegistry
{
    void Register(string drivePath, string sessionId);
    bool TryGet(string drivePath, out string sessionId);
    void Remove(string drivePath);
}
