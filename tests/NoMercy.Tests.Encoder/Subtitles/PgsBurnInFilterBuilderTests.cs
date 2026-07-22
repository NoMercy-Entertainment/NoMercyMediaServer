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

using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// PgsBurnInFilterBuilder constructs FFmpeg overlay filter chains for burning
/// PGS (bitmap) subtitles onto video. The filter syntax routes both video and
/// subtitle streams through the overlay filter to produce a composite output.
/// </summary>
public class PgsBurnInFilterBuilderTests
{
    private readonly PgsBurnInFilterBuilder _builder = new();

    // ── Basic single-stream scenarios ───────────────────────────────────────

    [Fact]
    public void Build_FirstVideoFirstSubtitle_ProducesOverlayFilter()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        result.FilterComplex.Should().Be(expected: "[0:v:0][0:s:0]overlay=format=auto[burned]");
    }

    [Fact]
    public void Build_FirstVideoSecondSubtitle_ProducesOverlayFilter()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 1);

        result.FilterComplex.Should().Be(expected: "[0:v:0][0:s:1]overlay=format=auto[burned]");
    }

    [Fact]
    public void Build_SecondVideoFirstSubtitle_ProducesOverlayFilter()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 1, subtitleStreamIndex: 0);

        result.FilterComplex.Should().Be(expected: "[0:v:1][0:s:0]overlay=format=auto[burned]");
    }

    // ── Multi-consumer split (regression: one pad cannot feed many -maps) ───

    [Fact]
    public void Build_MultipleVideoRungs_SplitsBurnedIntoOnePadPerRung()
    {
        // A filtergraph output pad can only feed one -map; two rungs mapping the
        // same [burned] pad aborts ffmpeg. The overlay must be split per rung.
        PgsBurnInFilterChain result = _builder.Build(
            videoStreamIndex: 0,
            subtitleStreamIndex: 0,
            videoOutputCount: 2,
            includeThumbnails: false
        );

        result.FilterComplex.Should().Contain(expected: "overlay=format=auto,split=2");
        result.VideoLabels.Should().Equal(expected: ["[burned0]", "[burned1]"]);
        result.FilterComplex.Should().Contain(expected: "[burned0][burned1]");
        result.VideoLabels.Should().OnlyHaveUniqueItems();
        result.ThumbnailLabel.Should().BeNull();
    }

    [Fact]
    public void Build_Thumbnails_AddsADedicatedThumbnailPad()
    {
        // The PGS path bypasses the normal graph that defines [thumbs]; the
        // thumbnail branch must get its own split pad or -map references a label
        // no filtergraph defines.
        PgsBurnInFilterChain result = _builder.Build(
            videoStreamIndex: 0,
            subtitleStreamIndex: 0,
            videoOutputCount: 1,
            includeThumbnails: true
        );

        result.FilterComplex.Should().Contain(expected: "split=2");
        result.ThumbnailLabel.Should().NotBeNull();
        result.FilterComplex.Should().Contain(expected: result.ThumbnailLabel!);
        result.VideoLabels.Should().HaveCount(expected: 1);
        result.VideoLabels[index: 0].Should().NotBe(unexpected: result.ThumbnailLabel);
    }

    [Fact]
    public void Build_MultipleRungsAndThumbnails_SplitsForEveryConsumer()
    {
        PgsBurnInFilterChain result = _builder.Build(
            videoStreamIndex: 0,
            subtitleStreamIndex: 1,
            videoOutputCount: 3,
            includeThumbnails: true
        );

        result.FilterComplex.Should().Contain(expected: "split=4");
        result.VideoLabels.Should().HaveCount(expected: 3);
        List<string> allPads = [.. result.VideoLabels, result.ThumbnailLabel!];
        allPads.Should().OnlyHaveUniqueItems(because: "every consumer needs its own pad");
    }

    [Fact]
    public void Build_SingleVideoNoThumbnails_KeepsSinglePadNoSplit()
    {
        PgsBurnInFilterChain result = _builder.Build(
            videoStreamIndex: 0,
            subtitleStreamIndex: 0,
            videoOutputCount: 1,
            includeThumbnails: false
        );

        result.FilterComplex.Should().NotContain(unexpected: "split");
        result.FilterComplex.Should().EndWith(expected: "[burned]");
        result.VideoLabels.Should().Equal(expected: "[burned]");
    }

    // ── Multiple stream indices ────────────────────────────────────────────

    [Theory]
    [InlineData(data: [0, 0, "[0:v:0][0:s:0]overlay=format=auto[burned]"])]
    [InlineData(data: [0, 2, "[0:v:0][0:s:2]overlay=format=auto[burned]"])]
    [InlineData(data: [1, 0, "[0:v:1][0:s:0]overlay=format=auto[burned]"])]
    [InlineData(data: [2, 3, "[0:v:2][0:s:3]overlay=format=auto[burned]"])]
    public void Build_VaryingIndices_ProducesCorrectFilterComplex(
        int videoIdx,
        int subtitleIdx,
        string expectedFilter
    )
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: videoIdx, subtitleStreamIndex: subtitleIdx);
        result.FilterComplex.Should().Be(expected: expectedFilter);
    }

    // ── Map label ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_AlwaysEmitsBurned_AsOutputLabel()
    {
        PgsBurnInFilterChain result1 = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);
        PgsBurnInFilterChain result2 = _builder.Build(videoStreamIndex: 1, subtitleStreamIndex: 2);

        result1.MapLabel.Should().Be(expected: "[burned]");
        result2.MapLabel.Should().Be(expected: "[burned]");
    }

    // ── Overlay filter format parameter ────────────────────────────────────

    [Fact]
    public void Build_FilterComplex_ContainsOverlayWithFormatAuto()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        result.FilterComplex.Should().Contain(expected: "overlay=format=auto");
    }

    [Fact]
    public void Build_HigherIndices_StillUsesFormatAuto()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 5, subtitleStreamIndex: 10);

        result.FilterComplex.Should().Contain(expected: "overlay=format=auto");
    }

    // ── Stream selector format validation ───────────────────────────────────

    [Fact]
    public void Build_FilterComplex_UsesInput0Notation()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        // Filter always references input 0 (first input file)
        result.FilterComplex.Should().StartWith(expected: "[0:v:");
        result.FilterComplex.Should().Contain(expected: "[0:s:");
    }

    [Theory]
    [InlineData(data: [0, 0])]
    [InlineData(data: [3, 5])]
    [InlineData(data: [10, 20])]
    public void Build_Always_ReferencesInput0(int videoIdx, int subtitleIdx)
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: videoIdx, subtitleStreamIndex: subtitleIdx);

        // Even with higher indices, filter always references input 0
        result.FilterComplex.Should().StartWith(expected: "[0:v:");
        result.FilterComplex.Should().Contain(expected: "[0:s:");
        result.FilterComplex.Should().Contain(expected: "overlay=format=auto");
    }

    // ── Edge case: zero-indexed streams ────────────────────────────────────

    [Fact]
    public void Build_AllZeroIndices_Valid()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);

        result.FilterComplex.Should().NotBeNullOrEmpty();
        result.MapLabel.Should().NotBeNullOrEmpty();
    }

    // ── Usage contract: map label integration ───────────────────────────────

    [Fact]
    public void Build_MapLabel_IsValidForFfmpegMapOption()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 1);

        // The MapLabel must be a bracketed label suitable for -map [burned]
        result.MapLabel.Should().StartWith(expected: "[");
        result.MapLabel.Should().EndWith(expected: "]");
    }

    [Fact]
    public void Build_SeveralStreams_AllHaveSameMapLabel()
    {
        PgsBurnInFilterChain result1 = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 0);
        PgsBurnInFilterChain result2 = _builder.Build(videoStreamIndex: 1, subtitleStreamIndex: 2);
        PgsBurnInFilterChain result3 = _builder.Build(videoStreamIndex: 5, subtitleStreamIndex: 10);

        result1.MapLabel.Should().Be(expected: result2.MapLabel);
        result2.MapLabel.Should().Be(expected: result3.MapLabel);
    }

    // ── Typical encoder integration scenario ────────────────────────────────

    [Fact]
    public void Build_ProducesValidFfmpegFilterAndMapChain()
    {
        PgsBurnInFilterChain pgs = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 2);

        // Simulate encoder building: -filter_complex + -map
        string filterArg = pgs.FilterComplex;
        string mapArg = pgs.MapLabel;

        filterArg.Should().Contain(expected: "[0:v:0][0:s:2]overlay=format=auto");
        mapArg.Should().Be(expected: "[burned]");

        // Verify the chain is valid: filter defines the output, map uses it
        filterArg.Should().EndWith(expected: "[burned]");
    }
}
