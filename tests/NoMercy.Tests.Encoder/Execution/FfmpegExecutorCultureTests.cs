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

using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Execution;

/// <summary>
/// <see cref="EncodingProgress.Bitrate"/> rides the dashboard's SignalR
/// progress payload (see EventBusProgressObserver) as a pre-formatted string.
/// On a comma-decimal server locale a bare ":F1" bakes "1234,5kbits/s" into
/// that payload instead of "1234.5kbits/s" — this pins InvariantCulture on
/// the formatter.
/// </summary>
public class FfmpegExecutorCultureTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly FfmpegExecutor _executor;

    public FfmpegExecutorCultureTests()
    {
        _executor = new(_processRunner.Object, NullLogger<FfmpegExecutor>.Instance);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public async Task Progress_BitrateString_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            EncodingProgress? lastProgress = null;
            _processRunner
                .Setup(r =>
                    r.RunAsync(
                        It.IsAny<string>(),
                        It.IsAny<string[]>(),
                        It.IsAny<Action<string>?>(),
                        It.IsAny<Action<string>?>(),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<Action<int>?>()
                    )
                )
                .Callback<
                    string,
                    string[],
                    Action<string>?,
                    Action<string>?,
                    string?,
                    CancellationToken,
                    CancellationToken,
                    Action<int>?
                >(
                    (exe, args, onStdOut, onStdErr, dir, ct, kill, onStarted) =>
                    {
                        onStdOut?.Invoke("frame=100");
                        onStdOut?.Invoke("fps=30.0");
                        onStdOut?.Invoke("out_time_us=10000000");
                        onStdOut?.Invoke("bitrate=1234.5kbits/s");
                        onStdOut?.Invoke("speed=1.0x");
                        onStdOut?.Invoke("progress=end");
                    }
                )
                .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

            await _executor.ExecuteAsync(
                BuildSimpleCommand(),
                TimeSpan.FromMinutes(1),
                onProgress: p => lastProgress = p
            );

            lastProgress!.Bitrate.Should().Be("1234.5kbits/s");
            lastProgress.Bitrate.Should().NotContain(",");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static FfmpegCommand BuildSimpleCommand() =>
        new("ffmpeg", ["-i", "/input.mkv", "/output.mp4"], null);
}
