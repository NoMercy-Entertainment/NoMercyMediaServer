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
        _outputDir = Path.Combine(Path.GetTempPath(), $"Mp4Audio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Finalize_AudioOnlyPlan_RenamesToM4a()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(_outputDir, "output.mp4");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01]);

        OutputPlan audioOnly = new(
            OutputFormat.Mp4,
            [],
            [
                new(
                    "libfdk_aac",
                    192,
                    2,
                    48000,
                    StreamAction.Transcode,
                    "en",
                    "0:a:0"
                ),
            ],
            [],
            null
        );

        await strategy.FinalizeAsync(_outputDir, audioOnly, "Track01", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_outputDir, "Track01.m4a")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Track01.mp4")));
    }

    [Fact]
    public async Task Finalize_VideoPlan_StaysMp4()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(_outputDir, "output.mp4");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x01]);

        OutputPlan videoPlan = new(
            OutputFormat.Mp4,
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    4000,
                    "medium",
                    "high",
                    "4.1",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            [],
            [],
            null
        );

        await strategy.FinalizeAsync(_outputDir, videoPlan, "Movie", CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_outputDir, "Movie.mp4")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "Movie.m4a")));
    }
}
