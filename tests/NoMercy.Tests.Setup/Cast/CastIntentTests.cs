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

using NoMercy.Setup.Cast;

namespace NoMercy.Tests.Setup.Cast;

/// <summary>
/// Requirement: each <see cref="CastIntent"/> factory must populate exactly the fields
/// the cast-receiver-leanback spec (§8.2) defines for that intent type and leave every
/// other field at its ignore-when-null default — a stray populated field for the wrong
/// intent type would ship as unexpected JSON to the receiver.
/// </summary>
[Trait("Category", "Unit")]
public class CastIntentTests
{
    [Fact]
    public void Idle_SetsOnlyType()
    {
        CastIntent intent = CastIntent.Idle();

        Assert.Equal("idle", intent.Type);
        Assert.Null(intent.MediaType);
        Assert.Null(intent.MediaId);
        Assert.Null(intent.ListType);
        Assert.Null(intent.ListId);
        Assert.Null(intent.TrackId);
        Assert.Null(intent.ResumeAt);
        Assert.Null(intent.Route);
    }

    [Fact]
    public void PlayVideo_SetsMediaTypeAndMediaId()
    {
        CastIntent intent = CastIntent.PlayVideo("movie", "603");

        Assert.Equal("play_video", intent.Type);
        Assert.Equal("movie", intent.MediaType);
        Assert.Equal("603", intent.MediaId);
        Assert.Null(intent.ResumeAt);
        Assert.Null(intent.ListType);
        Assert.Null(intent.Route);
    }

    [Fact]
    public void PlayVideo_WithResumeAt_SetsResumePosition()
    {
        CastIntent intent = CastIntent.PlayVideo("tv", "1399", 842);

        Assert.Equal(842, intent.ResumeAt);
    }

    [Fact]
    public void PlayMusic_SetsListTypeAndListId()
    {
        CastIntent intent = CastIntent.PlayMusic("album", "abc-123");

        Assert.Equal("play_music", intent.Type);
        Assert.Equal("album", intent.ListType);
        Assert.Equal("abc-123", intent.ListId);
        Assert.Null(intent.TrackId);
        Assert.Null(intent.ResumeAt);
        Assert.Null(intent.MediaType);
    }

    [Fact]
    public void PlayMusic_WithTrackIdAndResumeAt_SetsBoth()
    {
        CastIntent intent = CastIntent.PlayMusic("playlist", "xyz-789", "track-1", 30);

        Assert.Equal("track-1", intent.TrackId);
        Assert.Equal(30, intent.ResumeAt);
    }

    [Fact]
    public void Navigate_SetsRoute()
    {
        CastIntent intent = CastIntent.Navigate("/library/movies");

        Assert.Equal("navigate", intent.Type);
        Assert.Equal("/library/movies", intent.Route);
        Assert.Null(intent.MediaType);
        Assert.Null(intent.ListType);
    }

    [Fact]
    public void DefaultConstructor_DefaultsToIdleType()
    {
        CastIntent intent = new();

        Assert.Equal("idle", intent.Type);
    }
}
