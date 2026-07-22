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

using NoMercy.Encoder.Output;

namespace NoMercy.Tests.Encoder.Output;

public class OutputArtifactTests
{
    [Theory]
    [InlineData(data: ["master.m3u8", "application/vnd.apple.mpegurl"])]
    [InlineData(data: ["stream.mpd", "application/dash+xml"])]
    [InlineData(data: ["clip.mp4", "video/mp4"])]
    [InlineData(data: ["movie.mkv", "video/x-matroska"])]
    [InlineData(data: ["video.webm", "video/webm"])]
    [InlineData(data: ["segment.ts", "video/mp2t"])]
    [InlineData(data: ["seg.m4s", "video/iso.segment"])]
    [InlineData(data: ["audio.m4a", "audio/mp4"])]
    [InlineData(data: ["track.aac", "audio/aac"])]
    [InlineData(data: ["track.mp3", "audio/mpeg"])]
    [InlineData(data: ["audio.ogg", "audio/ogg"])]
    [InlineData(data: ["track.opus", "audio/opus"])]
    [InlineData(data: ["audio.flac", "audio/flac"])]
    [InlineData(data: ["subs.vtt", "text/vtt"])]
    [InlineData(data: ["subs.srt", "text/plain"])]
    [InlineData(data: ["subs.ass", "text/plain"])]
    [InlineData(data: ["subs.ssa", "text/plain"])]
    [InlineData(data: ["subs.ttml", "application/ttml+xml"])]
    [InlineData(data: ["thumb.webp", "image/webp"])]
    [InlineData(data: ["thumb.jpg", "image/jpeg"])]
    [InlineData(data: ["thumb.jpeg", "image/jpeg"])]
    [InlineData(data: ["thumb.png", "image/png"])]
    [InlineData(data: ["meta.json", "application/json"])]
    [InlineData(data: ["font.ttf", "font/ttf"])]
    [InlineData(data: ["font.otf", "font/otf"])]
    [InlineData(data: ["font.woff", "font/woff"])]
    [InlineData(data: ["font.woff2", "font/woff2"])]
    public void MimeFromPath_maps_known_extensions(string fileName, string expectedMime)
    {
        string mime = OutputArtifact.MimeFromPath(path: fileName);
        Assert.Equal(expected: expectedMime, actual: mime);
    }

    [Theory]
    [InlineData(data: "file.xyz")]
    [InlineData(data: "noextension")]
    [InlineData(data: "archive.tar.gz")]
    [InlineData(data: "data.bin")]
    public void MimeFromPath_unknown_extension_returns_octet_stream(string fileName)
    {
        string mime = OutputArtifact.MimeFromPath(path: fileName);
        Assert.Equal(expected: "application/octet-stream", actual: mime);
    }

    [Fact]
    public void MimeFromPath_is_case_insensitive()
    {
        Assert.Equal(expected: "video/mp4", actual: OutputArtifact.MimeFromPath(path: "CLIP.MP4"));
        Assert.Equal(expected: "application/vnd.apple.mpegurl", actual: OutputArtifact.MimeFromPath(path: "MASTER.M3U8"));
        Assert.Equal(expected: "text/vtt", actual: OutputArtifact.MimeFromPath(path: "SUBS.VTT"));
    }

    [Fact]
    public void OutputArtifact_construction_round_trips_all_fields()
    {
        OutputArtifact artifact = new(
            Path: "/out/master.m3u8",
            SizeBytes: 4096L,
            Sha256: "deadbeef",
            MediaType: "application/vnd.apple.mpegurl"
        );

        Assert.Equal(expected: "/out/master.m3u8", actual: artifact.Path);
        Assert.Equal(expected: 4096L, actual: artifact.SizeBytes);
        Assert.Equal(expected: "deadbeef", actual: artifact.Sha256);
        Assert.Equal(expected: "application/vnd.apple.mpegurl", actual: artifact.MediaType);
    }

    [Fact]
    public void MimeFromPath_handles_path_with_directories()
    {
        // Should only look at the extension, not be confused by dots in directory names
        Assert.Equal(expected: "video/mp4", actual: OutputArtifact.MimeFromPath(path: "/some/path.v1/clip.mp4"));
        Assert.Equal(
            expected: "application/vnd.apple.mpegurl",
            actual: OutputArtifact.MimeFromPath(path: "/hls/v720p/master.m3u8")
        );
    }
}
