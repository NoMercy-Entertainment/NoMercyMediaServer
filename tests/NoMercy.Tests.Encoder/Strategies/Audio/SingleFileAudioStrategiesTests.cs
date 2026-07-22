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

namespace NoMercy.Tests.Encoder.Strategies.Audio;

/// <summary>
/// Thin pass-through strategies for single-file audio outputs (MP3 / FLAC /
/// Ogg). Each only declares its OutputFormat + EncodeMode and delegates the
/// actual encode to IEncoder. Wrong format identification breaks the
/// StrategyResolver lookup; wrong EncodeMode would falsely advertise 2-pass
/// support that audio codecs don't meaningfully benefit from.
/// </summary>
public class SingleFileAudioStrategiesTests
{
    private static IEncoder MockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out/audio",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: null
                )
            );
        return mock.Object;
    }

    [Fact]
    public void Mp3Strategy_DeclaresMp3SinglePass()
    {
        Mp3Strategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<Mp3Strategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        strategy.Format.Should().Be(expected: OutputFormat.Mp3);
        strategy.EncodeMode.Should().Be(expected: EncodeMode.SinglePass);
    }

    [Fact]
    public void FlacStrategy_DeclaresFlacSinglePass()
    {
        FlacStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<FlacStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        strategy.Format.Should().Be(expected: OutputFormat.Flac);
        strategy.EncodeMode.Should().Be(expected: EncodeMode.SinglePass);
    }

    [Fact]
    public void OggStrategy_DeclaresOggSinglePass()
    {
        OggStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<OggStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        strategy.Format.Should().Be(expected: OutputFormat.Ogg);
        strategy.EncodeMode.Should().Be(expected: EncodeMode.SinglePass);
    }

    [Fact]
    public async Task Mp3Strategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
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
                    OutputPath: "/out/song.mp3",
                    Duration: TimeSpan.FromSeconds(seconds: 180),
                    Error: null,
                    Metrics: null
                )
            );
        Mp3Strategy strategy = new(
            encoder: encoderMock.Object,
            logger: NullLogger<Mp3Strategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: new(InputPath: "/in.flac", OutputDirectory: "/out", Profile: null!),
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(expected: "/out/song.mp3");
        encoderMock.Verify(
            expression: e => e.EncodeAsync(It.IsAny<EncodingRequest>(), null, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task FlacStrategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
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
                    OutputPath: "/out/track.flac",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: null
                )
            );
        FlacStrategy strategy = new(
            encoder: encoderMock.Object,
            logger: NullLogger<FlacStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: new(InputPath: "/in.wav", OutputDirectory: "/out", Profile: null!),
            progress: null,
            ct: CancellationToken.None
        );

        result.OutputPath.Should().Be(expected: "/out/track.flac");
    }

    [Fact]
    public async Task OggStrategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
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
                    OutputPath: "/out/album.ogg",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: null
                )
            );
        OggStrategy strategy = new(
            encoder: encoderMock.Object,
            logger: NullLogger<OggStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: new(InputPath: "/in.flac", OutputDirectory: "/out", Profile: null!),
            progress: null,
            ct: CancellationToken.None
        );

        result.OutputPath.Should().Be(expected: "/out/album.ogg");
    }
}
