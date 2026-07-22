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
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Dvd;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class DvdDiscSourceTests
{
    [Fact]
    public void ClassifyProtection_NullStderr_ReturnsNull()
    {
        string stderr = "";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_CssAuthenticationFailed_ReturnsCssProtection()
    {
        string stderr = "libdvdcss: css authentication failed";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "CSS");
        result.Message.Should().Contain(expected: "CSS");
    }

    [Fact]
    public void ClassifyProtection_CouldNotGetKeyForAnyTitle_ReturnsCssProtection()
    {
        string stderr = "libdvdcss: could not get a key for any title";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "CSS");
    }

    [Fact]
    public void ClassifyProtection_RegionCodeMismatch_ReturnsRegionLockProtection()
    {
        string stderr = "libdvdread: region code mismatch";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "RegionLock");
        result.Message.Should().Contain(expected: "region");
    }

    [Fact]
    public void ClassifyProtection_CaseSensitivityIgnored()
    {
        string stderr = "CSS AUTHENTICATION FAILED";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "CSS");
    }

    [Fact]
    public void ClassifyProtection_CssFailureInMultilineStderr_Detected()
    {
        string stderr = """
            libdvdcss: opening /dev/dvd
            libdvdcss: css authentication failed
            libdvdread: error - could not read block 100
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "CSS");
    }

    [Fact]
    public void ClassifyProtection_RegionCodeInMultilineStderr_Detected()
    {
        string stderr = """
            libdvdread: opening DVD...
            libdvdread: region code mismatch
            Cannot read disc
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "RegionLock");
    }

    [Fact]
    public void ClassifyProtection_NoProtectionMarkers_ReturnsNull()
    {
        string stderr = """
            libdvdread: opening /dev/dvd
            libdvdread: reading VMG
            Playback initialized
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_CouldNotGetKeyAnyTitle_ContainsHelpfulMessage()
    {
        string stderr = "Could not get a key for any title";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Message.Should().Contain(expected: "key");
        result.Message.Should().Contain(expected: "libdvdcss");
    }

    [Fact]
    public void ClassifyProtection_RegionMessageSuggestsFix()
    {
        string stderr = "region code mismatch";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Message.Should().Contain(expected: "region");
        result.Message.Should().Contain(expected: "drive");
    }

    [Fact]
    public void ClassifyProtection_PrefersCssOverRegion()
    {
        string stderr = """
            libdvdcss: css authentication failed
            libdvdread: region code mismatch
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "CSS");
    }

    [Fact]
    public void ClassifyProtection_ChecksExactErrorPatterns()
    {
        string stderr = "authentication issue";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().BeNull();
    }
}

/// <summary>
/// End-to-end tests for <see cref="DvdDiscSource.ProbeAsync"/> and
/// <see cref="DvdDiscSource.ProbeTitleAsync"/> via a mocked
/// <see cref="IProcessRunner"/> — no real libdvdread/ffmpeg subprocess or
/// physical drive.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DvdDiscSourceEndToEndTests
{
    private static DvdDiscSource MakeSut(IProcessRunner? runner = null)
    {
        EncoderOptions options = new() { FfprobePathOverride = "ffprobe" };
        return new(
            options: options,
            processRunner: runner ?? Mock.Of<IProcessRunner>(),
            logger: NullLogger<DvdDiscSource>.Instance
        );
    }

    [Fact]
    public void Type_IsDvd()
    {
        MakeSut().Type.Should().Be(expected: OpticalDiscType.Dvd);
    }

    [Fact]
    public async Task ProbeAsync_WalksTitlesUntilBoundaryMessage_ReturnsAllFoundTitles()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .SetupSequence(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: """{"format":{"duration":"600"}}""", StdErr: "", Duration: TimeSpan.Zero)
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: """{"format":{"duration":"5400"}}""", StdErr: "", Duration: TimeSpan.Zero)
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Title 3 not found", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().HaveCount(expected: 2);
        // Longest title (5400s) is flagged main feature and sorted first.
        info.Titles[0].Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 5400));
        info.Titles[0].IsMainFeature.Should().BeTrue();
        info.Titles[1].IsMainFeature.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_NoTitlesFound_ReturnsEmptyDiscInfoWithProtection()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "css authentication failed", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().BeEmpty();
        info.Protection.Should().NotBeNull();
        info.Protection!.Kind.Should().Be(expected: "CSS");
    }

    [Fact]
    public async Task ProbeAsync_AlreadyDvdPrefixedDrivePath_UsedVerbatim()
    {
        Mock<IProcessRunner> runner = new();
        string? capturedInputUrl = null;
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
                action: (_, args, _, _) => capturedInputUrl ??= args[^1]
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Title 1 not found", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "dvd:/dev/sr0", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        capturedInputUrl
            .Should()
            .Be(expected: "dvd:/dev/sr0", because: "an already-prefixed path must be used verbatim");
    }

    [Fact]
    public async Task ProbeAsync_MalformedJsonForOneTitle_SkipsItAndContinuesWalking()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .SetupSequence(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "{ not valid json", StdErr: "", Duration: TimeSpan.Zero))
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Title 2 not found", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().BeEmpty(because: "the only title probed had malformed JSON and was skipped");
    }

    [Fact]
    public async Task ProbeAsync_EmptyStdout_SkipsIterationWithoutAddingTitle()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .SetupSequence(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero))
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Title 2 not found", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_MissingFormatProperty_SkipsIterationWithoutAddingTitle()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .SetupSequence(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "{}", StdErr: "", Duration: TimeSpan.Zero))
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Title 2 not found", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeTitleAsync_ValidTitle_ReturnsStreamsWithRequestedIndex()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [ { "index": 0, "codec_type": "audio", "codec_name": "ac3", "channels": 6 } ]
            }
            """;
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: json, StdErr: "", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 5, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 5);
        title.AudioStreams.Should().HaveCount(expected: 1);
    }

    [Fact]
    public async Task ProbeTitleAsync_FfprobeFails_ReturnsEmptySkeletonTitle()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "read error", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 7, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 7);
        title.Duration.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public async Task ProbeTitleAsync_MalformedJson_ReturnsEmptySkeletonTitle_WithoutThrowing()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "{ not valid json", StdErr: "", Duration: TimeSpan.Zero));

        DvdDiscSource sut = MakeSut(runner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.Dvd);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 9, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 9);
        title.Duration.Should().Be(expected: TimeSpan.Zero);
    }
}
