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

using NoMercy.MediaProcessing.Reclaim;

namespace NoMercy.Tests.MediaProcessing.Reclaim;

[Trait("Category", "Unit")]
public class ReclaimClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    [Fact]
    public void Classify_ProtectedFolder_CompleteHlsAndOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new("movie.mkv", false, 4_000_000_000, Now.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, Now.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, Now.AddDays(-1)),
            new("audio_eng", true, 200_000_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, true, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_NonProtected_OriginalWithMasterAndLadders_ReturnsReclaimableHls()
    {
        List<FolderEntry> entries =
        [
            new("movie.mkv", false, 4_000_000_000, Now.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, Now.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, Now.AddDays(-1)),
            new("audio_eng", true, 200_000_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.ReclaimableHls);
        result
            .TargetNames.Should()
            .BeEquivalentTo(["video_1920x1080_SDR", "audio_eng", "movie.NoMercy.m3u8"]);
        result.ReclaimableBytes.Should().Be(500 + 1_500_000_000 + 200_000_000);
    }

    [Fact]
    public void Classify_MasterlessLadders_AllStale_ReturnsOrphanPartial()
    {
        List<FolderEntry> entries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, Now.AddDays(-10)),
            new("audio_eng", true, 100_000_000, Now.AddDays(-10)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.OrphanPartial);
        result.TargetNames.Should().BeEquivalentTo(["video_1920x1080_SDR", "audio_eng"]);
        result.ReclaimableBytes.Should().Be(900_000_000 + 100_000_000);
    }

    [Fact]
    public void Classify_MasterlessLadders_OneFresh_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, Now.AddDays(-10)),
            new("audio_eng", true, 100_000_000, Now.AddMinutes(-30)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_CompleteHls_NoOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new("movie.NoMercy.m3u8", false, 500, Now.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_500_000_000, Now.AddDays(-1)),
            new("movie.srt", false, 4_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_EmptyFolder_ReturnsNone()
    {
        List<FolderEntry> entries = [];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_PlainNonMediaFilesOnly_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new("readme.txt", false, 10, Now.AddDays(-1)),
            new("poster.jpg", false, 20_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_TsSegmentOnly_MasterPresentNoLadder_DoesNotCountAsOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new("movie.NoMercy.m3u8", false, 500, Now.AddDays(-1)),
            new("segment001.ts", false, 2_000_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(0);
    }

    [Fact]
    public void Classify_TsSegmentWithMasterlessLadder_AllStale_ReturnsOrphanPartialViaLadderRule()
    {
        List<FolderEntry> entries =
        [
            new("video_1920x1080_SDR", true, 900_000_000, Now.AddDays(-10)),
            new("segment001.ts", false, 2_000_000, Now.AddDays(-10)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.OrphanPartial);
        result.TargetNames.Should().BeEquivalentTo(["video_1920x1080_SDR"]);
        result.ReclaimableBytes.Should().Be(900_000_000);
    }

    [Fact]
    public void Classify_LadderRegexBoundaries_ExcludesSubtitlesAndArbitraryDirs()
    {
        List<FolderEntry> entries =
        [
            new("movie.mkv", false, 4_000_000_000, Now.AddDays(-30)),
            new("movie.NoMercy.m3u8", false, 500, Now.AddDays(-1)),
            new("video_1920x1080", true, 1_000_000_000, Now.AddDays(-1)),
            new("video_1920x1080_SDR", true, 1_200_000_000, Now.AddDays(-1)),
            new("audio_eng", true, 800_000_000, Now.AddDays(-1)),
            new("subtitles", true, 300_000, Now.AddDays(-1)),
            new("Extras", true, 5_000_000_000, Now.AddDays(-1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries, false, Now, StaleAfter);

        result.Kind.Should().Be(ReclaimKind.ReclaimableHls);
        result
            .TargetNames.Should()
            .BeEquivalentTo([
                "video_1920x1080",
                "video_1920x1080_SDR",
                "audio_eng",
                "movie.NoMercy.m3u8",
            ]);
        result.TargetNames.Should().NotContain("subtitles");
        result.TargetNames.Should().NotContain("Extras");
        result.ReclaimableBytes.Should().Be(1_000_000_000L + 1_200_000_000L + 800_000_000L + 500L);
    }
}
