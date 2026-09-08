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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.MediaProcessing.AudioAnalysis;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.AudioAnalysis;

/// <summary>
/// The command the analyzer builds, and what it makes of the output. ffmpeg is
/// mocked: the fixture lines are replayed through the same callbacks the real
/// process runner writes to.
/// </summary>
public class FfmpegAudioAnalyzerTests
{
    /// <summary>
    /// Locked deliberately. The order is load-bearing: beatdetect and its
    /// ametadata run ahead of loudnorm, which alters the signal and re-frames
    /// its output.
    /// </summary>
    private const string ExpectedFilterGraph =
        "beatdetect,ametadata=mode=print:file=-,keydetect,"
        + "aspectralstats=measure=centroid,silencedetect=n=-50dB:d=0.5,"
        + "loudnorm=print_format=json,ametadata=mode=print:file=-";

    private string[] _capturedArguments = [];

    private FfmpegAudioAnalyzer CreateAnalyzer(string stdOutFixture, string stdErrFixture)
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };

        Mock<IStorage> storage = new();
        storage
            .Setup(s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns((string path) => new LocalPathLease(path));

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    string[] arguments,
                    Action<string>? onStdOut,
                    Action<string>? onStdErr,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    _capturedArguments = arguments;

                    foreach (string line in ReadFixture(stdOutFixture))
                    {
                        onStdOut?.Invoke(line);
                    }

                    foreach (string line in ReadFixture(stdErrFixture))
                    {
                        onStdErr?.Invoke(line);
                    }

                    return Task.FromResult(
                        new ProcessResult(0, string.Empty, string.Empty, default)
                    );
                }
            );

        return new(
            options,
            runner.Object,
            storage.Object,
            NullLogger<FfmpegAudioAnalyzer>.Instance
        );
    }

    private static string[] ReadFixture(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "AudioAnalysis", "Fixtures", name);
        return File.ReadAllLines(path);
    }

    /// <summary>
    /// The version stamped on every stored row. Bumping it re-queues the library,
    /// so it must move with every change to what the pass measures.
    /// </summary>
    [Fact]
    public void TheAnalyzerVersionMatchesTheMetadataBeatGrid()
    {
        CreateAnalyzer("v1040-click-128-stdout.txt", "v1040-click-128-stderr.txt")
            .Version.Should()
            .Be(3);
    }

    [Fact]
    public async Task ItRunsOneDetectorPassAndPrintsMetadataRightAfterBeatdetect()
    {
        FfmpegAudioAnalyzer analyzer = CreateAnalyzer(
            "v1040-click-128-stdout.txt",
            "v1040-click-128-stderr.txt"
        );

        await analyzer.AnalyzeAsync("/music/track.flac", CancellationToken.None);

        int filterIndex = Array.IndexOf(_capturedArguments, "-af");

        filterIndex.Should().BeGreaterThan(-1);
        _capturedArguments[filterIndex + 1].Should().Be(ExpectedFilterGraph);
    }

    [Fact]
    public async Task ItReadsTheBeatGridFromTheMetadataTheGraphPrints()
    {
        FfmpegAudioAnalyzer analyzer = CreateAnalyzer(
            "v1040-click-128-stdout.txt",
            "v1040-click-128-stderr.txt"
        );

        AudioAnalysisResult? result = await analyzer.AnalyzeAsync(
            "/music/track.flac",
            CancellationToken.None
        );

        result.Should().NotBeNull();
        result!.BeatGridFromMetadata.Should().BeTrue();
        result.Bpm.Should().BeApproximately(128.0, 0.5);
        result.BeatOffsetMs.Should().NotBeNull();
        result.KeyName.Should().Be("C");
    }
}
