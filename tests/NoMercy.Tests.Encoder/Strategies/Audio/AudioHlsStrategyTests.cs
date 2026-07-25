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
            Mock.Of<IEncoder>(),
            NullLogger<AudioHlsStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        Assert.Equal(OutputFormat.AudioHls, strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        AudioHlsStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<AudioHlsStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        Assert.Equal(EncodeMode.SinglePass, strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_AacAudioInput_ProducesPlaylistAndSegments()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    true,
                    "/out/audio.m3u8",
                    TimeSpan.FromSeconds(3),
                    null,
                    new(128, 1.0, 0.0, "aac", null)
                )
            );

        AudioHlsStrategy strategy = new(
            encoder.Object,
            NullLogger<AudioHlsStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            "/media/track.aac",
            "/out",
            new(
                Ulid.NewUlid(),
                "Audio HLS",
                Container.AudioHlsFmp4,
                null,
                [],
                []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            null,
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal("/out/audio.m3u8", result.OutputPath);
        Assert.NotNull(result.Metrics);
        Assert.Equal("aac", result.Metrics.EncoderUsed);
        encoder.Verify(
            e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task EncodeAsync_FlacInput_TranscodesToAac()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    true,
                    "/out/audio.m3u8",
                    TimeSpan.FromSeconds(5),
                    null,
                    new(128, 1.0, 0.0, "aac", null)
                )
            );

        AudioHlsStrategy strategy = new(
            encoder.Object,
            NullLogger<AudioHlsStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            "/media/track.flac",
            "/out",
            new(
                Ulid.NewUlid(),
                "Audio HLS from FLAC",
                Container.AudioHlsFmp4,
                null,
                [],
                []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            null,
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Metrics);
        Assert.Equal("aac", result.Metrics.EncoderUsed);
    }
}
