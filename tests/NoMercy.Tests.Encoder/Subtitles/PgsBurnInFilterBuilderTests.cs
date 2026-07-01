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

        result.FilterComplex.Should().Be("[0:v:0][0:s:0]overlay=format=auto[burned]");
    }

    [Fact]
    public void Build_FirstVideoSecondSubtitle_ProducesOverlayFilter()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 1);

        result.FilterComplex.Should().Be("[0:v:0][0:s:1]overlay=format=auto[burned]");
    }

    [Fact]
    public void Build_SecondVideoFirstSubtitle_ProducesOverlayFilter()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 1, subtitleStreamIndex: 0);

        result.FilterComplex.Should().Be("[0:v:1][0:s:0]overlay=format=auto[burned]");
    }

    // ── Multiple stream indices ────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, "[0:v:0][0:s:0]overlay=format=auto[burned]")]
    [InlineData(0, 2, "[0:v:0][0:s:2]overlay=format=auto[burned]")]
    [InlineData(1, 0, "[0:v:1][0:s:0]overlay=format=auto[burned]")]
    [InlineData(2, 3, "[0:v:2][0:s:3]overlay=format=auto[burned]")]
    public void Build_VaryingIndices_ProducesCorrectFilterComplex(
        int videoIdx,
        int subtitleIdx,
        string expectedFilter
    )
    {
        PgsBurnInFilterChain result = _builder.Build(videoIdx, subtitleIdx);
        result.FilterComplex.Should().Be(expectedFilter);
    }

    // ── Map label ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_AlwaysEmitsBurned_AsOutputLabel()
    {
        PgsBurnInFilterChain result1 = _builder.Build(0, 0);
        PgsBurnInFilterChain result2 = _builder.Build(1, 2);

        result1.MapLabel.Should().Be("[burned]");
        result2.MapLabel.Should().Be("[burned]");
    }

    // ── Overlay filter format parameter ────────────────────────────────────

    [Fact]
    public void Build_FilterComplex_ContainsOverlayWithFormatAuto()
    {
        PgsBurnInFilterChain result = _builder.Build(0, 0);

        result.FilterComplex.Should().Contain("overlay=format=auto");
    }

    [Fact]
    public void Build_HigherIndices_StillUsesFormatAuto()
    {
        PgsBurnInFilterChain result = _builder.Build(videoStreamIndex: 5, subtitleStreamIndex: 10);

        result.FilterComplex.Should().Contain("overlay=format=auto");
    }

    // ── Stream selector format validation ───────────────────────────────────

    [Fact]
    public void Build_FilterComplex_UsesInput0Notation()
    {
        PgsBurnInFilterChain result = _builder.Build(0, 0);

        // Filter always references input 0 (first input file)
        result.FilterComplex.Should().StartWith("[0:v:");
        result.FilterComplex.Should().Contain("[0:s:");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 5)]
    [InlineData(10, 20)]
    public void Build_Always_ReferencesInput0(int videoIdx, int subtitleIdx)
    {
        PgsBurnInFilterChain result = _builder.Build(videoIdx, subtitleIdx);

        // Even with higher indices, filter always references input 0
        result.FilterComplex.Should().StartWith("[0:v:");
        result.FilterComplex.Should().Contain("[0:s:");
        result.FilterComplex.Should().Contain("overlay=format=auto");
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
        PgsBurnInFilterChain result = _builder.Build(0, 1);

        // The MapLabel must be a bracketed label suitable for -map [burned]
        result.MapLabel.Should().StartWith("[");
        result.MapLabel.Should().EndWith("]");
    }

    [Fact]
    public void Build_SeveralStreams_AllHaveSameMapLabel()
    {
        PgsBurnInFilterChain result1 = _builder.Build(0, 0);
        PgsBurnInFilterChain result2 = _builder.Build(1, 2);
        PgsBurnInFilterChain result3 = _builder.Build(5, 10);

        result1.MapLabel.Should().Be(result2.MapLabel);
        result2.MapLabel.Should().Be(result3.MapLabel);
    }

    // ── Typical encoder integration scenario ────────────────────────────────

    [Fact]
    public void Build_ProducesValidFfmpegFilterAndMapChain()
    {
        PgsBurnInFilterChain pgs = _builder.Build(videoStreamIndex: 0, subtitleStreamIndex: 2);

        // Simulate encoder building: -filter_complex + -map
        string filterArg = pgs.FilterComplex;
        string mapArg = pgs.MapLabel;

        filterArg.Should().Contain("[0:v:0][0:s:2]overlay=format=auto");
        mapArg.Should().Be("[burned]");

        // Verify the chain is valid: filter defines the output, map uses it
        filterArg.Should().EndWith("[burned]");
    }
}
