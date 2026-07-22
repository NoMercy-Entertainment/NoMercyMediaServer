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

using NoMercy.Encoder.ContentAnalysis;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

public class ContentAnalysisTests
{
    [Fact]
    public void ContentSegmentType_HasExpectedValues()
    {
        ContentSegmentType[] values = Enum.GetValues<ContentSegmentType>();

        values.Should().Contain(expected: ContentSegmentType.Intro);
        values.Should().Contain(expected: ContentSegmentType.Outro);
        values.Should().Contain(expected: ContentSegmentType.Commercial);
        values.Should().Contain(expected: ContentSegmentType.Recap);
        values.Should().Contain(expected: ContentSegmentType.Content);
        values.Should().HaveCount(expected: 5);
    }

    [Fact]
    public void ContentSegment_ConstructsCorrectly()
    {
        TimeSpan start = TimeSpan.FromSeconds(seconds: 5);
        TimeSpan end = TimeSpan.FromSeconds(seconds: 95);

        ContentSegment segment = new(
            Start: start,
            End: end,
            Type: ContentSegmentType.Intro,
            Confidence: 0.92
        );

        segment.Start.Should().Be(expected: start);
        segment.End.Should().Be(expected: end);
        segment.Type.Should().Be(expected: ContentSegmentType.Intro);
        segment.Confidence.Should().BeApproximately(expectedValue: 0.92, precision: 0.001);
    }

    [Fact]
    public void ContentSegment_Content_HasHighConfidence()
    {
        ContentSegment segment = new(
            Start: TimeSpan.FromSeconds(seconds: 90),
            End: TimeSpan.FromSeconds(seconds: 3600),
            Type: ContentSegmentType.Content,
            Confidence: 0.99
        );

        segment.Type.Should().Be(expected: ContentSegmentType.Content);
        segment.Confidence.Should().BeGreaterThan(expected: 0.9);
    }

    [Fact]
    public void ContentSegment_Recap_AtEndOfFile()
    {
        ContentSegment segment = new(
            Start: TimeSpan.FromSeconds(seconds: 3500),
            End: TimeSpan.FromSeconds(seconds: 3700),
            Type: ContentSegmentType.Recap,
            Confidence: 0.88
        );

        segment.Type.Should().Be(expected: ContentSegmentType.Recap);
        segment.End.Should().BeGreaterThan(expected: segment.Start);
    }

    [Fact]
    public void CropResult_NoCrop_HasShouldCropFalse()
    {
        CropResult result = new(Width: 1920, Height: 1080, X: 0, Y: 0, ShouldCrop: false);

        result.Width.Should().Be(expected: 1920);
        result.Height.Should().Be(expected: 1080);
        result.X.Should().Be(expected: 0);
        result.Y.Should().Be(expected: 0);
        result.ShouldCrop.Should().BeFalse();
    }

    [Fact]
    public void CropResult_WithLetterbox_HasShouldCropTrue()
    {
        CropResult result = new(Width: 1920, Height: 800, X: 0, Y: 140, ShouldCrop: true);

        result.ShouldCrop.Should().BeTrue();
        result.Y.Should().Be(expected: 140);
        result.Height.Should().Be(expected: 800);
    }

    [Fact]
    public void CropResult_WithPillarbox_HasShouldCropTrue()
    {
        CropResult result = new(Width: 1440, Height: 1080, X: 240, Y: 0, ShouldCrop: true);

        result.ShouldCrop.Should().BeTrue();
        result.X.Should().Be(expected: 240);
        result.Width.Should().Be(expected: 1440);
    }

    [Fact]
    public void CropResult_ZeroOffset_IsValid()
    {
        CropResult result = new(Width: 0, Height: 0, X: 0, Y: 0, ShouldCrop: false);

        result.Width.Should().Be(expected: 0);
        result.Height.Should().Be(expected: 0);
        result.ShouldCrop.Should().BeFalse();
    }
}
