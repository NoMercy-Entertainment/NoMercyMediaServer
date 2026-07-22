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

using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Tests.Database;

// MetadataTrack (base class of PlaybackPreference) stores each per-track quality
// selection as a JSON string but exposes it as a typed IVideo/IAudio/ISubtitle. The
// getter/setter pair on each of the three properties is the only logic here: a null
// round-trips to a null backing string (and back), a value serializes/deserializes
// losslessly through the backing column.
public class MetadataTrackTests
{
    [Fact]
    public void Video_SetToNull_LeavesBackingColumnNull()
    {
        MetadataTrack track = new() { Video = null };

        Assert.Null(@object: track._video);
        Assert.Null(@object: track.Video);
    }

    [Fact]
    public void Video_SetToValue_RoundTripsThroughTheBackingColumn()
    {
        MetadataTrack track = new()
        {
            Video = new IVideo
            {
                Width = 1920,
                Height = 1080,
                Codec = "h264",
                BitRate = 5_000_000,
            },
        };

        Assert.NotNull(@object: track._video);
        IVideo? result = track.Video;
        Assert.NotNull(@object: result);
        Assert.Equal(expected: 1920, actual: result!.Width);
        Assert.Equal(expected: 1080, actual: result.Height);
        Assert.Equal(expected: "h264", actual: result.Codec);
        Assert.Equal(expected: 5_000_000, actual: result.BitRate);
    }

    [Fact]
    public void Audio_SetToNull_LeavesBackingColumnNull()
    {
        MetadataTrack track = new() { Audio = null };

        Assert.Null(@object: track._audio);
        Assert.Null(@object: track.Audio);
    }

    [Fact]
    public void Audio_SetToValue_RoundTripsThroughTheBackingColumn()
    {
        MetadataTrack track = new()
        {
            Audio = new IAudio
            {
                Language = "eng",
                Codec = "aac",
                Channels = 2,
            },
        };

        Assert.NotNull(@object: track._audio);
        IAudio? result = track.Audio;
        Assert.NotNull(@object: result);
        Assert.Equal(expected: "eng", actual: result!.Language);
        Assert.Equal(expected: "aac", actual: result.Codec);
        Assert.Equal(expected: 2, actual: result.Channels);
    }

    [Fact]
    public void Subtitle_SetToNull_LeavesBackingColumnNull()
    {
        MetadataTrack track = new() { Subtitle = null };

        Assert.Null(@object: track._subtitle);
        Assert.Null(@object: track.Subtitle);
    }

    [Fact]
    public void Subtitle_SetToValue_RoundTripsThroughTheBackingColumn()
    {
        MetadataTrack track = new()
        {
            Subtitle = new ISubtitle
            {
                Language = "eng",
                Codec = "srt",
                Type = "subrip",
            },
        };

        Assert.NotNull(@object: track._subtitle);
        ISubtitle? result = track.Subtitle;
        Assert.NotNull(@object: result);
        Assert.Equal(expected: "eng", actual: result!.Language);
        Assert.Equal(expected: "srt", actual: result.Codec);
        Assert.Equal(expected: "subrip", actual: result.Type);
    }
}
