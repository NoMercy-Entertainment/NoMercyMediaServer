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

namespace NoMercy.Tests.Common.TestMedia;

/// <summary>
/// One entry in the shared synthetic test-media corpus. Models a real-world
/// release: a file/dir name in the wild's scene/anime style, the container +
/// codecs to synthesize it with, and the metadata a correct filename parser
/// should extract. The names are modelled on real download-folder samples
/// (scene-dotted movies, bracketed fansub episodes, season packs, dual-audio,
/// HDR, UTF-8 titles) so the corpus exercises the parser and the encoder
/// (input -> output, ffmpeg argument construction) across formats — without
/// shipping any real copyrighted media.
/// </summary>
public sealed record MediaCorpusEntry
{
    /// <summary>Export-relative path including the release dir, e.g.
    /// "Films/Shrek.2001.1080p.BluRay.x264-GROUP/Shrek.2001.1080p.BluRay.x264-GROUP.mkv".</summary>
    public required string RelativePath { get; init; }

    public required MediaContainer Container { get; init; }
    public required MediaVideoCodec VideoCodec { get; init; }
    public required MediaAudioCodec AudioCodec { get; init; }

    /// <summary>Frame size, e.g. "640x360". Kept small so generation is fast.</summary>
    public string Resolution { get; init; } = "640x360";

    /// <summary>True to tag the stream as HDR10 (PQ / BT.2020).</summary>
    public bool Hdr { get; init; }

    /// <summary>Two audio tracks (dual-audio releases) when true.</summary>
    public bool DualAudio { get; init; }

    // ── Expected parse result (what a correct parser must extract) ──────────

    public required string ExpectedTitle { get; init; }
    public int? ExpectedYear { get; init; }
    public int? ExpectedSeason { get; init; }
    public int? ExpectedEpisode { get; init; }
    public required bool ExpectedIsMovie { get; init; }
}

public enum MediaContainer
{
    Mkv,
    Mp4,
}

public enum MediaVideoCodec
{
    H264,
    H265,
    Av1,
}

public enum MediaAudioCodec
{
    Aac,
    Ac3,
    Flac,
}

/// <summary>
/// The shared corpus definition. Pure data — no I/O. <see cref="MediaCorpusGenerator"/>
/// materializes these into tiny real files with ffmpeg; the filename-parser and
/// encoder test suites both consume the same list so a naming pattern proven by
/// the parser is also a real encode input.
/// </summary>
public static class MediaCorpus
{
    public static IReadOnlyList<MediaCorpusEntry> Entries { get; } =
    [
        // ── Scene-dotted movies ────────────────────────────────────────────
        new()
        {
            RelativePath =
                "Films/Shrek.2001.1080p.BluRay.x264-GROUP/Shrek.2001.1080p.BluRay.x264-GROUP.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H264,
            AudioCodec = MediaAudioCodec.Ac3,
            ExpectedTitle = "Shrek",
            ExpectedYear = 2001,
            ExpectedIsMovie = true,
        },
        new()
        {
            RelativePath =
                "Films/Mary.and.The.Witchs.Flower.2017.1080p.BluRay.AAC.2.0.x264-GROUP.mp4",
            Container = MediaContainer.Mp4,
            VideoCodec = MediaVideoCodec.H264,
            AudioCodec = MediaAudioCodec.Aac,
            ExpectedTitle = "Mary and The Witchs Flower",
            ExpectedYear = 2017,
            ExpectedIsMovie = true,
        },
        // ── HDR remux-style movie ──────────────────────────────────────────
        new()
        {
            RelativePath =
                "Films/Some.Film.2019.2160p.UHD.BluRay.HDR.x265-GROUP/Some.Film.2019.2160p.UHD.BluRay.HDR.x265-GROUP.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H265,
            AudioCodec = MediaAudioCodec.Flac,
            Hdr = true,
            ExpectedTitle = "Some Film",
            ExpectedYear = 2019,
            ExpectedIsMovie = true,
        },
        // ── Bracketed fansub episodes (anime style) ────────────────────────
        new()
        {
            RelativePath = "Anime/[Reza] Witch Hat Atelier/[Reza] Witch Hat Atelier - S01E05.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H265,
            AudioCodec = MediaAudioCodec.Flac,
            DualAudio = true,
            ExpectedTitle = "Witch Hat Atelier",
            ExpectedSeason = 1,
            ExpectedEpisode = 5,
            ExpectedIsMovie = false,
        },
        new()
        {
            RelativePath =
                "Anime/[Judas] Jujutsu Kaisen (Season 2) [1080p][HEVC x265 10bit][Dual-Audio]/[Judas] Jujutsu Kaisen - S02E01.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H265,
            AudioCodec = MediaAudioCodec.Aac,
            DualAudio = true,
            ExpectedTitle = "Jujutsu Kaisen",
            ExpectedSeason = 2,
            ExpectedEpisode = 1,
            ExpectedIsMovie = false,
        },
        // ── WEB-DL dotted series episode ───────────────────────────────────
        new()
        {
            RelativePath =
                "Shows/High.Potential.S01.1080p.AMZN.WEB-DL.DDP5.1.H.264-GROUP/High.Potential.S01E03.1080p.AMZN.WEB-DL.DDP5.1.H.264-GROUP.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H264,
            AudioCodec = MediaAudioCodec.Ac3,
            ExpectedTitle = "High Potential",
            ExpectedSeason = 1,
            ExpectedEpisode = 3,
            ExpectedIsMovie = false,
        },
        // ── AV1 release ────────────────────────────────────────────────────
        new()
        {
            RelativePath =
                "Anime/[Sokudo] Kyokou Suiri [1080p BD][AV1][dual audio]/[Sokudo] Kyokou Suiri - S01E02 [1080p BD][AV1].mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.Av1,
            AudioCodec = MediaAudioCodec.Flac,
            DualAudio = true,
            ExpectedTitle = "Kyokou Suiri",
            ExpectedSeason = 1,
            ExpectedEpisode = 2,
            ExpectedIsMovie = false,
        },
        // ── UTF-8 title ────────────────────────────────────────────────────
        new()
        {
            RelativePath =
                "Films/Amélie.2001.1080p.BluRay.x265-GROUP/Amélie.2001.1080p.BluRay.x265-GROUP.mkv",
            Container = MediaContainer.Mkv,
            VideoCodec = MediaVideoCodec.H265,
            AudioCodec = MediaAudioCodec.Aac,
            ExpectedTitle = "Amélie",
            ExpectedYear = 2001,
            ExpectedIsMovie = true,
        },
        // ── mp4 + AAC episode (simple container) ───────────────────────────
        new()
        {
            RelativePath = "Shows/Example Show/Example Show - S03E12.mp4",
            Container = MediaContainer.Mp4,
            VideoCodec = MediaVideoCodec.H264,
            AudioCodec = MediaAudioCodec.Aac,
            ExpectedTitle = "Example Show",
            ExpectedSeason = 3,
            ExpectedEpisode = 12,
            ExpectedIsMovie = false,
        },
    ];
}
