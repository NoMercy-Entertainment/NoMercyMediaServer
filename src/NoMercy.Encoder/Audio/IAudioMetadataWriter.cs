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

namespace NoMercy.Encoder.Audio;

public interface IAudioMetadataWriter
{
    Task WriteTagsAsync(string filePath, AudioMetadata metadata, CancellationToken ct);
}

public record AudioMetadata(
    string Title,
    string Artist,
    string AlbumArtist,
    string Album,
    int TrackNumber,
    int DiscNumber,
    int? Year,
    string? Genre,
    string? MusicBrainzTrackId,
    string? MusicBrainzReleaseId,
    string? AcoustIdFingerprint,
    AlbumArtSource? CoverArt
);

public record AlbumArtSource(string? FilePath, string? Url, AlbumArtType Type);

public enum AlbumArtType
{
    Front,
    Back,
    Disc,
    Artist,
    Other,
}
