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
/// <see cref="SubtitleOcrEngine.OcrAsync"/>. Pins the regressions that shipped
/// bitmap-subtitle OCR completely broken in production:
///
/// • Bug 1 — the tesseract data dir and the metadata output file were placed
///   INSIDE the ffmpeg filtergraph (<c>datapath=C:/…</c>, <c>file=C:\…</c>). A
///   Windows drive colon inside a filtergraph value is unescapable — every
///   escaping attempt is parsed as an option separator — so the <c>ocr</c>
///   filter aborted at parse on every run and no <c>.vtt</c> was ever produced.
///   The data dir now rides on the <c>TESSDATA_PREFIX</c> env var and the
///   metadata file is a bare name resolved against the working directory, so no
///   colon path enters the graph. The assertions below fail if one leaks back.
///   (The prior test asserted the broken <c>datapath=C\\:/…</c> string was
///   built, never that ffmpeg could parse it — green while production was 100%
///   broken. That is the fake coverage this rewrite removes.)
/// • Bug 2 — the OCR .vtt was written next to the SOURCE file instead of the
///   encode output directory, so the post-encode library scan never registered
///   it. The sidecar assertions confirm it lands under
///   <c>{outputDirectory}/subtitles/</c>.
/// • Bug 3 — the sidecar was named <c>{lang}.ocr{streamIndex}.{ext}</c>. The
///   library scan keys subtitles by <c>{lang}.{type}</c> and pairs a bitmap
///   track with the text sidecar sharing its key, so the name has to be the
///   bitmap track's sibling — <c>{title}.{lang}.{variant}.{ext}</c>.
/// </summary>
public class SubtitleOcrEngineOcrAsyncTests
{
    private const string FfmpegPath = "/usr/bin/ffmpeg";
    private const string InputPath = "/media/movie.mkv";
    private const string ModelDirectory = "/models/tessdata";

    // ── Filtergraph carries no colon path (Bug 1 regression) ─────────────────

    [Fact]
    public async Task Tessdata_directory_rides_in_on_env_not_the_filtergraph()
    {
        string[]? args = null;
        IReadOnlyDictionary<string, string>? env = null;
        Mock<IProcessRunner> processRunner = CaptureProcess(
            capture: (a, e, _) =>
            {
                args = a;
                env = e;
            }
        );

        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: processRunner.Object,
            modelManager: ModelManagerMock().Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(inputPath: InputPath, streamIndex: 0, language: "eng", outputFormat: SubtitleCodecType.WebVtt, ct: default);

        string filter = Filter(args: args);
        filter.Should().Contain(expected: "ocr=language=eng");
        // datapath= put the model dir into the filtergraph; a drive colon there is
        // unescapable and aborts the parse — it must never come back.
        filter.Should().NotContain(unexpected: "datapath");
        env.Should().ContainKey(expected: "TESSDATA_PREFIX");
        // The prefix is the directory that holds the traineddata — the same value
        // the code derives from the model path, separator-normalized per platform.
        env!
            [key: "TESSDATA_PREFIX"]
            .Should()
            .Be(expected: Path.GetDirectoryName(path: Path.Combine(path1: ModelDirectory, path2: "eng.traineddata")));
    }

    [Fact]
    public async Task Windows_model_directory_never_enters_the_filtergraph()
    {
        const string windowsModelDir = @"C:\ffmpeg_build\tessdata";
        string[]? args = null;
        IReadOnlyDictionary<string, string>? env = null;
        Mock<IProcessRunner> processRunner = CaptureProcess(
            capture: (a, e, _) =>
            {
                args = a;
                env = e;
            }
        );
        Mock<ITesseractModelManager> modelManager = new();
        modelManager
            .Setup(expression: m => m.EnsureLanguageModelAsync("eng", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: Path.Combine(path1: windowsModelDir, path2: "eng.traineddata"));

        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: processRunner.Object,
            modelManager: modelManager.Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(inputPath: InputPath, streamIndex: 0, language: "eng", outputFormat: SubtitleCodecType.WebVtt, ct: default);

        string filter = Filter(args: args);
        // No drive letter and no backslash-escaped drive: the graph stays
        // colon-path-free so ffmpeg can parse it. The raw path rides on the env.
        filter.Should().NotContain(unexpected: "C:");
        filter.Should().NotContain(unexpected: @"C\");
        filter.Should().NotContain(unexpected: "datapath");
        env!
            [key: "TESSDATA_PREFIX"]
            .Should()
            .Be(expected: Path.GetDirectoryName(path: Path.Combine(path1: windowsModelDir, path2: "eng.traineddata")));
    }

    [Fact]
    public async Task Metadata_file_is_a_bare_name_resolved_against_the_working_directory()
    {
        string[]? args = null;
        string? workingDirectory = null;
        Mock<IProcessRunner> processRunner = CaptureProcess(
            capture: (a, _, w) =>
            {
                args = a;
                workingDirectory = w;
            }
        );

        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: processRunner.Object,
            modelManager: ModelManagerMock().Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(inputPath: InputPath, streamIndex: 0, language: "eng", outputFormat: SubtitleCodecType.WebVtt, ct: default);

        string filter = Filter(args: args);
        string fileValue = filter[(filter.IndexOf(value: "file=", comparisonType: StringComparison.Ordinal) + 5)..];
        // A bare name — no separators, no colon — so it can never reintroduce a
        // path into the graph. It lands in the working directory the runner is
        // handed, which must therefore be set.
        fileValue.Should().MatchRegex(regularExpression: @"^ocr-[0-9a-fA-F]+\.txt$");
        fileValue.Should().NotContain(unexpected: "/");
        fileValue.Should().NotContain(unexpected: @"\");
        fileValue.Should().NotContain(unexpected: ":");
        workingDirectory.Should().NotBeNullOrEmpty();
    }

    // ── Output placement (Bug 2 regression) ──────────────────────────────────

    [Fact]
    public async Task No_output_directory_keeps_legacy_next_to_input_naming()
    {
        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: SuccessProcess().Object,
            modelManager: ModelManagerMock().Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 0,
            language: "eng",
            outputFormat: SubtitleCodecType.WebVtt,
            ct: default
        );

        track.FilePath.Should().Be(expected: Path.Combine(path1: Path.GetDirectoryName(path: InputPath)!, path2: "eng_ocr.vtt"));
    }

    [Fact]
    public async Task Sidecar_is_named_as_the_bitmap_tracks_sibling()
    {
        Mock<IStorage> storage = StorageMock();
        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: SuccessProcess().Object,
            modelManager: ModelManagerMock().Object,
            storage: storage.Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 2,
            language: "eng",
            outputFormat: SubtitleCodecType.WebVtt,
            ct: default,
            sidecar: Sidecar(storage: storage.Object, variant: "full")
        );

        track
            .FilePath.Should()
            .Be(expected: "/encoded/Show.S01E01/subtitles/Show.S01E01.NoMercy.eng.full.vtt");
    }

    [Fact]
    public async Task Two_streams_sharing_a_language_are_separated_by_variant()
    {
        Mock<IStorage> storage = StorageMock();
        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: SuccessProcess().Object,
            modelManager: ModelManagerMock().Object,
            storage: storage.Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack full = await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 2,
            language: "eng",
            outputFormat: SubtitleCodecType.WebVtt,
            ct: default,
            sidecar: Sidecar(storage: storage.Object, variant: "full")
        );
        SubtitleTrack sign = await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 5,
            language: "eng",
            outputFormat: SubtitleCodecType.WebVtt,
            ct: default,
            sidecar: Sidecar(storage: storage.Object, variant: "sign")
        );

        full.FilePath.Should().NotBe(unexpected: sign.FilePath);
        full.FilePath.Should().EndWith(expected: "Show.S01E01.NoMercy.eng.full.vtt");
        sign.FilePath.Should().EndWith(expected: "Show.S01E01.NoMercy.eng.sign.vtt");
    }

    [Fact]
    public async Task Srt_format_keeps_the_same_sibling_naming()
    {
        Mock<IStorage> storage = StorageMock();
        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: SuccessProcess().Object,
            modelManager: ModelManagerMock().Object,
            storage: storage.Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        SubtitleTrack track = await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 1,
            language: "spa",
            outputFormat: SubtitleCodecType.Srt,
            ct: default,
            sidecar: Sidecar(storage: storage.Object, variant: "forced")
        );

        track
            .FilePath.Should()
            .Be(expected: "/encoded/Show.S01E01/subtitles/Show.S01E01.NoMercy.spa.forced.srt");
    }

    [Fact]
    public async Task Sidecar_is_written_through_its_own_storage_not_the_injected_one()
    {
        Mock<IStorage> destination = StorageMock();
        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: SuccessProcess().Object,
            modelManager: ModelManagerMock().Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine.OcrAsync(
            inputPath: InputPath,
            streamIndex: 0,
            language: "eng",
            outputFormat: SubtitleCodecType.WebVtt,
            ct: default,
            sidecar: Sidecar(storage: destination.Object, variant: "full")
        );

        destination.Verify(
            expression: s =>
                s.WriteAsync(
                    It.Is<string>(p => p.EndsWith("eng.full.vtt")),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    private static OcrSidecarTarget Sidecar(IStorage storage, string variant) =>
        new(
            Storage: storage,
            OutputDirectory: "/encoded/Show.S01E01",
            MediaTitle: "Show.S01E01.NoMercy",
            Variant: variant
        );

    // ── Failure surfacing ─────────────────────────────────────────────────────

    [Fact]
    public async Task Nonzero_exit_throws_with_real_stderr_in_the_message()
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(
                    ExitCode: 1,
                    StdOut: "",
                    StdErr: "Error opening data file /ffmpeg_build/windows/share/tessdata/eng.traineddata",
                    Duration: TimeSpan.Zero
                )
            );

        SubtitleOcrEngine engine = new(
            options: Options(),
            processRunner: processRunner.Object,
            modelManager: ModelManagerMock().Object,
            storage: StorageMock().Object,
            logger: NullLogger<SubtitleOcrEngine>.Instance
        );

        await engine
            .Invoking(action: e => e.OcrAsync(inputPath: InputPath, streamIndex: 0, language: "eng", outputFormat: SubtitleCodecType.WebVtt, ct: default))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedWildcardPattern: "*eng.traineddata*");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Filter(string[]? args)
    {
        args.Should().NotBeNull();
        int index = Array.IndexOf(array: args!, value: "-filter_complex");
        index.Should().BeGreaterThan(expected: -1);
        return args![index + 1];
    }

    private static EncoderOptions Options() => new() { FfmpegPathOverride = FfmpegPath };

    private static Mock<ITesseractModelManager> ModelManagerMock()
    {
        Mock<ITesseractModelManager> modelManager = new();
        modelManager
            .Setup(expression: m =>
                m.EnsureLanguageModelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: Path.Combine(path1: ModelDirectory, path2: "eng.traineddata"));
        return modelManager;
    }

    private static Mock<IStorage> StorageMock()
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>())).Returns<string>(valueFunction: p => new(path: p));
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        storage
            .Setup(expression: s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: Encoding.UTF8.GetBytes(s: "pts_time:0\nlavfi.ocr.text=hi\n"));
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
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        return processRunner;
    }

    private static Mock<IProcessRunner> CaptureProcess(
        Action<string[], IReadOnlyDictionary<string, string>?, string?> capture
    )
    {
        Mock<IProcessRunner> processRunner = new();
        processRunner
            .Setup(expression: p =>
                p.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                IReadOnlyDictionary<string, string>?,
                string?,
                CancellationToken
            >(action: (_, args, env, workingDirectory, _) => capture(arg1: args, arg2: env, arg3: workingDirectory))
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        return processRunner;
    }
}
