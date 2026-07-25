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

        values.Should().Contain(ContentSegmentType.Intro);
        values.Should().Contain(ContentSegmentType.Outro);
        values.Should().Contain(ContentSegmentType.Commercial);
        values.Should().Contain(ContentSegmentType.Recap);
        values.Should().Contain(ContentSegmentType.Content);
        values.Should().HaveCount(5);
    }

    [Fact]
    public void ContentSegment_ConstructsCorrectly()
    {
        TimeSpan start = TimeSpan.FromSeconds(5);
        TimeSpan end = TimeSpan.FromSeconds(95);

        ContentSegment segment = new(
            start,
            end,
            ContentSegmentType.Intro,
            0.92
        );

        segment.Start.Should().Be(start);
        segment.End.Should().Be(end);
        segment.Type.Should().Be(ContentSegmentType.Intro);
        segment.Confidence.Should().BeApproximately(0.92, 0.001);
    }

    [Fact]
    public void ContentSegment_Content_HasHighConfidence()
    {
        ContentSegment segment = new(
            TimeSpan.FromSeconds(90),
            TimeSpan.FromSeconds(3600),
            ContentSegmentType.Content,
            0.99
        );

        segment.Type.Should().Be(ContentSegmentType.Content);
        segment.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void ContentSegment_Recap_AtEndOfFile()
    {
        ContentSegment segment = new(
            TimeSpan.FromSeconds(3500),
            TimeSpan.FromSeconds(3700),
            ContentSegmentType.Recap,
            0.88
        );

        segment.Type.Should().Be(ContentSegmentType.Recap);
        segment.End.Should().BeGreaterThan(segment.Start);
    }

    [Fact]
    public void CropResult_NoCrop_HasShouldCropFalse()
    {
        CropResult result = new(1920, 1080, 0, 0, false);

        result.Width.Should().Be(1920);
        result.Height.Should().Be(1080);
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.ShouldCrop.Should().BeFalse();
    }

    [Fact]
    public void CropResult_WithLetterbox_HasShouldCropTrue()
    {
        CropResult result = new(1920, 800, 0, 140, true);

        result.ShouldCrop.Should().BeTrue();
        result.Y.Should().Be(140);
        result.Height.Should().Be(800);
    }

    [Fact]
    public void CropResult_WithPillarbox_HasShouldCropTrue()
    {
        CropResult result = new(1440, 1080, 240, 0, true);

        result.ShouldCrop.Should().BeTrue();
        result.X.Should().Be(240);
        result.Width.Should().Be(1440);
    }

    [Fact]
    public void CropResult_ZeroOffset_IsValid()
    {
        CropResult result = new(0, 0, 0, 0, false);

        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
        result.ShouldCrop.Should().BeFalse();
    }
}
