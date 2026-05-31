using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class ThumbnailGeneratorTests : IDisposable
{
    private readonly ThumbnailGenerator _gen = new(TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public ThumbnailGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailGeneratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand has correct fps filter
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_HasCorrectFpsFilter()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            plan,
            TimeSpan.FromMinutes(90)
        );

        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("fps=1/10");
        args.Should().Contain("scale=320:-2");
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand maps first video stream
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_MapsFirstVideoStream()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            plan,
            TimeSpan.FromMinutes(90)
        );

        cmd.Arguments.Should().Contain("0:v:0");
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand output path includes thumbs directory and pattern
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_OutputPathIsInsideThumbsDir()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            plan,
            TimeSpan.FromMinutes(90)
        );

        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("thumbs_320");
        args.Should().Contain("thumb_%04d.jpg");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand has correct tile dimensions for 9 images
    // 9 images → 3x3 grid
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_NineImages_ThreeByThreeGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand("ffmpeg", _tempDir, plan, imageCount: 9);

        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("tile=3x3");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand 4 images → 2x2 grid
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_FourImages_TwoByTwoGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand("ffmpeg", _tempDir, plan, imageCount: 4);

        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("tile=2x2");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand 10 images → 4x3 grid (ceil(sqrt(10))=4, ceil(10/4)=3)
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_TenImages_FourByThreeGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand("ffmpeg", _tempDir, plan, imageCount: 10);

        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("tile=4x3");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand output is a webp file
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_OutputIsWebp()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand("ffmpeg", _tempDir, plan, imageCount: 9);

        cmd.Arguments.Last().Should().EndWith(".webp");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync produces valid WEBVTT header
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_WritesWebvttHeader()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        await _gen.WriteVttCueFileAsync(
            _tempDir,
            plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(30),
            ct: default
        );

        string vttFile = Path.Combine(_tempDir, "thumbs_320x218.vtt");
        File.Exists(vttFile).Should().BeTrue();

        string content = await File.ReadAllTextAsync(vttFile);
        content.Should().StartWith("WEBVTT");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync correct timestamps
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_CorrectTimestamps()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        await _gen.WriteVttCueFileAsync(
            _tempDir,
            plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(30),
            ct: default
        );

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "thumbs_320x218.vtt"));

        // First cue: 00:00:00.000 --> 00:00:10.000
        content.Should().Contain("00:00:00.000 --> 00:00:10.000");
        // Second cue: 00:00:10.000 --> 00:00:20.000
        content.Should().Contain("00:00:10.000 --> 00:00:20.000");
        // Third cue: 00:00:20.000 --> 00:00:30.000
        content.Should().Contain("00:00:20.000 --> 00:00:30.000");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync correct xywh coordinates
    // First image is at col=0, row=0 → x=0, y=0
    // Second image (3-col grid) is at col=1, row=0 → x=320, y=0
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_CorrectXywhCoordinates()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        // 9 images → 3x3 grid, thumbHeight = 320*9/16 = 180
        await _gen.WriteVttCueFileAsync(
            _tempDir,
            plan,
            imageCount: 9,
            duration: TimeSpan.FromSeconds(90),
            ct: default
        );

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "thumbs_320x218.vtt"));

        // First frame: col=0, row=0 → xywh=0,0,320,218
        content.Should().Contain("thumbs_320x218.webp#xywh=0,0,320,218");
        // Second frame: col=1, row=0 → xywh=320,0,320,218
        content.Should().Contain("thumbs_320x218.webp#xywh=320,0,320,218");
        // Fourth frame: col=0, row=1 → xywh=0,218,320,218
        content.Should().Contain("thumbs_320x218.webp#xywh=0,218,320,218");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync last cue is clamped to duration
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_LastCueClamped()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        // imageCount=3, duration=25s → last cue end = min(30, 25) = 25s
        await _gen.WriteVttCueFileAsync(
            _tempDir,
            plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(25),
            ct: default
        );

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "thumbs_320x218.vtt"));
        content.Should().Contain("00:00:20.000 --> 00:00:25.000");
    }

    // ------------------------------------------------------------------
    // CleanupIndividualThumbnails removes the thumbs directory
    // ------------------------------------------------------------------

    [Fact]
    public void CleanupIndividualThumbnails_RemovesThumbsDirectory()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        string thumbDir = Path.Combine(_tempDir, "thumbs_320");
        Directory.CreateDirectory(thumbDir);
        File.WriteAllText(Path.Combine(thumbDir, "thumb_0001.jpg"), "dummy");

        _gen.CleanupIndividualThumbnails(_tempDir, plan);

        Directory.Exists(thumbDir).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // CleanupIndividualThumbnails is safe when directory does not exist
    // ------------------------------------------------------------------

    [Fact]
    public void CleanupIndividualThumbnails_MissingDir_DoesNotThrow()
    {
        ThumbnailOutputPlan plan = new(Width: 999, Height: 562, IntervalSeconds: 10);
        Action act = () => _gen.CleanupIndividualThumbnails(_tempDir, plan);
        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // ComputeGrid: various image counts
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 4, 3)]
    [InlineData(16, 4, 4)]
    [InlineData(17, 5, 4)]
    public void ComputeGrid_CorrectDimensions(int imageCount, int expectedW, int expectedH)
    {
        (int w, int h) = ThumbnailGenerator.ComputeGrid(imageCount);

        w.Should().Be(expectedW);
        h.Should().Be(expectedH);
    }
}
