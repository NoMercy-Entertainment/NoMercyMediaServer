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

using FluentAssertions;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Storage;

[Trait(name: "Category", value: "Unit")]
public class StoragePathHelpersRebaseTests
{
    [Fact]
    public void Rebases_a_driver_absolute_nfs_path_onto_the_scope_relative_root()
    {
        // The exact shape that crashed the rescan: MediaScan hands back a
        // driver-absolute path under the NFS export, the facade needs it
        // scope-relative to the folder root.
        string result = StoragePathHelpers.RebaseToFolderRoot(
            absolutePath: "/mnt/vault/Media/Marvels/TV.Shows/What.If.(2021)/What.If..S01E01.NoMercy.m3u8",
            folderPath: "Marvels/TV.Shows"
        );

        result.Should().Be(expected: "Marvels/TV.Shows/What.If.(2021)/What.If..S01E01.NoMercy.m3u8");
    }

    [Fact]
    public void Leaves_an_already_relative_path_unchanged_except_a_leading_slash()
    {
        StoragePathHelpers
            .RebaseToFolderRoot(absolutePath: "Marvels/TV.Shows/What.If.(2021)/f.m3u8", folderPath: "Marvels/TV.Shows")
            .Should()
            .Be(expected: "Marvels/TV.Shows/What.If.(2021)/f.m3u8");
    }

    [Fact]
    public void Normalizes_windows_backslashes_and_a_windows_absolute_root()
    {
        // Legacy local library stored as an M: drive path; the folder root is
        // the same relative segment. Backslashes normalize to forward slashes.
        string result = StoragePathHelpers.RebaseToFolderRoot(
            absolutePath: @"M:\Anime\Anime\Death.Note.(2006)\Death.Note.S00E01.NoMercy.mp4",
            folderPath: @"Anime\Anime"
        );

        result.Should().Be(expected: "Anime/Anime/Death.Note.(2006)/Death.Note.S00E01.NoMercy.mp4");
    }

    [Fact]
    public void Returns_trimmed_input_when_the_root_is_not_a_segment_of_the_path()
    {
        StoragePathHelpers
            .RebaseToFolderRoot(absolutePath: "/some/other/tree/file.m3u8", folderPath: "Marvels/TV.Shows")
            .Should()
            .Be(expected: "some/other/tree/file.m3u8");
    }

    [Fact]
    public void Returns_trimmed_input_when_the_root_is_empty()
    {
        StoragePathHelpers
            .RebaseToFolderRoot(absolutePath: "/mnt/vault/Media/x/y.m3u8", folderPath: "")
            .Should()
            .Be(expected: "mnt/vault/Media/x/y.m3u8");
    }

    [Fact]
    public void Matches_the_root_case_insensitively()
    {
        StoragePathHelpers
            .RebaseToFolderRoot(absolutePath: "/mnt/vault/Media/MARVELS/tv.shows/show/f.m3u8", folderPath: "Marvels/TV.Shows")
            .Should()
            .Be(expected: "MARVELS/tv.shows/show/f.m3u8");
    }
}
