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
[Trait(name: "Category", value: "Unit")]
public class CastIntentTests
{
    [Fact]
    public void Idle_SetsOnlyType()
    {
        CastIntent intent = CastIntent.Idle();

        Assert.Equal(expected: "idle", actual: intent.Type);
        Assert.Null(@object: intent.MediaType);
        Assert.Null(@object: intent.MediaId);
        Assert.Null(@object: intent.ListType);
        Assert.Null(@object: intent.ListId);
        Assert.Null(@object: intent.TrackId);
        Assert.Null(value: intent.ResumeAt);
        Assert.Null(@object: intent.Route);
    }

    [Fact]
    public void PlayVideo_SetsMediaTypeAndMediaId()
    {
        CastIntent intent = CastIntent.PlayVideo(mediaType: "movie", mediaId: "603");

        Assert.Equal(expected: "play_video", actual: intent.Type);
        Assert.Equal(expected: "movie", actual: intent.MediaType);
        Assert.Equal(expected: "603", actual: intent.MediaId);
        Assert.Null(value: intent.ResumeAt);
        Assert.Null(@object: intent.ListType);
        Assert.Null(@object: intent.Route);
    }

    [Fact]
    public void PlayVideo_WithResumeAt_SetsResumePosition()
    {
        CastIntent intent = CastIntent.PlayVideo(mediaType: "tv", mediaId: "1399", resumeAt: 842);

        Assert.Equal(expected: 842, actual: intent.ResumeAt);
    }

    [Fact]
    public void PlayMusic_SetsListTypeAndListId()
    {
        CastIntent intent = CastIntent.PlayMusic(listType: "album", listId: "abc-123");

        Assert.Equal(expected: "play_music", actual: intent.Type);
        Assert.Equal(expected: "album", actual: intent.ListType);
        Assert.Equal(expected: "abc-123", actual: intent.ListId);
        Assert.Null(@object: intent.TrackId);
        Assert.Null(value: intent.ResumeAt);
        Assert.Null(@object: intent.MediaType);
    }

    [Fact]
    public void PlayMusic_WithTrackIdAndResumeAt_SetsBoth()
    {
        CastIntent intent = CastIntent.PlayMusic(listType: "playlist", listId: "xyz-789", trackId: "track-1", resumeAt: 30);

        Assert.Equal(expected: "track-1", actual: intent.TrackId);
        Assert.Equal(expected: 30, actual: intent.ResumeAt);
    }

    [Fact]
    public void Navigate_SetsRoute()
    {
        CastIntent intent = CastIntent.Navigate(route: "/library/movies");

        Assert.Equal(expected: "navigate", actual: intent.Type);
        Assert.Equal(expected: "/library/movies", actual: intent.Route);
        Assert.Null(@object: intent.MediaType);
        Assert.Null(@object: intent.ListType);
    }

    [Fact]
    public void DefaultConstructor_DefaultsToIdleType()
    {
        CastIntent intent = new();

        Assert.Equal(expected: "idle", actual: intent.Type);
    }
}
