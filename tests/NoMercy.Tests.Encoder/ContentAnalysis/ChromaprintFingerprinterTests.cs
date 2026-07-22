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
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Monitoring;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.ContentAnalysis;

/// <summary>
/// The chromaprint-backed fingerprinter parses ffmpeg stdout; these tests
/// pin that parser and the command-line shape. Running real ffmpeg in unit
/// tests would require bundled fixtures and network / binary assumptions
/// that don't belong here.
/// </summary>
public class ChromaprintFingerprinterTests
{
    private readonly EncoderOptions _options = new()
    {
        FfmpegPathOverride = "/usr/bin/ffmpeg",
        FfprobePathOverride = "/usr/bin/ffprobe",
    };

    [Fact]
    public void ParseRawOutput_CommaSeparatedHashes_ExtractsAllValues()
    {
        uint[] result = ChromaprintFingerprinter.ParseRawOutput(stdout: "123,456,789\n");

        result.Should().Equal(elements: [123u, 456u, 789u]);
    }

    [Fact]
    public void ParseRawOutput_PrefixedWithEquals_StripsLabel()
    {
        // Some ffmpeg builds emit "CHROMAPRINT=1,2,3" — the parser must
        // drop everything up to and including the '=' sign.
        uint[] result = ChromaprintFingerprinter.ParseRawOutput(stdout: "CHROMAPRINT=1,2,3");

        result.Should().Equal(elements: [1u, 2u, 3u]);
    }

    [Fact]
    public void ParseRawOutput_NegativeInts_HandledAsUnsigned()
    {
        // Chromaprint hashes are 32-bit; signed values wrap to the high
        // bits of uint. -1 → 0xFFFFFFFF.
        uint[] result = ChromaprintFingerprinter.ParseRawOutput(stdout: "-1,-2");

        result[0].Should().Be(expected: 0xFFFFFFFFu);
        result[1].Should().Be(expected: 0xFFFFFFFEu);
    }

    [Fact]
    public void ParseRawOutput_Empty_ReturnsEmptyArray()
    {
        ChromaprintFingerprinter.ParseRawOutput(stdout: "").Should().BeEmpty();
    }

    [Fact]
    public void ParseRawOutput_Whitespace_ReturnsEmptyArray()
    {
        ChromaprintFingerprinter.ParseRawOutput(stdout: "   \n\t").Should().BeEmpty();
    }

    [Fact]
    public async Task FingerprintAsync_AppliesWindowArgs()
    {
        string[]? captured = null;
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => captured = args
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "1,2,3\n", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 500)));

        ChromaprintFingerprinter fp = new(
            options: _options,
            processRunner: runner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<ChromaprintFingerprinter>.Instance,
            activityMonitor: new MediaActivityMonitor()
        );

        FingerprintWindow window = new(Start: TimeSpan.FromSeconds(seconds: 10), Duration: TimeSpan.FromSeconds(seconds: 300));
        AudioFingerprint result = await fp.FingerprintAsync(
            filePath: "/media/ep1.mkv",
            window: window,
            ct: CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.Should().Contain(expected: "-ss");
        captured.Should().Contain(expected: "10.000");
        captured.Should().Contain(expected: "-t");
        captured.Should().Contain(expected: "300.000");
        captured.Should().Contain(expected: "-f");
        captured.Should().Contain(expected: "chromaprint");
        result.Hashes.Should().Equal(elements: [1u, 2u, 3u]);
        result.StartTime.Should().Be(expected: TimeSpan.FromSeconds(seconds: 10));
    }

    [Fact]
    public async Task FingerprintAsync_NegativeWindowStart_UsesSseofNotSs()
    {
        // A negative Start (e.g. the outro window, -4min) means "relative to
        // the end of file" — -ss only accepts a non-negative offset from the
        // start, so ffmpeg either ignores it or errors. The end-relative case
        // must use -sseof, which accepts the same negative value.
        string[]? captured = null;
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => captured = args
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "1,2,3\n", StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 500)));

        ChromaprintFingerprinter fp = new(
            options: _options,
            processRunner: runner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<ChromaprintFingerprinter>.Instance,
            activityMonitor: new MediaActivityMonitor()
        );

        FingerprintWindow window = new(Start: TimeSpan.FromMinutes(minutes: -4), Duration: TimeSpan.FromMinutes(minutes: 4));
        await fp.FingerprintAsync(filePath: "/media/ep1.mkv", window: window, ct: CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Should().NotContain(unexpected: "-ss");
        captured.Should().ContainInConsecutiveOrder(expected: ["-sseof", "-240.000"]);
        captured.Should().ContainInConsecutiveOrder(expected: ["-t", "240.000"]);
    }

    [Fact]
    public async Task FingerprintAsync_FailedExec_ReturnsEmptyPrint()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "ffmpeg barfed", Duration: TimeSpan.Zero));

        ChromaprintFingerprinter fp = new(
            options: _options,
            processRunner: runner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<ChromaprintFingerprinter>.Instance,
            activityMonitor: new MediaActivityMonitor()
        );

        AudioFingerprint result = await fp.FingerprintAsync(
            filePath: "/media/ep1.mkv",
            window: null,
            ct: CancellationToken.None
        );

        result.Hashes.Should().BeEmpty();
        result.StartTime.Should().Be(expected: TimeSpan.Zero);
    }
}
