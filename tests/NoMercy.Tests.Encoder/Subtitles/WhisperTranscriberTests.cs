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

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// Dispatch + arg-shape contract for <see cref="WhisperTranscriber"/>. This
/// class is the seam between the encoder pipeline and the whisper.cpp ffmpeg
/// filter, and the filter syntax is unforgiving — a missing colon escape or
/// a wrong key=value pair silently produces an empty subtitle file with zero
/// cues.
///
/// Each category below pins a specific failure mode:
///
/// • Model resolution — explicit override beats EncoderOptions; missing both
///   throws InvalidOperationException; non-existent file throws
///   FileNotFoundException so callers can surface a clear setup error.
/// • Filter arg construction — model path / language / queue / destination /
///   format must all land in the filter, in the literal `key=value` shape
///   ffmpeg parses. Translate flag toggles `:translate=1`.
/// • Filter path escaping — backslashes become slashes (cross-OS canonical),
///   colons get backslash-escaped (the filter graph treats colon as the
///   key/value separator).
/// • Process failure surfacing — non-zero exit code throws so the pipeline
///   doesn't continue with an unwritten subtitle file.
/// • Output verification — even on exit 0, the subtitle file must actually
///   exist; whisper has hung-but-clean-exit modes that produce nothing.
/// • Cue counting — counts "-->" lines in the produced SRT, the WebVTT/SRT
///   timing marker.
/// </summary>
public class WhisperTranscriberTests
{
    private const string ModelPath = "/models/ggml-large-v3.bin";
    private const string FfmpegPath = "/usr/bin/ffmpeg";
    private const string InputPath = "/media/movie.mkv";

    // ── Model resolution ─────────────────────────────────────────────────────

    [Fact]
    public async Task Throws_when_no_model_path_configured_and_no_override()
    {
        EncoderOptions options = new() { FfmpegPathOverride = FfmpegPath, WhisperModelPath = null };
        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: Mock.Of<IProcessRunner>(),
            storage: Mock.Of<IStorage>(),
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber
            .Invoking(action: t =>
                t.TranscribeAsync(inputPath: InputPath, audioStreamIndex: 0, language: "eng", options_: null, progress: null, ct: default)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*WhisperModelPath is not configured*");
    }

    [Fact]
    public async Task Throws_FileNotFound_when_model_does_not_exist_on_storage()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(ModelPath)).Returns(value: false);

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: Mock.Of<IProcessRunner>(),
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber
            .Invoking(action: t =>
                t.TranscribeAsync(inputPath: InputPath, audioStreamIndex: 0, language: "eng", options_: null, progress: null, ct: default)
            )
            .Should()
            .ThrowAsync<FileNotFoundException>()
            .WithMessage(expectedWildcardPattern: "*Whisper model not found*");
    }

    [Fact]
    public async Task Override_ModelPath_takes_precedence_over_encoder_options()
    {
        // WhisperOptions.ModelPath beats EncoderOptions.WhisperModelPath when both set.
        const string overridePath = "/override/custom-model.bin";
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: overridePath);
        Mock<IProcessRunner> processRunner = SuccessProcess();
        InMemoryStream(storage: storage, path: GetExpectedOutputPath(language: "eng"), srtContent: "");

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: new(ModelPath: overridePath),
            progress: null,
            ct: default
        );

        // Verify Exists was called with the OVERRIDE, not the default path.
        storage.Verify(expression: s => s.Exists(overridePath), times: Times.AtLeastOnce);
        storage.Verify(expression: s => s.Exists(ModelPath), times: Times.Never);
    }

    // ── Filter arg construction ──────────────────────────────────────────────

    [Fact]
    public async Task Builds_ffmpeg_args_with_input_map_and_filter()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        InMemoryStream(
            storage: storage,
            path: GetExpectedOutputPath(language: "eng"),
            srtContent: "1\n00:00:01,000 --> 00:00:02,000\nhi\n"
        );

        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    FfmpegPath,
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedArgs = args
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 3,
            language: "fre",
            options_: null,
            progress: null,
            ct: default
        );

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Should().ContainInOrder(expected: ["-i", InputPath]);
        capturedArgs.Should().ContainInOrder(expected: ["-map", "0:a:3"]); // streamIndex 3
        capturedArgs.Should().Contain(expected: "-vn"); // discard video
        capturedArgs.Should().ContainInOrder(expected: ["-f", "null"]); // discard ffmpeg's own output
        int afIndex = Array.IndexOf(array: capturedArgs, value: "-af");
        afIndex.Should().BeGreaterThan(expected: -1);
        string filter = capturedArgs[afIndex + 1];
        filter.Should().Contain(expected: "whisper=model=");
        filter.Should().Contain(expected: ":language=fre");
        filter.Should().Contain(expected: ":queue=3");
        filter.Should().Contain(expected: ":format=srt");
        filter.Should().NotContain(unexpected: "translate"); // default off
    }

    [Fact]
    public async Task Translate_option_adds_translate_to_filter()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        InMemoryStream(storage: storage, path: GetExpectedOutputPath(language: "jpn"), srtContent: "");

        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    FfmpegPath,
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedArgs = args
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "jpn",
            options_: new(ModelPath: ModelPath, TranslateToEnglish: true),
            progress: null,
            ct: default
        );

        int afIndex = Array.IndexOf(array: capturedArgs!, value: "-af");
        string filter = capturedArgs![afIndex + 1];
        filter.Should().Contain(expected: ":translate=1");
    }

    // ── Path escaping ────────────────────────────────────────────────────────

    [Fact]
    public async Task Filter_paths_have_backslashes_replaced_with_forward_slashes()
    {
        // EscapeFilterPath normalizes Windows-style backslashes — ffmpeg filter
        // graph syntax doesn't recognize backslash separators.
        const string windowsModelPath = @"C:\models\ggml.bin";
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = windowsModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: windowsModelPath);
        InMemoryStream(storage: storage, path: GetExpectedOutputPath(language: "eng"), srtContent: "");

        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    FfmpegPath,
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedArgs = args
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: null,
            progress: null,
            ct: default
        );

        int afIndex = Array.IndexOf(array: capturedArgs!, value: "-af");
        string filter = capturedArgs![afIndex + 1];
        filter.Should().NotContain(unexpected: @"\models"); // backslash gone
        filter.Should().Contain(expected: "/models/"); // forward slashes present
    }

    [Fact]
    public async Task Filter_paths_have_colons_backslash_escaped()
    {
        // Filter graph syntax uses `:` to separate key=value pairs — any colon
        // in a path value must be backslash-escaped to avoid premature parsing.
        const string windowsModelPath = @"C:\models\ggml.bin";
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = windowsModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: windowsModelPath);
        InMemoryStream(storage: storage, path: GetExpectedOutputPath(language: "eng"), srtContent: "");

        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    FfmpegPath,
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedArgs = args
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: null,
            progress: null,
            ct: default
        );

        int afIndex = Array.IndexOf(array: capturedArgs!, value: "-af");
        string filter = capturedArgs![afIndex + 1];
        filter.Should().Contain(expected: @"C\:/models"); // drive-letter colon escaped
    }

    // ── Process failure surfacing ────────────────────────────────────────────

    [Fact]
    public async Task Throws_when_process_exits_non_zero()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(
                    ExitCode: 137,
                    StdOut: "",
                    StdErr: "killed",
                    Duration: TimeSpan.Zero
                )
            );

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber
            .Invoking(action: t =>
                t.TranscribeAsync(inputPath: InputPath, audioStreamIndex: 0, language: "eng", options_: null, progress: null, ct: default)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*exited with code 137*");
    }

    [Fact]
    public async Task Throws_when_output_file_missing_after_clean_exit()
    {
        // Whisper has been observed to exit cleanly while producing no output —
        // we must surface that as a clear error, not return a SubtitleTrack
        // pointing at a nonexistent file.
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(ModelPath)).Returns(value: true);
        storage.Setup(expression: s => s.Exists(It.Is<string>(p => p.EndsWith(".srt")))).Returns(value: false);
        storage
            .Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => new(path: p));
        Mock<IProcessRunner> processRunner = SuccessProcess();

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber
            .Invoking(action: t =>
                t.TranscribeAsync(inputPath: InputPath, audioStreamIndex: 0, language: "eng", options_: null, progress: null, ct: default)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*produced no output*");
    }

    // ── Cue counting + return shape ──────────────────────────────────────────

    [Fact]
    public async Task Returns_subtitle_track_with_srt_format_and_counted_cues()
    {
        // CountCuesIn counts "-->" lines — three timing markers = 3 cues.
        const string srt = """
            1
            00:00:01,000 --> 00:00:02,000
            line one

            2
            00:00:02,500 --> 00:00:03,000
            line two

            3
            00:00:04,000 --> 00:00:05,000
            line three
            """;
        string outputPath = GetExpectedOutputPath(language: "eng");

        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        InMemoryStream(storage: storage, path: outputPath, srtContent: srt);
        Mock<IProcessRunner> processRunner = SuccessProcess();

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        SubtitleTrack track = await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: null,
            progress: null,
            ct: default
        );

        track.FilePath.Should().Be(expected: outputPath);
        track.Language.Should().Be(expected: "eng");
        track.Format.Should().Be(expected: SubtitleCodecType.Srt);
        track.CueCount.Should().Be(expected: 3);
    }

    [Fact]
    public async Task Returns_zero_cues_when_srt_has_no_timing_markers()
    {
        const string emptySrt = "no markers here\n";
        string outputPath = GetExpectedOutputPath(language: "eng");

        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        InMemoryStream(storage: storage, path: outputPath, srtContent: emptySrt);
        Mock<IProcessRunner> processRunner = SuccessProcess();

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        SubtitleTrack track = await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: null,
            progress: null,
            ct: default
        );

        track.CueCount.Should().Be(expected: 0);
    }

    // ── Progress observer wiring ─────────────────────────────────────────────

    [Fact]
    public async Task Progress_observer_receives_stage_started_and_completed()
    {
        EncoderOptions options = new()
        {
            FfmpegPathOverride = FfmpegPath,
            WhisperModelPath = ModelPath,
        };
        Mock<IStorage> storage = StorageMock(modelPath: ModelPath);
        InMemoryStream(storage: storage, path: GetExpectedOutputPath(language: "eng"), srtContent: "");
        Mock<IProcessRunner> processRunner = SuccessProcess();
        Mock<IProgressObserver> progress = new();

        WhisperTranscriber transcriber = new(
            options: options,
            processRunner: processRunner.Object,
            storage: storage.Object,
            logger: NullLogger<WhisperTranscriber>.Instance
        );

        await transcriber.TranscribeAsync(
            inputPath: InputPath,
            audioStreamIndex: 0,
            language: "eng",
            options_: null,
            progress: progress.Object,
            ct: default
        );

        progress.Verify(
            expression: p => p.OnStageStarted(It.Is<string>(s => s.Contains("Whisper") && s.Contains("eng"))),
            times: Times.Once
        );
        progress.Verify(
            expression: p =>
                p.OnStageCompleted(It.Is<string>(s => s.Contains("Whisper")), It.IsAny<TimeSpan>()),
            times: Times.Once
        );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetExpectedOutputPath(string language)
    {
        string outputDir = Path.GetDirectoryName(path: InputPath)!;
        string outputName = $"{Path.GetFileNameWithoutExtension(path: InputPath)}.{language}.whisper";
        return Path.Combine(path1: outputDir, path2: $"{outputName}.srt");
    }

    private static Mock<IStorage> StorageMock(string modelPath)
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(modelPath)).Returns(value: true);
        storage.Setup(expression: s => s.Exists(It.Is<string>(p => p.EndsWith(".srt")))).Returns(value: true);
        storage
            .Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => new(path: p));
        return storage;
    }

    private static Mock<IProcessRunner> SuccessProcess()
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        return processRunner;
    }

    private static void InMemoryStream(Mock<IStorage> storage, string path, string srtContent)
    {
        // Path.Combine separator varies by OS — mock by suffix instead of exact path
        // so the test passes regardless of host.
        storage
            .Setup(expression: s => s.OpenRead(It.Is<string>(p => p.EndsWith(".srt"))))
            .Returns(valueFunction: () => new MemoryStream(buffer: Encoding.UTF8.GetBytes(s: srtContent)));
    }
}
