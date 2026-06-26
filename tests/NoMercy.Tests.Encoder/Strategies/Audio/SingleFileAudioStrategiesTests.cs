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

using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Audio;

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
        mock.Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
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
        Mp3Strategy strategy = new(MockEncoder());

        strategy.Format.Should().Be(OutputFormat.Mp3);
        strategy.EncodeMode.Should().Be(EncodeMode.SinglePass);
    }

    [Fact]
    public void FlacStrategy_DeclaresFlacSinglePass()
    {
        FlacStrategy strategy = new(MockEncoder());

        strategy.Format.Should().Be(OutputFormat.Flac);
        strategy.EncodeMode.Should().Be(EncodeMode.SinglePass);
    }

    [Fact]
    public void OggStrategy_DeclaresOggSinglePass()
    {
        OggStrategy strategy = new(MockEncoder());

        strategy.Format.Should().Be(OutputFormat.Ogg);
        strategy.EncodeMode.Should().Be(EncodeMode.SinglePass);
    }

    [Fact]
    public async Task Mp3Strategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out/song.mp3",
                    Duration: TimeSpan.FromSeconds(180),
                    Error: null,
                    Metrics: null
                )
            );
        Mp3Strategy strategy = new(encoderMock.Object);

        EncodingResult result = await strategy.EncodeAsync(
            new EncodingRequest("/in.flac", "/out", null!),
            progress: null,
            CancellationToken.None
        );

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be("/out/song.mp3");
        encoderMock.Verify(
            e => e.EncodeAsync(It.IsAny<EncodingRequest>(), null, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task FlacStrategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out/track.flac",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: null
                )
            );
        FlacStrategy strategy = new(encoderMock.Object);

        EncodingResult result = await strategy.EncodeAsync(
            new EncodingRequest("/in.wav", "/out", null!),
            progress: null,
            CancellationToken.None
        );

        result.OutputPath.Should().Be("/out/track.flac");
    }

    [Fact]
    public async Task OggStrategy_EncodeAsync_DelegatesToEncoder()
    {
        Mock<IEncoder> encoderMock = new();
        encoderMock
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out/album.ogg",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: null
                )
            );
        OggStrategy strategy = new(encoderMock.Object);

        EncodingResult result = await strategy.EncodeAsync(
            new EncodingRequest("/in.flac", "/out", null!),
            progress: null,
            CancellationToken.None
        );

        result.OutputPath.Should().Be("/out/album.ogg");
    }
}
