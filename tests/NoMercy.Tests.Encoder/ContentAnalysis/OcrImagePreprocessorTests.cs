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

namespace NoMercy.Tests.Encoder.ContentAnalysis;

/// <summary>
/// Unit tests for <see cref="OcrImagePreprocessor.PreprocessForOcr"/>.
/// No file I/O, no FFmpeg — pure pixel math.
/// </summary>
public class OcrImagePreprocessorTests
{
    [Fact]
    public void PreprocessForOcr_FullyOpaque_ReturnsLumaOfOriginalColor()
    {
        // Fully-opaque white pixel (RGBA = 255,255,255,255)
        // BT.601 luma of white = 255; alpha=1 → output = 1*255 + 0*128 = 255
        byte[] rgba = [255, 255, 255, 255];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        result[0].Should().Be(255);
    }

    [Fact]
    public void PreprocessForOcr_FullyTransparent_ReturnsNeutralGrey()
    {
        // Fully-transparent pixel → output = 0*luma + 1*128 = 128
        byte[] rgba = [0, 0, 0, 0];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        result[0].Should().Be(128);
    }

    [Fact]
    public void PreprocessForOcr_HalfAlpha_BlendsBetweenLumaAndGrey()
    {
        // Black pixel (luma=0) at alpha=128 (~50%)
        // out = 0.502 * 0 + 0.498 * 128 ≈ 63.7 → 64
        byte[] rgba = [0, 0, 0, 128];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result.Should().HaveCount(1);
        // Allow ±2 for floating-point rounding
        result[0].Should().BeInRange(62, 66);
    }

    [Fact]
    public void PreprocessForOcr_OutputLengthMatchesWidthTimesHeight()
    {
        int w = 4;
        int h = 3;
        byte[] rgba = new byte[w * h * 4]; // all zeros
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, w, h);

        result.Should().HaveCount(w * h);
    }

    [Fact]
    public void PreprocessForOcr_WrongBufferSize_Throws()
    {
        // 2x2 image needs 16 bytes; supply 12
        byte[] tooShort = new byte[12];
        Action act = () => OcrImagePreprocessor.PreprocessForOcr(tooShort, 2, 2);

        act.Should().Throw<ArgumentException>().WithMessage("*16*");
    }

    [Fact]
    public void PreprocessForOcr_FullyOpaqueBlack_ReturnsZero()
    {
        // Black, fully opaque → luma=0, alpha=1 → out = 0
        byte[] rgba = [0, 0, 0, 255];
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result[0].Should().Be(0);
    }

    [Fact]
    public void PreprocessForOcr_MultiPixel_AllTransparent_AllReturnGrey()
    {
        int w = 8;
        int h = 8;
        byte[] rgba = new byte[w * h * 4]; // all zeros → alpha=0
        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, w, h);

        result.Should().AllSatisfy(b => b.Should().Be(128));
    }

    // ── BT.601 luma weights — pinning the per-channel math ─────────────────

    [Fact]
    public void PreprocessForOcr_PureRedOpaque_UsesBt601RedWeight()
    {
        // BT.601: R coefficient = 0.299, full alpha → 0.299 * 255 ≈ 76.
        // Wrong weight here = subtle but real OCR regressions on coloured
        // PGS subs that use the red channel as a foreground marker.
        byte[] rgba = [0xFF, 0x00, 0x00, 0xFF];

        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result[0].Should().Be(76);
    }

    [Fact]
    public void PreprocessForOcr_PureGreenOpaque_UsesBt601GreenWeight()
    {
        // BT.601: G coefficient = 0.587 — the dominant channel for luma.
        byte[] rgba = [0x00, 0xFF, 0x00, 0xFF];

        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result[0].Should().Be(150);
    }

    [Fact]
    public void PreprocessForOcr_PureBlueOpaque_UsesBt601BlueWeight()
    {
        // BT.601: B coefficient = 0.114 — darkest perceived channel.
        byte[] rgba = [0x00, 0x00, 0xFF, 0xFF];

        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 1, 1);

        result[0].Should().Be(29);
    }

    [Fact]
    public void PreprocessForOcr_MultiPixelMix_ProducesPerPixelOutput()
    {
        // 2×1 frame: pixel0 = opaque white, pixel1 = opaque black.
        // Verifies the loop indexes the RGBA buffer correctly (no
        // off-by-one or row-stride confusion).
        byte[] rgba = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0xFF];

        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 2, 1);

        result.Should().HaveCount(2);
        result[0].Should().Be(255);
        result[1].Should().Be(0);
    }

    [Fact]
    public void PreprocessForOcr_ZeroDimensions_ReturnsEmpty()
    {
        // 0×0 frame is a degenerate but valid input — buffer length must
        // match (0). Result is an empty array, not an exception.
        byte[] rgba = [];

        byte[] result = OcrImagePreprocessor.PreprocessForOcr(rgba, 0, 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void PreprocessForOcr_WrongBufferSize_ParamNameMatches()
    {
        // ArgumentException's ParamName must be the actual parameter so
        // callers' error-handling switches don't drift if the parameter
        // ever gets renamed.
        byte[] tooShort = new byte[3];

        Action act = () => OcrImagePreprocessor.PreprocessForOcr(tooShort, 1, 1);

        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("rgbaImage");
    }
}
