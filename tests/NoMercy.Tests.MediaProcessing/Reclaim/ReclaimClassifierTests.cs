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

[Trait(name: "Category", value: "Unit")]
public class ReclaimClassifierTests
{
    private static readonly DateTimeOffset Now = new(year: 2026, month: 7, day: 11, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(hours: 24);

    [Fact]
    public void Classify_ProtectedFolder_CompleteHlsAndOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: Now.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: Now.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: true, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_NonProtected_OriginalWithMasterAndLadders_ReturnsReclaimableHls()
    {
        List<FolderEntry> entries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: Now.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: Now.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 200_000_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.ReclaimableHls);
        result
            .TargetNames.Should()
            .BeEquivalentTo(expectation: ["video_1920x1080_SDR", "audio_eng", "movie.NoMercy.m3u8"]);
        result.ReclaimableBytes.Should().Be(expected: 500 + 1_500_000_000 + 200_000_000);
    }

    [Fact]
    public void Classify_MasterlessLadders_AllStale_ReturnsOrphanPartial()
    {
        List<FolderEntry> entries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: Now.AddDays(days: -10)),
            new(Name: "audio_eng", IsDirectory: true, Size: 100_000_000, LastModified: Now.AddDays(days: -10)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.OrphanPartial);
        result.TargetNames.Should().BeEquivalentTo(expectation: ["video_1920x1080_SDR", "audio_eng"]);
        result.ReclaimableBytes.Should().Be(expected: 900_000_000 + 100_000_000);
    }

    [Fact]
    public void Classify_MasterlessLadders_OneFresh_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: Now.AddDays(days: -10)),
            new(Name: "audio_eng", IsDirectory: true, Size: 100_000_000, LastModified: Now.AddMinutes(minutes: -30)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_CompleteHls_NoOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: Now.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_500_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "movie.srt", IsDirectory: false, Size: 4_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_EmptyFolder_ReturnsNone()
    {
        List<FolderEntry> entries = [];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_PlainNonMediaFilesOnly_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new(Name: "readme.txt", IsDirectory: false, Size: 10, LastModified: Now.AddDays(days: -1)),
            new(Name: "poster.jpg", IsDirectory: false, Size: 20_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_TsSegmentOnly_MasterPresentNoLadder_DoesNotCountAsOriginal_ReturnsNone()
    {
        List<FolderEntry> entries =
        [
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: Now.AddDays(days: -1)),
            new(Name: "segment001.ts", IsDirectory: false, Size: 2_000_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.None);
        result.TargetNames.Should().BeEmpty();
        result.ReclaimableBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Classify_TsSegmentWithMasterlessLadder_AllStale_ReturnsOrphanPartialViaLadderRule()
    {
        List<FolderEntry> entries =
        [
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 900_000_000, LastModified: Now.AddDays(days: -10)),
            new(Name: "segment001.ts", IsDirectory: false, Size: 2_000_000, LastModified: Now.AddDays(days: -10)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.OrphanPartial);
        result.TargetNames.Should().BeEquivalentTo(expectation: ["video_1920x1080_SDR"]);
        result.ReclaimableBytes.Should().Be(expected: 900_000_000);
    }

    [Fact]
    public void Classify_LadderRegexBoundaries_ExcludesSubtitlesAndArbitraryDirs()
    {
        List<FolderEntry> entries =
        [
            new(Name: "movie.mkv", IsDirectory: false, Size: 4_000_000_000, LastModified: Now.AddDays(days: -30)),
            new(Name: "movie.NoMercy.m3u8", IsDirectory: false, Size: 500, LastModified: Now.AddDays(days: -1)),
            new(Name: "video_1920x1080", IsDirectory: true, Size: 1_000_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "video_1920x1080_SDR", IsDirectory: true, Size: 1_200_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "audio_eng", IsDirectory: true, Size: 800_000_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "subtitles", IsDirectory: true, Size: 300_000, LastModified: Now.AddDays(days: -1)),
            new(Name: "Extras", IsDirectory: true, Size: 5_000_000_000, LastModified: Now.AddDays(days: -1)),
        ];

        ReclaimClassification result = ReclaimClassifier.Classify(entries: entries, isProtected: false, now: Now, partialStaleAfter: StaleAfter);

        result.Kind.Should().Be(expected: ReclaimKind.ReclaimableHls);
        result
            .TargetNames.Should()
            .BeEquivalentTo(expectation:
            [
                "video_1920x1080",
                "video_1920x1080_SDR",
                "audio_eng",
                "movie.NoMercy.m3u8",
            ]);
        result.TargetNames.Should().NotContain(unexpected: "subtitles");
        result.TargetNames.Should().NotContain(unexpected: "Extras");
        result.ReclaimableBytes.Should().Be(expected: 1_000_000_000L + 1_200_000_000L + 800_000_000L + 500L);
    }
}
