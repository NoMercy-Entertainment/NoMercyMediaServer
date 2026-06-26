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

namespace NoMercy.Encoder.Metadata;

/// <summary>
/// Merges per-stream metadata from the source file with DB-supplied metadata
/// using defined field-level precedence rules. Used during copy-mode encodes
/// where the source streams are passed byte-for-byte and already carry
/// embedded metadata that must not be silently discarded.
/// </summary>
public interface IMetadataMerger
{
    /// <summary>
    /// Merges <paramref name="source"/> stream metadata with <paramref name="dbTracks"/>
    /// according to field-level precedence rules and returns the unified track list.
    /// </summary>
    IReadOnlyList<TrackMetadata> Merge(
        IReadOnlyList<SourceTrackMetadata> source,
        IReadOnlyList<TrackMetadata> dbTracks
    );
}

/// <summary>
/// Per-stream metadata read from the source file during analysis.
/// Populated from the MediaInfo stream objects; Comment may be null when the
/// analyzer does not yet expose it per-stream.
/// </summary>
public record SourceTrackMetadata(
    /// <summary>
    /// Matches <see cref="TrackMetadata.OutputIndex"/> on the DB side so the
    /// merger can correlate source and DB rows by position.
    /// </summary>
    int OutputIndex,
    /// <summary>"video" | "audio" | "subtitle"</summary>
    string Kind,
    string? Language,
    string? Title,
    /// <summary>
    /// Per-stream comment tag from the source. Null when the analyzer does not
    /// expose it yet. The merger always preserves this value — DB never overwrites.
    /// </summary>
    string? Comment,
    bool IsDefault,
    bool IsForced
);
