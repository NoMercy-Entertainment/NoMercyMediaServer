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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Verifies Phase 13 audio single-file output — when an MP4 encode has no
/// video tracks, the finalized file extension switches to .m4a so music
/// players pick it up correctly (V1 parity).
/// </summary>
public class Mp4AudioOnlyExtensionTests : IDisposable
{
    private readonly string _outputDir;

    public Mp4AudioOnlyExtensionTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Mp4Audio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Finalize_AudioOnlyPlan_RenamesToM4a()
    {
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(path1: _outputDir, path2: "output.mp4");
        await File.WriteAllBytesAsync(path: sourcePath, bytes: [0x00, 0x01]);

        OutputPlan audioOnly = new(
            Format: OutputFormat.Mp4,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "libfdk_aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "en",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        await strategy.FinalizeAsync(outputDirectory: _outputDir, plan: audioOnly, mediaTitle: "Track01", ct: CancellationToken.None);

        Assert.True(condition: File.Exists(path: Path.Combine(path1: _outputDir, path2: "Track01.m4a")));
        Assert.False(condition: File.Exists(path: Path.Combine(path1: _outputDir, path2: "Track01.mp4")));
    }

    [Fact]
    public async Task Finalize_VideoPlan_StaysMp4()
    {
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(path1: _outputDir, path2: "output.mp4");
        await File.WriteAllBytesAsync(path: sourcePath, bytes: [0x00, 0x01]);

        OutputPlan videoPlan = new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 4000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        await strategy.FinalizeAsync(outputDirectory: _outputDir, plan: videoPlan, mediaTitle: "Movie", ct: CancellationToken.None);

        Assert.True(condition: File.Exists(path: Path.Combine(path1: _outputDir, path2: "Movie.mp4")));
        Assert.False(condition: File.Exists(path: Path.Combine(path1: _outputDir, path2: "Movie.m4a")));
    }
}
