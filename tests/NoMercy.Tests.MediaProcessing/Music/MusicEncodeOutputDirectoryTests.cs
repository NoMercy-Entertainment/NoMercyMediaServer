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
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Music;

/// <summary>
/// A music encode writes into the album folder, stated relative to the
/// destination storage root.
/// <para>
/// The dispatcher used to store a whole local path there — resolved against the
/// running process rather than the library, so it read
/// "…/bin/Debug/net10.0/Libraries/Music/[Various Artists]/[2009] Radio 538: Hitzone 50".
/// The encoder rejects a rooted OutputDirectory outright, so every such job died
/// before writing a byte: 3,817 of them on the dev server, and thousands more
/// still queued carrying the same stored path.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class MusicEncodeOutputDirectoryTests
{
    [Fact]
    public void ARootedBasePathIsReadBackDownToTheAlbumFolder()
    {
        MusicEncodeJob
            .AlbumOutputDirectory(
                "C:/Projects/NoMercy/apps/nomercy-media-server/src/NoMercy.Service/bin/Debug/net10.0/Libraries/Music/[Various Artists]/[2009] Radio 538_ Hitzone 50"
            )
            .Should()
            .Be(
                "[2009] Radio 538_ Hitzone 50",
                "the encoder refuses a rooted OutputDirectory, and the album folder is "
                    + "the part of that path which was ever meant to be stored"
            );
    }

    [Fact]
    public void AWindowsRootedBasePathIsReadTheSameWay()
    {
        MusicEncodeJob
            .AlbumOutputDirectory(@"D:\Media\Libraries\Music\[Other]\[2005] Catch the Lightning")
            .Should()
            .Be("[2005] Catch the Lightning");
    }

    [Fact]
    public void AUnixRootedBasePathIsReadTheSameWay()
    {
        MusicEncodeJob
            .AlbumOutputDirectory("/mnt/vault/Media/Libraries/Music/[Other]/[1972] Eagles")
            .Should()
            .Be("[1972] Eagles");
    }

    [Fact]
    public void AnAlreadyRelativeBasePathIsLeftAlone()
    {
        MusicEncodeJob
            .AlbumOutputDirectory("[1972] Eagles")
            .Should()
            .Be("[1972] Eagles", "that is what the dispatcher stores now");
    }

    [Fact]
    public void NothingInIsNothingOut()
    {
        MusicEncodeJob.AlbumOutputDirectory(string.Empty).Should().BeEmpty();
        MusicEncodeJob.AlbumOutputDirectory("/").Should().BeEmpty();
    }
}
