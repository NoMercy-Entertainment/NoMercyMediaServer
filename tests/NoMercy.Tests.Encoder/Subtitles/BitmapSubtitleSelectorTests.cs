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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// Guards the index handed to <see cref="ISubtitleOcrEngine.OcrAsync"/>, which
/// ffmpeg reads as <c>[0:s:N]</c> — a position among SUBTITLE streams, not the
/// absolute ffprobe stream index carried by <see cref="SubtitleStreamInfo.Index"/>.
/// Passing the absolute index makes ffmpeg reject the filtergraph
/// ("Stream specifier ':s:3' matches no streams") and silently produce no sidecar.
/// </summary>
public class BitmapSubtitleSelectorTests
{
    private static SubtitleStreamInfo Sub(
        int absoluteIndex,
        string codec,
        string? language = "eng"
    ) =>
        new(
            Index: absoluteIndex,
            Codec: codec,
            Language: language,
            IsDefault: false,
            IsForced: false,
            Title: null
        );

    [Fact]
    public void Select_UsesSubtitleRelativeIndex_NotTheAbsoluteFfprobeIndex()
    {
        // A real BD rip: video at 0, two audio at 1-2, PGS subtitles at 3 and 4.
        // The absolute indices (3, 4) address no stream under [0:s:N] — only
        // s:0 and s:1 exist. This is the exact shape that produced 34 .mks files
        // and zero OCR sidecars.
        List<SubtitleStreamInfo> streams =
        [
            Sub(3, "hdmv_pgs_subtitle"),
            Sub(4, "hdmv_pgs_subtitle"),
        ];

        IReadOnlyList<BitmapSubtitleRef> selected = BitmapSubtitleSelector.Select(streams);

        selected.Select(entry => entry.SubtitleIndex).Should().Equal(0, 1);
        selected.Select(entry => entry.Stream.Index).Should().Equal(3, 4);
    }

    [Fact]
    public void Select_IndexesAgainstAllSubtitleStreams_NotOnlyTheBitmapOnes()
    {
        // A text track ahead of the bitmap one: the PGS stream is subtitle 1.
        // Indexing the FILTERED list instead would yield 0 and OCR the ASS
        // track's slot — the subtle variant of the same bug.
        List<SubtitleStreamInfo> streams = [Sub(2, "ass"), Sub(3, "hdmv_pgs_subtitle")];

        IReadOnlyList<BitmapSubtitleRef> selected = BitmapSubtitleSelector.Select(streams);

        selected.Should().HaveCount(1);
        selected[0].SubtitleIndex.Should().Be(1);
        selected[0].Stream.Codec.Should().Be("hdmv_pgs_subtitle");
    }

    [Fact]
    public void Select_SkipsTextSubtitles()
    {
        List<SubtitleStreamInfo> streams = [Sub(2, "subrip"), Sub(3, "ass"), Sub(4, "mov_text")];

        BitmapSubtitleSelector.Select(streams).Should().BeEmpty();
    }

    [Theory]
    [InlineData("hdmv_pgs_subtitle")]
    [InlineData("pgs")]
    [InlineData("dvd_subtitle")]
    [InlineData("vobsub")]
    [InlineData("dvb_subtitle")]
    public void Select_RecognisesEveryBitmapCodecAlias(string codec)
    {
        BitmapSubtitleSelector.Select([Sub(3, codec)]).Should().HaveCount(1);
    }

    [Fact]
    public void Select_NoSubtitleStreams_ReturnsEmpty()
    {
        BitmapSubtitleSelector.Select([]).Should().BeEmpty();
    }
}
