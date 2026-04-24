namespace NoMercy.Tests.Encoder.ContentAnalysis;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Tests.Encoder.Storage;

public class CropDetectorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly EncoderOptions _options = new() { FfmpegPathOverride = "ffmpeg" };

    [Fact]
    public async Task Detect_StableCrop_ReturnsWithShouldCropTrue()
    {
        string[] stderrLines =
        [
            "frame=1 fps=0 q=-0 size=N/A time=00:00:00.00 bitrate=N/A",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
        ];

        SetupStderr(stderrLines, exitCode: 0);
        CropDetector detector = new(
            _options,
            _processRunner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync("/tmp/in.mkv", CancellationToken.None);

        Assert.Equal(1920, result.Width);
        Assert.Equal(1040, result.Height);
        Assert.Equal(0, result.X);
        Assert.Equal(20, result.Y);
        Assert.True(result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FullFrameCrop_ShouldCropFalse()
    {
        string[] stderrLines = Enumerable.Repeat("[cropdetect] crop=1920:1080:0:0", 10).ToArray();

        SetupStderr(stderrLines, exitCode: 0);
        CropDetector detector = new(
            _options,
            _processRunner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync("/tmp/in.mkv", CancellationToken.None);

        Assert.False(result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FewerThanMinObservations_ShouldCropFalse()
    {
        string[] stderrLines = Enumerable
            .Repeat("crop=1920:1040:0:20", 3) // below threshold
            .ToArray();

        SetupStderr(stderrLines, exitCode: 0);
        CropDetector detector = new(
            _options,
            _processRunner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync("/tmp/in.mkv", CancellationToken.None);

        Assert.False(result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FfmpegNonZeroExit_ReturnsEmptyResult()
    {
        SetupStderr(["crop=1920:1040:0:20"], exitCode: 1);
        CropDetector detector = new(
            _options,
            _processRunner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync("/tmp/in.mkv", CancellationToken.None);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
        Assert.False(result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_PicksMostFrequentCrop()
    {
        // 6 x wide-crop, 2 x no-crop → wide wins
        string[] stderrLines =
        [
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
        ];

        SetupStderr(stderrLines, exitCode: 0);
        CropDetector detector = new(
            _options,
            _processRunner.Object,
            TestStorageFactory.CreateLocal(),
            NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync("/tmp/in.mkv", CancellationToken.None);

        Assert.Equal(1040, result.Height);
        Assert.Equal(20, result.Y);
        Assert.True(result.ShouldCrop);
    }

    private void SetupStderr(string[] lines, int exitCode)
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    string[] _,
                    Action<string>? onStdOut,
                    Action<string>? onStdErr,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    foreach (string line in lines)
                        onStdErr?.Invoke(line);

                    return Task.FromResult(
                        new ProcessResult(
                            exitCode,
                            string.Empty,
                            string.Join('\n', lines),
                            TimeSpan.FromSeconds(1)
                        )
                    );
                }
            );
    }
}
