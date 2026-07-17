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
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// End-to-end arg-building and output-placement contract for
/// <see cref="SubtitleOcrEngine.OcrAsync"/>. Pins the two regressions that
/// shipped bitmap-subtitle OCR completely broken in production:
///
/// • Bug 1 — the tesseract <c>datapath=</c> option was never passed to the
///   <c>ocr</c> filter, so every run failed with "Error opening data file"
///   against the BUILD MACHINE's baked-in tessdata path. The datapath
///   assertions below must fail if that wiring is ever dropped again — a
///   mock that only checks the process exited 0 is fake coverage for this
///   regression.
/// • Bug 2 — the OCR .vtt was written next to the SOURCE file instead of the
///   encode output directory, so the post-encode library scan never
///   registered it. The output-directory assertions confirm the sidecar
///   lands under <c>{outputDirectory}/subtitles/</c> with a per-stream
///   unique filename, matching the naming the scan already discovers real
///   text-subtitle sidecars by.
/// </summary>
public class SubtitleOcrEngineOcrAsyncTests
{
    private const string FfmpegPath = "/usr/bin/ffmpeg";
    private const string InputPath = "/media/movie.mkv";
    private const string ModelDirectory = "/models/tessdata";

    // ── Datapath wiring (Bug 1 regression) ───────────────────────────────────

    [Fact]
    public async Task Filter_includes_datapath_pointing_at_model_directory()
    {
        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = CaptureArgsProcess(args => capturedArgs = args);
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(InputPath, 0, "eng", SubtitleCodecType.WebVtt, default);

        capturedArgs.Should().NotBeNull();
        int filterIndex = Array.IndexOf(capturedArgs!, "-filter_complex");
        filterIndex.Should().BeGreaterThan(-1);
        string filter = capturedArgs![filterIndex + 1];
        filter.Should().Contain("ocr=language=eng");
        filter.Should().Contain($"datapath={ModelDirectory}");
    }

    [Fact]
    public async Task Datapath_on_a_windows_model_directory_is_filter_escaped()
    {
        const string windowsModelDir = @"C:\ffmpeg_build\tessdata";
        string[]? capturedArgs = null;
        Mock<IProcessRunner> processRunner = CaptureArgsProcess(args => capturedArgs = args);
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = new();
        modelManager
            .Setup(m => m.EnsureLanguageModelAsync("eng", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Path.Combine(windowsModelDir, "eng.traineddata"));

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(InputPath, 0, "eng", SubtitleCodecType.WebVtt, default);

        int filterIndex = Array.IndexOf(capturedArgs!, "-filter_complex");
        string filter = capturedArgs![filterIndex + 1];
        filter.Should().Contain(@"datapath=C\\:/ffmpeg_build/tessdata");
    }

    // ── Output placement (Bug 2 regression) ──────────────────────────────────

    [Fact]
    public async Task No_output_directory_keeps_legacy_next_to_input_naming()
    {
        Mock<IProcessRunner> processRunner = SuccessProcess();
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            InputPath,
            0,
            "eng",
            SubtitleCodecType.WebVtt,
            default
        );

        track.FilePath.Should().Be(Path.Combine(Path.GetDirectoryName(InputPath)!, "eng_ocr.vtt"));
    }

    [Fact]
    public async Task Output_directory_lands_under_subtitles_subfolder()
    {
        const string outputDirectory = "/encoded/Show.S01E01";
        Mock<IProcessRunner> processRunner = SuccessProcess();
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            InputPath,
            2,
            "eng",
            SubtitleCodecType.WebVtt,
            default,
            outputDirectory
        );

        track.FilePath.Should().Be(Path.Combine(outputDirectory, "subtitles", "eng.ocr2.vtt"));
    }

    [Fact]
    public async Task Two_streams_sharing_a_language_produce_distinct_filenames()
    {
        const string outputDirectory = "/encoded/Show.S01E01";
        Mock<IProcessRunner> processRunner = SuccessProcess();
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack first = await engine.OcrAsync(
            InputPath,
            2,
            "eng",
            SubtitleCodecType.WebVtt,
            default,
            outputDirectory
        );
        SubtitleTrack second = await engine.OcrAsync(
            InputPath,
            5,
            "eng",
            SubtitleCodecType.WebVtt,
            default,
            outputDirectory
        );

        first.FilePath.Should().NotBe(second.FilePath);
        first.FilePath.Should().Be(Path.Combine(outputDirectory, "subtitles", "eng.ocr2.vtt"));
        second.FilePath.Should().Be(Path.Combine(outputDirectory, "subtitles", "eng.ocr5.vtt"));
    }

    [Fact]
    public async Task Srt_format_keeps_the_same_output_directory_placement()
    {
        const string outputDirectory = "/encoded/Show.S01E01";
        Mock<IProcessRunner> processRunner = SuccessProcess();
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            InputPath,
            1,
            "spa",
            SubtitleCodecType.Srt,
            default,
            outputDirectory
        );

        track.FilePath.Should().Be(Path.Combine(outputDirectory, "subtitles", "spa.ocr1.srt"));
    }

    // ── Failure surfacing ─────────────────────────────────────────────────────

    [Fact]
    public async Task Nonzero_exit_throws_with_real_stderr_in_the_message()
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProcessResult(
                    ExitCode: 1,
                    StdOut: "",
                    StdErr: "Error opening data file /ffmpeg_build/windows/share/tessdata/eng.traineddata",
                    Duration: TimeSpan.Zero
                )
            );
        Mock<IStorage> storage = StorageMock();
        Mock<ITesseractModelManager> modelManager = ModelManagerMock();

        SubtitleOcrEngine engine = new(
            Options(),
            processRunner.Object,
            modelManager.Object,
            storage.Object,
            NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine
            .Invoking(e => e.OcrAsync(InputPath, 0, "eng", SubtitleCodecType.WebVtt, default))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*eng.traineddata*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EncoderOptions Options() => new() { FfmpegPathOverride = FfmpegPath };

    private static Mock<ITesseractModelManager> ModelManagerMock()
    {
        Mock<ITesseractModelManager> modelManager = new();
        modelManager
            .Setup(m =>
                m.EnsureLanguageModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Path.Combine(ModelDirectory, "eng.traineddata"));
        return modelManager;
    }

    private static Mock<IStorage> StorageMock()
    {
        Mock<IStorage> storage = new();
        storage.Setup(s => s.AcquireLocalPath(It.IsAny<string>())).Returns<string>(p => new(p));
        storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
        storage
            .Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("pts_time:0\nlavfi.ocr.text=hi\n"));
        return storage;
    }

    private static Mock<IProcessRunner> SuccessProcess()
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        return processRunner;
    }

    private static Mock<IProcessRunner> CaptureArgsProcess(Action<string[]> capture)
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                Action<string>?,
                Action<string>?,
                string?,
                CancellationToken
            >((_, args, _, _, _, _) => capture(args))
            .ReturnsAsync(
                new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        return processRunner;
    }
}
