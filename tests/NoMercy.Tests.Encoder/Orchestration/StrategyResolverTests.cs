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
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;

namespace NoMercy.Tests.Encoder.Orchestration;

public class StrategyResolverTests
{
    [Fact]
    public void Resolve_MatchingFormatAndMode_ReturnsStrategy()
    {
        IEncodingStrategy hlsSingle = BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);
        IEncodingStrategy hlsTwo = BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.TwoPass);
        StrategyResolver resolver = new(strategies: [hlsSingle, hlsTwo]);

        IEncodingStrategy? resolved = resolver.Resolve(format: OutputFormat.Hls, mode: EncodeMode.TwoPass);

        Assert.Same(expected: hlsTwo, actual: resolved);
    }

    [Fact]
    public void Resolve_UnknownCombination_ReturnsNull()
    {
        StrategyResolver resolver = new(strategies: [BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.SinglePass)]);

        IEncodingStrategy? resolved = resolver.Resolve(format: OutputFormat.Dash, mode: EncodeMode.TwoPass);

        Assert.Null(@object: resolved);
    }

    [Fact]
    public void Resolve_LastRegistrationWins_PluginsOverrideBuiltIn()
    {
        IEncodingStrategy builtIn = BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);
        IEncodingStrategy plugin = BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);
        StrategyResolver resolver = new(strategies: [builtIn, plugin]);

        IEncodingStrategy? resolved = resolver.Resolve(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);

        Assert.Same(expected: plugin, actual: resolved);
    }

    [Fact]
    public void Resolve_EmptyStrategyList_ReturnsNull()
    {
        StrategyResolver resolver = new(strategies: []);

        IEncodingStrategy? resolved = resolver.Resolve(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);

        Assert.Null(@object: resolved);
    }

    [Fact]
    public void Resolve_MultipleFormats_ReturnsCorrectOne()
    {
        IEncodingStrategy hls = BuildStrategy(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);
        IEncodingStrategy mkv = BuildStrategy(format: OutputFormat.Mkv, mode: EncodeMode.SinglePass);
        IEncodingStrategy mp4 = BuildStrategy(format: OutputFormat.Mp4, mode: EncodeMode.SinglePass);
        IEncodingStrategy dash = BuildStrategy(format: OutputFormat.Dash, mode: EncodeMode.SinglePass);
        StrategyResolver resolver = new(strategies: [hls, mkv, mp4, dash]);

        Assert.Same(expected: hls, actual: resolver.Resolve(format: OutputFormat.Hls, mode: EncodeMode.SinglePass));
        Assert.Same(expected: mkv, actual: resolver.Resolve(format: OutputFormat.Mkv, mode: EncodeMode.SinglePass));
        Assert.Same(expected: mp4, actual: resolver.Resolve(format: OutputFormat.Mp4, mode: EncodeMode.SinglePass));
        Assert.Same(expected: dash, actual: resolver.Resolve(format: OutputFormat.Dash, mode: EncodeMode.SinglePass));
    }

    private static IEncodingStrategy BuildStrategy(OutputFormat format, EncodeMode mode)
    {
        Mock<IEncodingStrategy> mock = new();
        mock.Setup(expression: s => s.Format).Returns(value: format);
        mock.Setup(expression: s => s.EncodeMode).Returns(value: mode);
        mock.Setup(expression: s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "test", GpuUsed: null)
                )
            );
        return mock.Object;
    }
}
