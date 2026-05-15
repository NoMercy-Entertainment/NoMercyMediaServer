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
using NoMercy.Resources;
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
        _tempDir = Path.Combine(Path.GetTempPath(), $"ChapterThumbsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };
        _buildStage = new(
            options,
            new FontExtractor(TestStorageFactory.CreateLocal()),
            new SubtitleExtractor(),
            OutputStrategyFactoryTestHelper.Create(),
            [],
            NullLogger<BuildStage>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IEncoder MockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );
        return mock.Object;
    }

    private static IReadOnlyList<ChapterInfo> MakeChapters(int count)
    {
        List<ChapterInfo> chapters = [];
        for (int i = 0; i < count; i++)
        {
            TimeSpan start = TimeSpan.FromMinutes(i * 10);
            TimeSpan end = TimeSpan.FromMinutes((i + 1) * 10);
            chapters.Add(new ChapterInfo(start, end, $"Chapter {i + 1}"));
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
            .Range(0, videoCount)
            .Select(i => new VideoOutputPlan(
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

        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: videos,
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: MakeChapters(chapterCount),
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
                        new("decode_0", OperationType.Decode, [], new()),
                        new("encode_0", OperationType.Encode, ["decode_0"], new()),
                    ],
                    DeviceId: null,
                    GpuSlotsRequired: 0,
                    CpuThreadsRequired: 4,
                    RequiresGpu: false,
                    Priority: 1
                ),
            ],
            EstimatedTotalDuration: TimeSpan.FromMinutes(90),
            OutputPlan: outputPlan
        );

    // ── Test 1: Decompose emits chapter tasks when flag is set ─────────────────

    [Fact]
    public void DecomposeAddsChapterTasks_WhenFlagSet()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlanWithChapters(
            chapterCount: 5,
            generateChapterThumbs: true,
            videoCount: 1
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        DecomposedTask[] chapterTasks = tasks
            .Where(task => task.Kind == EncodeTaskKind.Chapters)
            .ToArray();

        chapterTasks.Should().HaveCount(5);
        chapterTasks.Should().AllSatisfy(task => task.GroupTag.Should().Be(GroupTag));
        chapterTasks.Should().AllSatisfy(task => task.EstimatedCostUnits.Should().Be(1));
        chapterTasks
            .Should()
            .AllSatisfy(task =>
                task.Resources.Should().BeEquivalentTo(new ResourceRequirement(null, 0, 1))
            );

        for (int i = 0; i < 5; i++)
        {
            chapterTasks[i].OutputIndex.Should().Be(i);
            chapterTasks[i].TaskId.Should().Be($"{GroupTag}-chapter-{i}");
        }
    }

    // ── Test 2: Decompose skips chapter tasks when flag is false ───────────────

    [Fact]
    public void DecomposeSkipsChapterTasks_WhenFlagFalse()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlanWithChapters(
            chapterCount: 5,
            generateChapterThumbs: false,
            videoCount: 1
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Where(task => task.Kind == EncodeTaskKind.Chapters).Should().BeEmpty();
    }

    // ── Test 3: BuildStage emits single-frame extract command ─────────────────

    [Fact]
    public async Task BuildStage_ChapterTaskEmitsSingleFrameExtract()
    {
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(3);
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
            Resources: new ResourceRequirement(null, 0, 1),
            EstimatedCostUnits: 1,
            Label: $"chapter still {targetChapter + 1}/3 @ {chapters[targetChapter].Start.TotalSeconds:F0}s"
        );

        BuildInput input = new(
            WrapInExecutionPlan(outputPlan),
            InputPath: "/movies/test.mkv",
            OutputDirectory: _tempDir,
            MediaTitle: "Test",
            TaskFilter: chapterTask
        );

        StageResult result = await _buildStage.ExecuteAsync(
            input,
            _context,
            CancellationToken.None
        );

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();
        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)result).Value;
        commands.Should().HaveCount(1);

        FfmpegCommand cmd = commands[0];
        string joined = string.Join(" ", cmd.Arguments);

        // Seek before input for accuracy
        joined.Should().Contain("-ss");
        joined
            .Should()
            .Contain(
                chapters[targetChapter]
                    .Start.TotalSeconds.ToString(
                        "F3",
                        System.Globalization.CultureInfo.InvariantCulture
                    )
            );

        // Single frame extraction
        joined.Should().Contain("-frames:v");
        joined.Should().Contain("1");

        // Scale filter
        joined.Should().Contain("-vf");
        joined.Should().Contain("scale=240:-2");

        // Output filename
        string expectedOutput = Path.Combine("chapters", $"{targetChapter:D2}.webp");
        joined.Should().Contain(expectedOutput.Replace(Path.DirectorySeparatorChar, '/'));
    }

    // ── Test 4: ChapterWriter emits thumbnail URIs when flag is set ───────────

    [Fact]
    public async Task ChapterWriter_EmitsThumbReference_WhenFlagSet()
    {
        ChapterWriter writer = new(TestStorageFactory.CreateLocal());
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(3);

        await writer.WriteChaptersAsync(
            _tempDir,
            chapters,
            CancellationToken.None,
            includeThumbUris: true
        );

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));

        content.Should().Contain("chapters/00.webp");
        content.Should().Contain("chapters/01.webp");
        content.Should().Contain("chapters/02.webp");
    }

    [Fact]
    public async Task ChapterWriter_NoThumbReference_WhenFlagFalse()
    {
        ChapterWriter writer = new(TestStorageFactory.CreateLocal());
        IReadOnlyList<ChapterInfo> chapters = MakeChapters(2);

        await writer.WriteChaptersAsync(
            _tempDir,
            chapters,
            CancellationToken.None,
            includeThumbUris: false
        );

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "chapters.vtt"));

        content.Should().NotContain("chapters/");
    }
}
