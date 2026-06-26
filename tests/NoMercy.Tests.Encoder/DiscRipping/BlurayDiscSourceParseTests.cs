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

using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Locks in the playlist-enumeration regex against the exact stderr
/// libbluray emits. The fixture below is the live capture from probing
/// Avatar: The Last Airbender Book 1, Disc 1 with the nomercy-ffmpeg
/// fork — 19 playlists ranging from 3:59 to 3:07:12.
/// </summary>
public class BlurayDiscSourceParseTests
{
    private const string AvatarStderr =
        @"src/libaacs/mmc.c:394: AACS not active or supported by the drive
src/libaacs/mmc.c:719: Drive does not support reading drive certificate
src/libaacs/aacs.c:1181: Unable to read drive certificate
[bluray @ 0000028e11e4bfc0] 19 usable playlists:
[bluray @ 0000028e11e4bfc0] playlist 01601.mpls (0:23:39)
[bluray @ 0000028e11e4bfc0] playlist 00252.mpls (0:03:59)
[bluray @ 0000028e11e4bfc0] playlist 01610.mpls (0:21:40)
[bluray @ 0000028e11e4bfc0] playlist 01617.mpls (0:22:36)
[bluray @ 0000028e11e4bfc0] playlist 01618.mpls (0:22:21)
[bluray @ 0000028e11e4bfc0] playlist 01619.mpls (0:22:46)
[bluray @ 0000028e11e4bfc0] playlist 01620.mpls (0:23:04)
[bluray @ 0000028e11e4bfc0] playlist 01621.mpls (0:23:07)
[bluray @ 0000028e11e4bfc0] playlist 01622.mpls (0:22:24)
[bluray @ 0000028e11e4bfc0] playlist 00600.mpls (3:07:12)
[bluray @ 0000028e11e4bfc0] playlist 00250.mpls (0:04:38)
[bluray @ 0000028e11e4bfc0] playlist 00602.mpls (0:22:28)
[bluray @ 0000028e11e4bfc0] playlist 00603.mpls (0:23:25)
[bluray @ 0000028e11e4bfc0] playlist 00604.mpls (0:23:09)
[bluray @ 0000028e11e4bfc0] playlist 00605.mpls (0:23:34)
[bluray @ 0000028e11e4bfc0] playlist 00606.mpls (0:23:52)
[bluray @ 0000028e11e4bfc0] playlist 00607.mpls (0:23:55)
[bluray @ 0000028e11e4bfc0] playlist 00608.mpls (0:23:12)
[bluray @ 0000028e11e4bfc0] playlist 00601.mpls (0:23:40)
[bluray @ 0000028e11e4bfc0] selected 00600.mpls
src/libbluray/disc/aacs.c:306: Unable to decrypt unit (AACS)!";

    [Fact]
    public void ParsePlaylists_AvatarS1D1_Returns19Playlists()
    {
        List<(int Index, TimeSpan Duration)> playlists = BlurayDiscSource.ParsePlaylists(
            AvatarStderr
        );

        playlists.Should().HaveCount(19);
    }

    [Fact]
    public void ParsePlaylists_AvatarS1D1_LongestIsMainPlaylist00600()
    {
        List<(int Index, TimeSpan Duration)> playlists = BlurayDiscSource.ParsePlaylists(
            AvatarStderr
        );

        (int Index, TimeSpan Duration) longest = playlists
            .OrderByDescending(p => p.Duration)
            .First();
        longest.Index.Should().Be(600);
        longest.Duration.Should().Be(new TimeSpan(3, 7, 12));
    }

    [Fact]
    public void ParsePlaylists_AvatarS1D1_EpisodePlaylistsAreAround22Minutes()
    {
        List<(int Index, TimeSpan Duration)> playlists = BlurayDiscSource.ParsePlaylists(
            AvatarStderr
        );

        // The episode playlists (everything except the main concat 00600 and
        // the two short intros 00250 / 00252) all fall in the 21-24 min band.
        IEnumerable<(int Index, TimeSpan Duration)> episodes = playlists.Where(p =>
            p.Index != 600 && p.Index != 250 && p.Index != 252
        );

        episodes
            .Should()
            .OnlyContain(p =>
                p.Duration >= TimeSpan.FromMinutes(20) && p.Duration <= TimeSpan.FromMinutes(25)
            );
    }

    [Fact]
    public void ParsePlaylists_AvatarS1D1_DurationsParsedCorrectly()
    {
        List<(int Index, TimeSpan Duration)> playlists = BlurayDiscSource.ParsePlaylists(
            AvatarStderr
        );

        (int Index, TimeSpan Duration) intro252 = playlists.First(p => p.Index == 252);
        intro252.Duration.Should().Be(new TimeSpan(0, 3, 59));

        (int Index, TimeSpan Duration) intro250 = playlists.First(p => p.Index == 250);
        intro250.Duration.Should().Be(new TimeSpan(0, 4, 38));

        (int Index, TimeSpan Duration) episode01601 = playlists.First(p => p.Index == 1601);
        episode01601.Duration.Should().Be(new TimeSpan(0, 23, 39));
    }

    [Fact]
    public void ParsePlaylists_EmptyInput_ReturnsEmpty()
    {
        BlurayDiscSource.ParsePlaylists("").Should().BeEmpty();
        BlurayDiscSource.ParsePlaylists(null!).Should().BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_NoPlaylistLines_ReturnsEmpty()
    {
        BlurayDiscSource
            .ParsePlaylists("Some random output\nNo playlists here\n[bluray] cert error")
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_DuplicateLines_DeduplicatesByIndex()
    {
        const string duplicateInput =
            @"[bluray] playlist 00600.mpls (3:07:12)
[bluray] playlist 00600.mpls (3:07:12)
[bluray] playlist 00601.mpls (0:23:40)";

        BlurayDiscSource.ParsePlaylists(duplicateInput).Should().HaveCount(2);
    }
}
