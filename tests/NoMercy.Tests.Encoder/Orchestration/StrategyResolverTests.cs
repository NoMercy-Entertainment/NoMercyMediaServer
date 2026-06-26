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
        IEncodingStrategy hlsSingle = BuildStrategy(OutputFormat.Hls, EncodeMode.SinglePass);
        IEncodingStrategy hlsTwo = BuildStrategy(OutputFormat.Hls, EncodeMode.TwoPass);
        StrategyResolver resolver = new([hlsSingle, hlsTwo]);

        IEncodingStrategy? resolved = resolver.Resolve(OutputFormat.Hls, EncodeMode.TwoPass);

        Assert.Same(hlsTwo, resolved);
    }

    [Fact]
    public void Resolve_UnknownCombination_ReturnsNull()
    {
        StrategyResolver resolver = new([BuildStrategy(OutputFormat.Hls, EncodeMode.SinglePass)]);

        IEncodingStrategy? resolved = resolver.Resolve(OutputFormat.Dash, EncodeMode.TwoPass);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_LastRegistrationWins_PluginsOverrideBuiltIn()
    {
        IEncodingStrategy builtIn = BuildStrategy(OutputFormat.Hls, EncodeMode.SinglePass);
        IEncodingStrategy plugin = BuildStrategy(OutputFormat.Hls, EncodeMode.SinglePass);
        StrategyResolver resolver = new([builtIn, plugin]);

        IEncodingStrategy? resolved = resolver.Resolve(OutputFormat.Hls, EncodeMode.SinglePass);

        Assert.Same(plugin, resolved);
    }

    [Fact]
    public void Resolve_EmptyStrategyList_ReturnsNull()
    {
        StrategyResolver resolver = new([]);

        IEncodingStrategy? resolved = resolver.Resolve(OutputFormat.Hls, EncodeMode.SinglePass);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_MultipleFormats_ReturnsCorrectOne()
    {
        IEncodingStrategy hls = BuildStrategy(OutputFormat.Hls, EncodeMode.SinglePass);
        IEncodingStrategy mkv = BuildStrategy(OutputFormat.Mkv, EncodeMode.SinglePass);
        IEncodingStrategy mp4 = BuildStrategy(OutputFormat.Mp4, EncodeMode.SinglePass);
        IEncodingStrategy dash = BuildStrategy(OutputFormat.Dash, EncodeMode.SinglePass);
        StrategyResolver resolver = new([hls, mkv, mp4, dash]);

        Assert.Same(hls, resolver.Resolve(OutputFormat.Hls, EncodeMode.SinglePass));
        Assert.Same(mkv, resolver.Resolve(OutputFormat.Mkv, EncodeMode.SinglePass));
        Assert.Same(mp4, resolver.Resolve(OutputFormat.Mp4, EncodeMode.SinglePass));
        Assert.Same(dash, resolver.Resolve(OutputFormat.Dash, EncodeMode.SinglePass));
    }

    private static IEncodingStrategy BuildStrategy(OutputFormat format, EncodeMode mode)
    {
        Mock<IEncodingStrategy> mock = new();
        mock.Setup(s => s.Format).Returns(format);
        mock.Setup(s => s.EncodeMode).Returns(mode);
        mock.Setup(s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );
        return mock.Object;
    }
}
