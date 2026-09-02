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

namespace NoMercy.Tests.Storage;

[Trait("Category", "Unit")]
public class StoragePathHelpersRebaseTests
{
    [Fact]
    public void Rebases_a_driver_absolute_nfs_path_onto_the_scope_relative_root()
    {
        // The exact shape that crashed the rescan: MediaScan hands back a
        // driver-absolute path under the NFS export, the facade needs it
        // scope-relative to the folder root.
        string result = StoragePathHelpers.RebaseToFolderRoot(
            "/mnt/vault/Media/Marvels/TV.Shows/What.If.(2021)/What.If..S01E01.NoMercy.m3u8",
            "Marvels/TV.Shows"
        );

        result.Should().Be("Marvels/TV.Shows/What.If.(2021)/What.If..S01E01.NoMercy.m3u8");
    }

    [Fact]
    public void Leaves_an_already_relative_path_unchanged_except_a_leading_slash()
    {
        StoragePathHelpers
            .RebaseToFolderRoot("Marvels/TV.Shows/What.If.(2021)/f.m3u8", "Marvels/TV.Shows")
            .Should()
            .Be("Marvels/TV.Shows/What.If.(2021)/f.m3u8");
    }

    [Fact]
    public void Normalizes_windows_backslashes_and_a_windows_absolute_root()
    {
        // Legacy local library stored as an M: drive path; the folder root is
        // the same relative segment. Backslashes normalize to forward slashes.
        string result = StoragePathHelpers.RebaseToFolderRoot(
            @"M:\Anime\Anime\Death.Note.(2006)\Death.Note.S00E01.NoMercy.mp4",
            @"Anime\Anime"
        );

        result.Should().Be("Anime/Anime/Death.Note.(2006)/Death.Note.S00E01.NoMercy.mp4");
    }

    [Fact]
    public void Returns_trimmed_input_when_the_root_is_not_a_segment_of_the_path()
    {
        StoragePathHelpers
            .RebaseToFolderRoot("/some/other/tree/file.m3u8", "Marvels/TV.Shows")
            .Should()
            .Be("some/other/tree/file.m3u8");
    }

    [Fact]
    public void Returns_trimmed_input_when_the_root_is_empty()
    {
        StoragePathHelpers
            .RebaseToFolderRoot("/mnt/vault/Media/x/y.m3u8", "")
            .Should()
            .Be("mnt/vault/Media/x/y.m3u8");
    }

    [Fact]
    public void Matches_the_root_case_insensitively()
    {
        StoragePathHelpers
            .RebaseToFolderRoot("/mnt/vault/Media/MARVELS/tv.shows/show/f.m3u8", "Marvels/TV.Shows")
            .Should()
            .Be("MARVELS/tv.shows/show/f.m3u8");
    }

    [Fact]
    public void Anchors_the_root_at_a_segment_boundary_past_a_longer_sibling_with_the_same_prefix()
    {
        // The exact "TV.Shows/TV.Shows" doubling bug: a bare substring match on
        // root "TV.Shows" found it INSIDE the unrelated, longer sibling segment
        // "TV.Shows.Archive" that sits earlier in the path, cutting the rebase
        // at the wrong point. The real "TV.Shows" segment further along must win.
        string result = StoragePathHelpers.RebaseToFolderRoot(
            "/mnt/vault/Media/TV.Shows.Archive/Marvels/TV.Shows/What.If.(2021)/f.m3u8",
            "TV.Shows"
        );

        result.Should().Be("TV.Shows/What.If.(2021)/f.m3u8");
    }
}
