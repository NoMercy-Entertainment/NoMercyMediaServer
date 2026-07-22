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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Audio;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies.Audio;

public class AudioHlsStrategyTests
{
    [Fact]
    public void Format_IsAudioHls()
    {
        AudioHlsStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<AudioHlsStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        Assert.Equal(expected: OutputFormat.AudioHls, actual: strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        AudioHlsStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<AudioHlsStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        Assert.Equal(expected: EncodeMode.SinglePass, actual: strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_AacAudioInput_ProducesPlaylistAndSegments()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out/audio.m3u8",
                    Duration: TimeSpan.FromSeconds(seconds: 3),
                    Error: null,
                    Metrics: new(OutputSizeBytes: 128, AverageSpeed: 1.0, AverageFps: 0.0, EncoderUsed: "aac", GpuUsed: null)
                )
            );

        AudioHlsStrategy strategy = new(
            encoder: encoder.Object,
            logger: NullLogger<AudioHlsStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            InputPath: "/media/track.aac",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Audio HLS",
                Container: Container.AudioHlsFmp4,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(condition: result.Success);
        Assert.Equal(expected: "/out/audio.m3u8", actual: result.OutputPath);
        Assert.NotNull(@object: result.Metrics);
        Assert.Equal(expected: "aac", actual: result.Metrics.EncoderUsed);
        encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task EncodeAsync_FlacInput_TranscodesToAac()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out/audio.m3u8",
                    Duration: TimeSpan.FromSeconds(seconds: 5),
                    Error: null,
                    Metrics: new(OutputSizeBytes: 128, AverageSpeed: 1.0, AverageFps: 0.0, EncoderUsed: "aac", GpuUsed: null)
                )
            );

        AudioHlsStrategy strategy = new(
            encoder: encoder.Object,
            logger: NullLogger<AudioHlsStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            InputPath: "/media/track.flac",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Audio HLS from FLAC",
                Container: Container.AudioHlsFmp4,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(condition: result.Success);
        Assert.NotNull(@object: result.Metrics);
        Assert.Equal(expected: "aac", actual: result.Metrics.EncoderUsed);
    }
}
