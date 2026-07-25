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

using NoMercy.Api.Services.Music;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Covers the `CurrentList` route-segment mapping: all five member routes
/// (album, artist, playlist, track, genre) are plural, but the semantic
/// playback `type` token dispatched through StartPlaybackCommand (and the
/// MusicPlaylistManager switch behind it) stays singular. ToRouteSegment/
/// FromRouteSegment must round-trip so IsSamePlaylist detection and the
/// cast-handoff CastIntent.PlayMusic list_type never regress.
/// </summary>
[Trait("Category", "Unit")]
public class MusicPlayerStateFactoryRouteSegmentTests
{
    [Theory]
    [InlineData(["album", "albums"])]
    [InlineData(["artist", "artists"])]
    [InlineData(["playlist", "playlists"])]
    [InlineData(["track", "tracks"])]
    [InlineData(["genre", "genres"])]
    public void ToRouteSegment_MapsTypeToExpectedSegment(string type, string expectedSegment)
    {
        MusicPlayerStateFactory.ToRouteSegment(type).Should().Be(expectedSegment);
    }

    [Theory]
    [InlineData(["albums", "album"])]
    [InlineData(["artists", "artist"])]
    [InlineData(["playlists", "playlist"])]
    [InlineData(["tracks", "track"])]
    [InlineData(["genres", "genre"])]
    public void FromRouteSegment_MapsSegmentToExpectedType(string segment, string expectedType)
    {
        MusicPlayerStateFactory.FromRouteSegment(segment).Should().Be(expectedType);
    }

    [Theory]
    [InlineData("album")]
    [InlineData("artist")]
    [InlineData("playlist")]
    [InlineData("track")]
    [InlineData("genre")]
    public void ToRouteSegment_ThenFromRouteSegment_RoundTripsToOriginalType(string type)
    {
        string segment = MusicPlayerStateFactory.ToRouteSegment(type);
        MusicPlayerStateFactory.FromRouteSegment(segment).Should().Be(type);
    }
}
