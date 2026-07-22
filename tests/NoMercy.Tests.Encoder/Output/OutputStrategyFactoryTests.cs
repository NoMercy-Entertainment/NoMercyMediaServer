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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class OutputStrategyFactoryTests
{
    [Theory]
    [InlineData(data: [OutputFormat.Hls, typeof(HlsOutputStrategy)])]
    [InlineData(data: [OutputFormat.Mkv, typeof(MkvOutputStrategy)])]
    [InlineData(data: [OutputFormat.Mp4, typeof(Mp4OutputStrategy)])]
    [InlineData(data: [OutputFormat.Dash, typeof(DashOutputStrategy)])]
    [InlineData(data: [OutputFormat.Mp3, typeof(Mp3OutputStrategy)])]
    [InlineData(data: [OutputFormat.Flac, typeof(FlacOutputStrategy)])]
    [InlineData(data: [OutputFormat.Ogg, typeof(OggOutputStrategy)])]
    public void Resolve_BuiltInFormat_ReturnsMatchingStrategy(OutputFormat format, Type expected)
    {
        OutputStrategyFactory factory = new(strategies:
        [
            new HlsOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(storage: TestStorageFactory.CreateLocal()),
        ]);

        IOutputStrategy resolved = factory.Resolve(format: format);

        resolved.Should().BeOfType(expectedType: expected);
    }

    [Fact]
    public void Resolve_UnknownFormat_Throws()
    {
        OutputStrategyFactory factory = new(strategies:
        [
            new HlsOutputStrategy(storage: TestStorageFactory.CreateLocal()),
        ]);

        Action act = () => factory.Resolve(format: (OutputFormat)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resolve_PluginOverride_TakesPrecedenceOverBuiltIn()
    {
        // Last-registration-wins: plugin HLS strategy registered after built-in
        // should be preferred by the factory.
        FakeHlsStrategy pluginOverride = new();
        OutputStrategyFactory factory = new(strategies:
        [
            new HlsOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            pluginOverride,
        ]);

        IOutputStrategy resolved = factory.Resolve(format: OutputFormat.Hls);

        resolved.Should().BeSameAs(expected: pluginOverride);
    }

    private sealed class FakeHlsStrategy : IOutputStrategy
    {
        public OutputFormat Format => OutputFormat.Hls;

        public void ConfigureOutput(
            FfmpegCommandBuilder builder,
            OutputPlan plan,
            string outputDirectory
        ) { }

        public Task FinalizeAsync(
            string outputDirectory,
            OutputPlan plan,
            string mediaTitle,
            CancellationToken ct
        ) => Task.CompletedTask;

        public string[] GetOutputSubdirectories(OutputPlan plan) => [];
    }
}
