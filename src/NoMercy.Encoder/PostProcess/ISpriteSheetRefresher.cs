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

using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

/// <summary>
/// Rebuilds the scrub-preview sheet for media that is already encoded, without
/// touching a single video or audio stream.
///
/// <para>It samples the encoded output rather than the release source, because
/// by the time a preview needs upgrading the source is usually long gone. That
/// turns out to be the better input anyway: the encoded rendition is already
/// tone-mapped to SDR and already cropped, so the frames come out matching what
/// a viewer actually sees, with none of the colour handling the encode pipeline
/// needs when it reads an HDR master.</para>
/// </summary>
public interface ISpriteSheetRefresher
{
    /// <summary>
    /// Renders a sheet at <paramref name="tileWidth"/> into
    /// <paramref name="mediaFolder"/> and removes the sheets it supersedes.
    /// Returns the new sheet's filename, or null when the folder holds nothing
    /// playable to sample.
    /// </summary>
    Task<string?> RefreshAsync(
        IStorage storage,
        string mediaFolder,
        int tileWidth,
        int intervalSeconds,
        CancellationToken ct = default
    );
}
