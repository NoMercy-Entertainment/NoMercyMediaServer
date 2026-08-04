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

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Where a already-registered video file was last seen: the library folder it
/// belongs to (<paramref name="Share"/>, a <c>Folders.Id</c>) plus the stored
/// path pair. Enough to ask the storage facade whether that media is still
/// readable, without handing an EF entity to the caller.
/// </summary>
public sealed record RecordedVideoFileLocation(string Share, string HostFolder, string Filename);
