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

using NoMercy.MediaProcessing.Inbox;

namespace NoMercy.Tests.MediaProcessing.Inbox;

[Trait("Category", "Unit")]
public class InboxClassifierAlreadyEncodedTests
{
    [Fact]
    public void IsFinishedHls_MasterAndLadder_ReturnsTrue()
    {
        string[] siblingNames = ["movie.NoMercy.m3u8", "video_1920x1080_SDR", "audio_eng"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.True(result);
    }

    [Fact]
    public void IsFinishedHls_MasterOnly_ReturnsFalse()
    {
        string[] siblingNames = ["movie.NoMercy.m3u8"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void IsFinishedHls_LadderOnlyNoMaster_ReturnsFalse()
    {
        string[] siblingNames = ["video_1920x1080_SDR", "audio_eng"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void IsFinishedHls_PlainDropSingleFile_ReturnsFalse()
    {
        string[] siblingNames = ["movie.mkv"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void IsFinishedHls_PlainDropWithPoster_ReturnsFalse()
    {
        string[] siblingNames = ["movie.mp4", "poster.jpg"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void IsFinishedHls_CaseInsensitiveMasterWithLadder_ReturnsTrue()
    {
        string[] siblingNames = ["movie.nomercy.M3U8", "video_1920x1080_SDR"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.True(result);
    }

    [Fact]
    public void IsFinishedHls_MasterWithNonLadderDirOnly_ReturnsFalse()
    {
        string[] siblingNames = ["movie.NoMercy.m3u8", "subtitles"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void IsFinishedHls_MasterWithExtrasDirOnly_ReturnsFalse()
    {
        string[] siblingNames = ["movie.NoMercy.m3u8", "Extras"];

        bool result = InboxClassifier.IsFinishedHls(siblingNames);

        Assert.False(result);
    }

    [Fact]
    public void ClassificationResult_AlreadyEncoded_RoundTripsAndDefaultsFalse()
    {
        ClassificationResult defaulted = new()
        {
            DetectedType = "movie",
            Confidence = "high",
            Candidates = [],
        };

        ClassificationResult encoded = new()
        {
            DetectedType = "movie",
            Confidence = "high",
            Candidates = [],
            AlreadyEncoded = true,
        };

        Assert.False(defaulted.AlreadyEncoded);
        Assert.True(encoded.AlreadyEncoded);
    }
}
