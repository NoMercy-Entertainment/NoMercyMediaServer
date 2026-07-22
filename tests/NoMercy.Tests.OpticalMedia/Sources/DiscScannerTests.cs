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
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class DiscScannerTests
{
    private static DiscScanner MakeSut(IProcessRunner? runner = null)
    {
        EncoderOptions options = new() { FfprobePathOverride = "ffprobe" };
        IProcessRunner processRunner = runner ?? Moq.Mock.Of<IProcessRunner>();
        return new(options: options, processRunner: processRunner, logger: NullLogger<DiscScanner>.Instance);
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsSingleEmptyTitle()
    {
        string json = """{"format":{}}""";
        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Type.Should().Be(expected: OpticalDiscType.BluRay);
        result.Titles.Should().HaveCount(expected: 1);
        result.Titles[0].Duration.Should().Be(expected: TimeSpan.Zero);
        result.TotalDuration.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public void Parse_WithValidTitle_ReturnsSingleTitleWithDuration()
    {
        string json = """
            {
              "format": {
                "tags": { "title": "Test Movie" },
                "duration": "7200.5"
              }
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.Dvd);

        result.Titles.Should().HaveCount(expected: 1);
        result.Titles[0].Name.Should().Be(expected: "Test Movie");
        result
            .Titles[0]
            .Duration.Should()
            .BeCloseTo(nearbyTime: TimeSpan.FromSeconds(value: 7200.5), precision: TimeSpan.FromMilliseconds(milliseconds: 1));
        result.Type.Should().Be(expected: OpticalDiscType.Dvd);
    }

    [Fact]
    public void Parse_WithVideoStreams_ParsesCodecAndResolution()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080,
                  "pix_fmt": "yuv420p"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().HaveCount(expected: 1);
        result.Titles[0].VideoStreams[0].Codec.Should().Be(expected: "h264");
        result.Titles[0].VideoStreams[0].Width.Should().Be(expected: 1920);
        result.Titles[0].VideoStreams[0].Height.Should().Be(expected: 1080);
        result.Titles[0].VideoStreams[0].PixelFormat.Should().Be(expected: "yuv420p");
    }

    [Fact]
    public void Parse_WithAudioStreams_ParsesCodecChannelsAndLanguage()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000",
                  "tags": { "language": "eng" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.Dvd);

        result.Titles[0].AudioStreams.Should().HaveCount(expected: 1);
        result.Titles[0].AudioStreams[0].Codec.Should().Be(expected: "ac3");
        result.Titles[0].AudioStreams[0].Channels.Should().Be(expected: 6);
        result.Titles[0].AudioStreams[0].SampleRate.Should().Be(expected: 48000);
        result.Titles[0].AudioStreams[0].Language.Should().Be(expected: "eng");
    }

    [Fact]
    public void Parse_WithSubtitleStreams_ParsesCodecAndLanguage()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 2,
                  "codec_type": "subtitle",
                  "codec_name": "dvd_subtitle",
                  "tags": { "language": "eng" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.Dvd);

        result.Titles[0].Subtitles.Should().HaveCount(expected: 1);
        result.Titles[0].Subtitles[0].Codec.Should().Be(expected: "dvd_subtitle");
        result.Titles[0].Subtitles[0].Language.Should().Be(expected: "eng");
    }

    [Fact]
    public void Parse_WithMultipleAudioTracks_AllParsed()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000",
                  "tags": { "language": "eng" }
                },
                {
                  "index": 2,
                  "codec_type": "audio",
                  "codec_name": "aac",
                  "channels": 2,
                  "sample_rate": "48000",
                  "tags": { "language": "fra" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].AudioStreams.Should().HaveCount(expected: 2);
        result.Titles[0].AudioStreams[0].Language.Should().Be(expected: "eng");
        result.Titles[0].AudioStreams[1].Language.Should().Be(expected: "fra");
    }

    [Fact]
    public void Parse_WithChapters_ParsesStartEndAndTitle()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "chapters": [
                {
                  "start_time": "0.0",
                  "end_time": "600.0",
                  "tags": { "title": "Prologue" }
                },
                {
                  "start_time": "600.0",
                  "end_time": "3600.0",
                  "tags": { "title": "Main Feature" }
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.Dvd);

        result.Titles[0].Chapters.Should().HaveCount(expected: 2);
        result.Titles[0].Chapters[0].Start.Should().Be(expected: TimeSpan.Zero);
        result.Titles[0].Chapters[0].Title.Should().Be(expected: "Prologue");
        result.Titles[0].Chapters[1].Start.Should().Be(expected: TimeSpan.FromSeconds(seconds: 600));
        result.Titles[0].Chapters[1].Title.Should().Be(expected: "Main Feature");
    }

    [Fact]
    public void Parse_WithoutLanguageTags_AudioStreamLanguageIsNull()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 1,
                  "codec_type": "audio",
                  "codec_name": "ac3",
                  "channels": 6,
                  "sample_rate": "48000"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].AudioStreams[0].Language.Should().BeNull();
    }

    [Fact]
    public void Parse_WithoutChapterTags_ChapterTitleIsNull()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "chapters": [
                {
                  "start_time": "0.0",
                  "end_time": "600.0"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.Dvd);

        result.Titles[0].Chapters[0].Title.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJsonThrows()
    {
        string json = """{ invalid json }""";

        Action act = () => DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*ffprobe output was not valid JSON*");
    }

    [Fact]
    public void Parse_MalformedDurationString_ParsesAsZero()
    {
        string json = """
            {
              "format": { "duration": "not_a_number" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].Duration.Should().Be(expected: TimeSpan.Zero);
        result.Titles[0].VideoStreams[0].Width.Should().Be(expected: 1920);
        result.Titles[0].VideoStreams[0].Height.Should().Be(expected: 1080);
    }

    [Fact]
    public void Parse_UnknownCodecTypeIsIgnored()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "data"
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().BeEmpty();
        result.Titles[0].AudioStreams.Should().BeEmpty();
        result.Titles[0].Subtitles.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SetsIsMainFeatureToTrue()
    {
        string json = """{"format": {"duration": "3600"}}""";

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].IsMainFeature.Should().BeTrue();
    }

    [Fact]
    public void Parse_SetsIndexToZero()
    {
        string json = """{"format": {"duration": "3600"}}""";

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].Index.Should().Be(expected: 0);
    }

    [Theory]
    [InlineData(data: OpticalDiscType.BluRay)]
    [InlineData(data: OpticalDiscType.Dvd)]
    [InlineData(data: OpticalDiscType.Cd)]
    public void Parse_DiscTypePassedThrough(OpticalDiscType type)
    {
        string json = """{"format":{}}""";

        DiscInfo result = DiscScanner.Parse(json: json, discType: type);

        result.Type.Should().Be(expected: type);
    }

    [Fact]
    public void Parse_EstimatedSizeSetToZero()
    {
        string json = """{"format":{"duration":"3600"}}""";

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].EstimatedSizeBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Parse_MultipleVideoStreamsAllParsed()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [
                {
                  "index": 0,
                  "codec_type": "video",
                  "codec_name": "h264",
                  "width": 1920,
                  "height": 1080
                },
                {
                  "index": 1,
                  "codec_type": "video",
                  "codec_name": "hevc",
                  "width": 3840,
                  "height": 2160
                }
              ]
            }
            """;

        DiscInfo result = DiscScanner.Parse(json: json, discType: OpticalDiscType.BluRay);

        result.Titles[0].VideoStreams.Should().HaveCount(expected: 2);
        result.Titles[0].VideoStreams[0].Codec.Should().Be(expected: "h264");
        result.Titles[0].VideoStreams[1].Codec.Should().Be(expected: "hevc");
    }

    // ── ScanAsync — end to end via mocked IProcessRunner ──────────────────

    [Fact]
    public async Task ScanAsync_NonBlurayPath_SkipsPreProbe_ScansDirectly()
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
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: """{"format":{"duration":"3600"}}""", StdErr: "", Duration: TimeSpan.Zero)
            );

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);

        result.Type.Should().Be(expected: OpticalDiscType.Dvd);
        result.Titles.Should().HaveCount(expected: 1);
        // Only one ffprobe call for a non-Blu-ray path — no 1s pre-probe.
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task ScanAsync_BlurayPath_RunsPreProbeThenFullScan()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)) // pre-probe
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: """{"format":{"duration":"5400"}}""", StdErr: "", Duration: TimeSpan.Zero)
            ); // full scan

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "bluray:/dev/sr0", ct: CancellationToken.None);

        result.Type.Should().Be(expected: OpticalDiscType.BluRay);
        result.Titles[0].Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 5400));
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 2)
        );
    }

    [Fact]
    public async Task ScanAsync_BlurayPreProbeDetectsAacsFailure_ThrowsRuntimeException()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "aacs: no matching certificate", Duration: TimeSpan.Zero));

        DiscScanner sut = MakeSut(runner: runner.Object);

        Func<Task> act = () => sut.ScanAsync(drivePath: "bluray:/dev/sr0", ct: CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ScanAsync_FfprobeExitsNonZero_ReturnsEmptyTitlesDiscInfo()
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "no such file", Duration: TimeSpan.Zero));

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);

        result.Titles.Should().BeEmpty();
        result.DiscLabel.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_MalformedJsonFromFfprobe_ReturnsEmptyTitlesDiscInfo_WithoutThrowing()
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

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);

        result.Titles.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_BlurayPreProbeTimesOut_ProceedsToFullScanAnyway()
    {
        // The 1-second pre-probe uses its own linked CTS; a timeout there
        // (OperationCanceledException where the OUTER token is NOT itself
        // cancelled) must be swallowed and the real scan must still run.
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
            .ThrowsAsync(exception: new OperationCanceledException(message: "pre-probe timed out"))
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: """{"format":{"duration":"1800"}}""", StdErr: "", Duration: TimeSpan.Zero)
            );

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "bluray:/dev/sr0", ct: CancellationToken.None);

        result.Titles[0].Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 1800));
    }

    // ── ClassifyBluRayStderr — direct unit tests (pure function) ───────────

    [Fact]
    public void ClassifyBluRayStderr_EmptyStderr_DoesNotThrow()
    {
        Action act = () => DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: "");
        act.Should().NotThrow();
    }

    [Fact]
    public void ClassifyBluRayStderr_AacsNoMatchingCertificate_ThrowsDiscAacsCertMissing()
    {
        Action act = () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: "aacs: no matching certificate");

        act.Should().Throw<EncoderRuntimeException>();
    }

    [Fact]
    public void ClassifyBluRayStderr_BdplusNoMatchingConverter_ThrowsDiscBdplusConverterMissing()
    {
        Action act = () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: "bdplus: no matching converter");

        act.Should().Throw<EncoderRuntimeException>();
    }

    [Theory]
    [InlineData(data: "Protocol not found")]
    [InlineData(data: "No such file or directory")]
    [InlineData(data: "Input/output error")]
    public void ClassifyBluRayStderr_ProtocolLevelFailure_ThrowsDiscReadError(string stderrText)
    {
        Action act = () => DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: stderrText);

        act.Should().Throw<EncoderRuntimeException>();
    }

    [Fact]
    public void ClassifyBluRayStderr_UnrecognizedStderr_DoesNotThrow()
    {
        Action act = () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: "some unrelated ffmpeg warning");

        act.Should().NotThrow();
    }

    [Fact]
    public void ClassifyBluRayStderr_ExtractsVolumeIdFromHexString_IncludesItInException()
    {
        string stderr = "aacs: no matching certificate for volume 0123456789ABCDEF0123456789ABCDEF";

        Action act = () => DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: stderr);

        act.Should()
            .Throw<EncoderRuntimeException>()
            .Where(exceptionExpression: ex => ex.Message.Contains("0123456789ABCDEF0123456789ABCDEF"));
    }

    [Fact]
    public async Task ScanAsync_FfprobeFailsWithEmptyStderr_LogsNoStderrPlaceholderWithoutThrowing()
    {
        // Exercises DiscScanner's private TrimStderr("(no stderr)" branch) —
        // only reachable when a real ffprobe failure carries no stderr text
        // at all, distinct from the "no such file"-style failures used by
        // the sibling ScanAsync failure test above.
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "", Duration: TimeSpan.Zero));

        DiscScanner sut = MakeSut(runner: runner.Object);

        DiscInfo result = await sut.ScanAsync(drivePath: "/dev/sr0", ct: CancellationToken.None);

        result.Titles.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyBluRayStderr_NoHexVolumeId_FallsBackToDrivePath()
    {
        Action act = () =>
            DiscScanner.ClassifyBluRayStderr(drivePath: "/dev/sr0", stderr: "aacs: no matching certificate");

        act.Should().Throw<EncoderRuntimeException>().Where(exceptionExpression: ex => ex.Message.Contains("/dev/sr0"));
    }
}
