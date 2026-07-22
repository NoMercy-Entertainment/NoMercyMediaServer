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

namespace NoMercy.OpticalMedia.Metadata;

/// <summary>
/// Stub <see cref="ITocReader"/> that always returns null.
/// Used until a native TOC reader is available for the target platform.
/// When null is returned, <see cref="AudioCdIdentifier"/> falls back to
/// NeedsManualAssignment.
/// </summary>
// TODO follow-up: native TOC read (Linux ioctl CDROMREADTOCENTRY, macOS IOKit)
public sealed class NullTocReader : ITocReader
{
    public Task<DiscToc?> ReadTocAsync(string drivePath, CancellationToken ct) =>
        Task.FromResult<DiscToc?>(result: null);
}
