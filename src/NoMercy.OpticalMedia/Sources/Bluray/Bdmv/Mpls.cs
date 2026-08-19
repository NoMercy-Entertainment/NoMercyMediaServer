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

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// One PlayItem of a playlist: its clip, in/out times (45 kHz ticks) and whether it carries multiple
/// camera angles. The menu reels and the feature are both playlists, so the same PlayItem timing
/// drives both the menu segment windows and the feature chapter marks.
public sealed record MplsPlayItem
{
    public required int Index { get; init; }
    public required string ClipName { get; init; }
    public required long InTime { get; init; }
    public required long OutTime { get; init; }
    public required bool IsMultiAngle { get; init; }
    public required int AngleCount { get; init; }

    /// The extra angle clip names (angles 2..n); empty for single-angle PlayItems.
    public required IReadOnlyList<string> AngleClips { get; init; }

    public long Duration => OutTime - InTime;
}

/// One PlayListMark resolved to its presentation time. SedConds is the mark's offset into the
/// playlist's seamless stream (the sum of preceding PlayItem durations plus the mark's offset into
/// its own PlayItem). These are the disc's scene-selection points and the menu's screen boundaries.
public sealed record MplsMark
{
    public required int Index { get; init; }
    public required int MarkType { get; init; }
    public required int PlayItemRef { get; init; }
    public required double Seconds { get; init; }
}

/// One stream the playlist's STN_table declares, with its coding type, the codec name resolved
/// through the shared CodingType whitelist (null when the coding byte is outside the reference
/// table), and ISO-639-2 language. The menu's audio list and subtitle-locale list are derived from
/// the feature playlist's STN table, not hardcoded.
public sealed record MplsStream
{
    public required int CodingType { get; init; }
    public string? Codec { get; init; }
    public string? Language { get; init; }
}

/// A decoded MPLS playlist: its PlayItems, PlayListMarks (with presentation seconds) and the first
/// PlayItem's STN table. Built faithfully to libbluray mpls_parse.c.
public sealed record MplsPlaylist
{
    public required IReadOnlyList<MplsPlayItem> PlayItems { get; init; }
    public required IReadOnlyList<MplsMark> Marks { get; init; }
    public required IReadOnlyList<MplsStream> AudioStreams { get; init; }
    public required IReadOnlyList<MplsStream> SubtitleStreams { get; init; }

    /// IG (interactive graphics, coding 0x91) streams — present only when this playlist carries an
    /// HDMV menu. Zero across a whole disc means its menus are BD-J, not HDMV.
    public required IReadOnlyList<MplsStream> InteractiveGraphicsStreams { get; init; }
    public required double TotalSeconds { get; init; }
    public bool HasMultiAngle => PlayItems.Any(item => item.IsMultiAngle);
}
