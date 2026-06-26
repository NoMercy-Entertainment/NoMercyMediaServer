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
        uint[] result = ChromaprintFingerprinter.ParseRawOutput("123,456,789\n");

        result.Should().Equal(123u, 456u, 789u);
    }

    [Fact]
    public void ParseRawOutput_PrefixedWithEquals_StripsLabel()
    {
        // Some ffmpeg builds emit "CHROMAPRINT=1,2,3" — the parser must
        // drop everything up to and including the '=' sign.
        uint[] result = ChromaprintFingerprinter.ParseRawOutput("CHROMAPRINT=1,2,3");

        result.Should().Equal(1u, 2u, 3u);
    }

    [Fact]
    public void ParseRawOutput_NegativeInts_HandledAsUnsigned()
    {
        // Chromaprint hashes are 32-bit; signed values wrap to the high
        // bits of uint. -1 → 0xFFFFFFFF.
        uint[] result = ChromaprintFingerprinter.ParseRawOutput("-1,-2");

        result[0].Should().Be(0xFFFFFFFFu);
        result[1].Should().Be(0xFFFFFFFEu);
    }

    [Fact]
    public void ParseRawOutput_Empty_ReturnsEmptyArray()
    {
        ChromaprintFingerprinter.ParseRawOutput("").Should().BeEmpty();
    }

    [Fact]
    public void ParseRawOutput_Whitespace_ReturnsEmptyArray()
    {
        ChromaprintFingerprinter.ParseRawOutput("   \n\t").Should().BeEmpty();
    }

    [Fact]
    public async Task FingerprintAsync_AppliesWindowArgs()
    {
        string[]? captured = null;
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                (_, args, _, _) => captured = args
            )
            .ReturnsAsync(new ProcessResult(0, "1,2,3\n", "", TimeSpan.FromMilliseconds(500)));

        ChromaprintFingerprinter fp = new(
            _options,
            runner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<ChromaprintFingerprinter>.Instance
        );

        FingerprintWindow window = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(300));
        AudioFingerprint result = await fp.FingerprintAsync(
            "/media/ep1.mkv",
            window,
            CancellationToken.None
        );

        captured.Should().NotBeNull();
        captured!.Should().Contain("-ss");
        captured.Should().Contain("10.000");
        captured.Should().Contain("-t");
        captured.Should().Contain("300.000");
        captured.Should().Contain("-f");
        captured.Should().Contain("chromaprint");
        result.Hashes.Should().Equal(1u, 2u, 3u);
        result.StartTime.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task FingerprintAsync_FailedExec_ReturnsEmptyPrint()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(1, "", "ffmpeg barfed", TimeSpan.Zero));

        ChromaprintFingerprinter fp = new(
            _options,
            runner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<ChromaprintFingerprinter>.Instance
        );

        AudioFingerprint result = await fp.FingerprintAsync(
            "/media/ep1.mkv",
            null,
            CancellationToken.None
        );

        result.Hashes.Should().BeEmpty();
        result.StartTime.Should().Be(TimeSpan.Zero);
    }
}
