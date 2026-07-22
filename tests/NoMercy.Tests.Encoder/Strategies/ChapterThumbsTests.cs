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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Covers the opt-in chapter-still feature end-to-end:
/// decomposition task emission, BuildStage command shape, and ChapterWriter URI output.
/// </summary>
public class ChapterThumbsTests : IDisposable
{
    private const string GroupTag = "01HZCHAPTEST0000000000";

    private readonly string _tempDir;
    private readonly BuildStage _buildStage;
    private readonly EncodingContext _context = EncodingContext.Create();

    public ChapterThumbsTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"ChapterThumbsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);

        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };
        _buildStage = new(
            options: options,
            fontExtractor: new FontExtractor(storage: TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IEncoder MockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(expression: encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "test", GpuUsed: null)
                )
            );
        return mock.Object;
    }

    private static IReadOnlyList<ChapterInfo> MakeChapters(int count)
    {
        List<ChapterInfo> chapters = [];
        for (int i = 0; i < count; i++)
        {
            TimeSpan start = TimeSpan.FromMinutes(minutes: i * 10);
            TimeSpan end = TimeSpan.FromMinutes(minutes: (i + 1) * 10);
            chapters.Add(item: new(Start: start, End: end, Title: $"Chapter {i + 1}"));
        }

        return chapters;
    }

    private static OutputPlan MakePlanWithChapters(
        int chapterCount,
        bool generateChapterThumbs,
        int videoCount = 1
    )
    {
        VideoOutputPlan[] videos = Enumerable
            .Range(start: 0, count: videoCount)
            .Select(selector: i => new VideoOutputPlan(
                Width: 1920,
                Height: 1080,
                EncoderName: "libx264",
                Crf: 23,
                BitrateKbps: 0,
                Preset: "medium",
                Profile: "main",
                Level: "4.0",
                TenBit: false,
                PixelFormat: "yuv420p",
                MapLabel: $"[v{i}]",
                ExtraFlags: []
            ))
            .ToArray();

        return new(
            Format: OutputFormat.Hls,
            VideoOutputs: videos,
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: MakeChapters(count: chapterCount),
            GenerateChapterThumbs: generateChapterThumbs
        );
    }

    private static ExecutionPlan WrapInExecutionPlan(OutputPlan outputPlan) =>
        new(
            Groups:
            [
                new(
                    GroupId: "group_0",
                    Nodes:
                    [
                        new(Id: "decode_0", Operation: OperationType.Decode, DependsOn: [], Parameters: new()),
                        new(Id: "encode_0", Operation: OperationType.Encode, DependsOn: ["decode_0"], Parameters: new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 90),
            OutputPlan: outputPlan
        );

    // ── Test 1: Decompose emits chapter tasks when flag is set ─────────────────

    [Fact]
    public void DecomposeAddsChapterTasks_WhenFlagSet()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlanWithChapters(
            chapterCount: 5,
            generateChapterThumbs: true,
            videoCount: 1
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask[] chapterTasks = tasks
            .Where(predicate: task => task.Kind == EncodeTaskKind.Chapters)
            .ToArray();

        chapterTasks.Should().HaveCount(expected: 5);
        chapterTasks.Should().AllSatisfy(expected: task => task.GroupTag.Should().Be(expected: GroupTag));
        chapterTasks.Should().AllSatisfy(expected: task => task.EstimatedCostUnits.Should().Be(expected: 1));
        chapterTasks
            .Should()
            .AllSatisfy(expected: task =>
                task.Resources.Should().BeEquivalentTo(expectation: new ResourceRequirement(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1))
            );

        for (int i = 0; i < 5; i++)
        {
            chapterTasks[i].OutputIndex.Should().Be(expected: i);
            chapterTasks[i].TaskId.Should().Be(expected: $"{GroupTag}-chapter-{i}");
        }
    }

    // ── Test 2: Decompose skips chapter tasks when flag is false ───────────────

    [Fact]
    public void DecomposeSkipsChapterTasks_WhenFlagFalse()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlanWithChapters(
            chapterCount: 5,
            generateChapterThumbs: false,
            videoCount: 1
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        tasks.Where(predicate: task => task.Kind == EncodeTaskKind.Chapters).Should().BeEmpty();
    }

    // ── Test 3: BuildStage emits single-frame extract command ─────────────────

    [Fact]
    public async Task BuildStage_ChapterTaskEmitsSingleFrameExtract()
    {
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(count: 3);
        OutputPlan outputPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: chapters,
            GenerateChapterThumbs: true
        );

        int targetChapter = 1;
        DecomposedTask chapterTask = new(
            TaskId: $"{GroupTag}-chapter-{targetChapter}",
            ParentJobId: 0,
            GroupTag: GroupTag,
            Kind: EncodeTaskKind.Chapters,
            OutputIndex: targetChapter,
            Resources: new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 1),
            EstimatedCostUnits: 1,
            Label: $"chapter still {targetChapter + 1}/3 @ {chapters[index: targetChapter].Start.TotalSeconds:F0}s"
        );

        BuildInput input = new(
            Plan: WrapInExecutionPlan(outputPlan: outputPlan),
            InputPath: "/movies/test.mkv",
            OutputDirectory: _tempDir,
            MediaTitle: "Test",
            TaskFilter: chapterTask
        );

        StageResult result = await _buildStage.ExecuteAsync(
            input: input,
            context: _context,
            ct: CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands.Should().HaveCount(expected: 1);

        FfmpegCommand cmd = commands[0];
        string joined = string.Join(separator: " ", value: cmd.Arguments);

        // Seek before input for accuracy
        joined.Should().Contain(expected: "-ss");
        joined
            .Should()
            .Contain(
                expected: chapters[index: targetChapter]
                    .Start.TotalSeconds.ToString(
                        format: "F3",
                        provider: System.Globalization.CultureInfo.InvariantCulture
                    )
            );

        // Single frame extraction
        joined.Should().Contain(expected: "-frames:v");
        joined.Should().Contain(expected: "1");

        // Scale filter
        joined.Should().Contain(expected: "-vf");
        joined.Should().Contain(expected: "scale=240:-2");

        // Output filename
        string expectedOutput = Path.Combine(path1: "chapters", path2: $"{targetChapter:D2}.webp");
        joined.Should().Contain(expected: expectedOutput.Replace(oldChar: Path.DirectorySeparatorChar, newChar: '/'));
    }

    // ── Test 4: ChapterWriter emits thumbnail URIs when flag is set ───────────

    [Fact]
    public async Task ChapterWriter_EmitsThumbReference_WhenFlagSet()
    {
        ChapterWriter writer = new(storage: TestStorageFactory.CreateLocal());
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(count: 3);

        await writer.WriteChaptersAsync(
            outputDirectory: _tempDir,
            chapters: chapters,
            ct: CancellationToken.None,
            includeThumbUris: true
        );

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));

        content.Should().Contain(expected: "chapters/00.webp");
        content.Should().Contain(expected: "chapters/01.webp");
        content.Should().Contain(expected: "chapters/02.webp");
    }

    [Fact]
    public async Task ChapterWriter_NoThumbReference_WhenFlagFalse()
    {
        ChapterWriter writer = new(storage: TestStorageFactory.CreateLocal());
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(count: 2);

        await writer.WriteChaptersAsync(
            outputDirectory: _tempDir,
            chapters: chapters,
            ct: CancellationToken.None,
            includeThumbUris: false
        );

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));

        content.Should().NotContain(unexpected: "chapters/");
    }
}
