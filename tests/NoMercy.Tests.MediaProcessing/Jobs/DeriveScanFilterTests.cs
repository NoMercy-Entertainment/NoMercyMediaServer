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

using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

// ---------------------------------------------------------------------------
// The post-encode registration scans the library filtered by a single token
// and MediaScan keeps a file only when file.Contains(token) is true (see
// MediaScan.Process). VideoEncodeJob used to pass CreateFileName()
// (show.SxxExx.episodeTitle.NoMercy) as that token. The episode-title segment
// is re-cleaned from the DB title at scan time, so any drift from the name the
// encoder wrote at encode time — an apostrophe cleaned to "" one run and "."
// the next, a changed cleaning rule, an edited title — made the Contains match
// fail and registered nothing, forcing users to hit Rescan (which scans
// unfiltered and succeeds). DeriveScanFilter anchors on the output folder leaf
// the encoder actually wrote into instead, which carries only the stable
// show.SxxExx / movie.(year) token. These pin that the anchor matches the
// on-disk path where the drift-prone full name does not.
// ---------------------------------------------------------------------------
[Trait("Category", "Unit")]
public sealed class DeriveScanFilterTests
{
    [Fact]
    public void EpisodePath_ReturnsFolderLeaf_NotReconstructedFileName()
    {
        string filter = VideoEncodeJob.DeriveScanFilter(
            "Helstrom.(2020)/Helstrom.S01E01",
            "Helstrom.S01E01.Mother.s.Little.Helpers.NoMercy"
        );

        filter.Should().Be("Helstrom.S01E01");
    }

    [Fact]
    public void MoviePath_ReturnsFolderLeaf()
    {
        string filter = VideoEncodeJob.DeriveScanFilter(
            "Jolt.(2021)",
            "Jolt.(2021).NoMercy"
        );

        filter.Should().Be("Jolt.(2021)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void EmptyOutputPath_FallsBackToFileName(string? outputPath)
    {
        string filter = VideoEncodeJob.DeriveScanFilter(
            outputPath,
            "Some.Movie.(2021).NoMercy"
        );

        filter.Should().Be("Some.Movie.(2021).NoMercy");
    }

    [Fact]
    public void BackslashSeparators_AreHandled()
    {
        string filter = VideoEncodeJob.DeriveScanFilter(
            @"Helstrom.(2020)\Helstrom.S01E01",
            "ignored"
        );

        filter.Should().Be("Helstrom.S01E01");
    }

    // The regression itself: the encoder wrote the file with the apostrophe
    // cleaned to "" ("Mothers"); the drift-prone filter reconstructs it with the
    // apostrophe as "." ("Mother.s"). MediaScan's file.Contains(filter) therefore
    // misses the real file — the exact "have to rescan after encode" symptom —
    // while the folder-anchored filter still matches it.
    [Fact]
    public void FolderAnchor_MatchesOnDiskFile_WhereDriftedFileNameFilterDoesNot()
    {
        const string outputPath = "Helstrom.(2020)/Helstrom.S01E01";
        const string driftedFileNameFilter = "Helstrom.S01E01.Mother.s.Little.Helpers.NoMercy";
        const string onDiskFile =
            "M:/TV.Shows/Helstrom.(2020)/Helstrom.S01E01/"
            + "Helstrom.S01E01.Mothers.Little.Helpers.NoMercy.m3u8";

        // Old behaviour: MediaScan filtered by the reconstructed file name and
        // dropped the real file, so nothing registered.
        onDiskFile.Contains(driftedFileNameFilter).Should().BeFalse();

        // New behaviour: the folder-anchored filter is a substring of the real
        // path, so MediaScan keeps the file and registration succeeds.
        string anchor = VideoEncodeJob.DeriveScanFilter(outputPath, driftedFileNameFilter);
        onDiskFile.Contains(anchor).Should().BeTrue();
    }
}
