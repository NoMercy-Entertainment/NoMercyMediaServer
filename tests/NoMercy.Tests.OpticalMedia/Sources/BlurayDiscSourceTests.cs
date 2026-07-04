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

using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait("Category", "Unit")]
public class BlurayDiscSourceTests
{
    [Fact]
    public void ParsePlaylists_EmptyStderr_ReturnsEmpty()
    {
        string stderr = "";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_NullStderr_ReturnsEmpty()
    {
        string stderr = "";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_SinglePlaylist_ParsesIndexAndDuration()
    {
        string stderr = "playlist 00100.mpls (02:05:30)";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(100);
        result[0]
            .Duration.Should()
            .Be(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(5)).Add(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void ParsePlaylists_MultiplePlaylistsInSeparateLines_AllParsed()
    {
        string stderr = """
            libbluray DEBUG: parse_playlist 00090.mpls
            playlist 00090.mpls (01:30:45)
            libbluray DEBUG: parse_playlist 00100.mpls
            playlist 00100.mpls (02:15:20)
            libbluray DEBUG: parse_playlist 00110.mpls
            playlist 00110.mpls (00:45:15)
            """;

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(3);
        result[0].Index.Should().Be(90);
        result[1].Index.Should().Be(100);
        result[2].Index.Should().Be(110);
    }

    [Fact]
    public void ParsePlaylists_DuplicateIndices_DistinctByIndex()
    {
        string stderr = """
            playlist 00100.mpls (01:00:00)
            playlist 00100.mpls (02:00:00)
            """;

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(100);
    }

    [Fact]
    public void ParsePlaylists_MalformedIndexLine_Skipped()
    {
        string stderr = """
            playlist notanumber.mpls (01:00:00)
            playlist 00100.mpls (01:00:00)
            """;

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(100);
    }

    [Fact]
    public void ParsePlaylists_MalformedDurationLine_Skipped()
    {
        string stderr = """
            playlist 00100.mpls (invalid)
            playlist 00110.mpls (01:00:00)
            """;

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(110);
    }

    [Fact]
    public void ParsePlaylists_IgnoreCasePlaylistKeyword()
    {
        string stderr = """
            PLAYLIST 00100.mpls (01:00:00)
            Playlist 00110.mpls (01:30:00)
            """;

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ParsePlaylists_VariableWhitespaceFormat_Parsed()
    {
        string stderr = "playlist  00100.mpls  (01:00:00)";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(100);
    }

    [Fact]
    public void ParsePlaylists_LargePlaylistIndex_Parsed()
    {
        string stderr = "playlist 99999.mpls (01:00:00)";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Index.Should().Be(99999);
    }

    [Fact]
    public void ParsePlaylists_SingleDigitTimeParts_Parsed()
    {
        string stderr = "playlist 00100.mpls (1:2:3)";

        System.Collections.Generic.List<(int Index, TimeSpan Duration)> result =
            BlurayDiscSource.ParsePlaylists(stderr);

        result.Should().HaveCount(1);
        result[0].Duration.Should().Be(new TimeSpan(1, 2, 3));
    }

    [Fact]
    public void ClassifyProtection_NullStderr_ReturnsNull()
    {
        string stderr = "";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_DriveNoCertificate_ReturnsAacsProtection()
    {
        string stderr = "Drive does not support reading drive certificate";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
        result.Message.Should().Contain("bus key");
    }

    [Fact]
    public void ClassifyProtection_UnableToReadCertificate_ReturnsAacsProtection()
    {
        string stderr = "Unable to read drive certificate";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
    }

    [Fact]
    public void ClassifyProtection_UnableToDecryptUnit_ReturnsAacsProtection()
    {
        string stderr = "Unable to decrypt unit (AACS)";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
        result.Message.Should().Contain("KEYDB");
    }

    [Fact]
    public void ClassifyProtection_NoMatchingCertificate_ReturnsAacsProtection()
    {
        string stderr = "aacs: no matching certificate found";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
    }

    [Fact]
    public void ClassifyProtection_NoMatchingConverter_ReturnsBdplusProtection()
    {
        string stderr = "bdplus: no matching converter";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("BD+");
        result.Message.Should().Contain("converter");
    }

    [Fact]
    public void ClassifyProtection_CaseSensitivityIgnored()
    {
        string stderr = "DRIVE DOES NOT SUPPORT READING DRIVE CERTIFICATE";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
    }

    [Fact]
    public void ClassifyProtection_MixedWarnings_FirstMatchReturned()
    {
        string stderr = """
            Some warning here
            Unable to decrypt unit (AACS)
            Another warning
            """;

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("AACS");
    }

    [Fact]
    public void ClassifyProtection_BdplusPriority_CorrectlyClassified()
    {
        string stderr = "bdplus: no matching converter for this disc";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("BD+");
    }

    [Fact]
    public void ClassifyProtection_NoProtectionMarkers_ReturnsNull()
    {
        string stderr = """
            [bluray] playlist 00100.mpls found
            [bluray] reading playlist
            No protection markers here
            """;

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr);

        result.Should().BeNull();
    }
}
