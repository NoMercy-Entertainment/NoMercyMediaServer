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

        Assert.Null(track._video);
        Assert.Null(track.Video);
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

        Assert.NotNull(track._video);
        IVideo? result = track.Video;
        Assert.NotNull(result);
        Assert.Equal(1920, result!.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal("h264", result.Codec);
        Assert.Equal(5_000_000, result.BitRate);
    }

    [Fact]
    public void Audio_SetToNull_LeavesBackingColumnNull()
    {
        MetadataTrack track = new() { Audio = null };

        Assert.Null(track._audio);
        Assert.Null(track.Audio);
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

        Assert.NotNull(track._audio);
        IAudio? result = track.Audio;
        Assert.NotNull(result);
        Assert.Equal("eng", result!.Language);
        Assert.Equal("aac", result.Codec);
        Assert.Equal(2, result.Channels);
    }

    [Fact]
    public void Subtitle_SetToNull_LeavesBackingColumnNull()
    {
        MetadataTrack track = new() { Subtitle = null };

        Assert.Null(track._subtitle);
        Assert.Null(track.Subtitle);
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

        Assert.NotNull(track._subtitle);
        ISubtitle? result = track.Subtitle;
        Assert.NotNull(result);
        Assert.Equal("eng", result!.Language);
        Assert.Equal("srt", result.Codec);
        Assert.Equal("subrip", result.Type);
    }
}
