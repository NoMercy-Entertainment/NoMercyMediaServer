using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class DetectFrameCropTests : IAsyncLifetime
{
    private readonly string _testVideo;
    private readonly string _cwd = Directory.GetCurrentDirectory();

    public DetectFrameCropTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _testVideo = Path.Combine(_cwd, "cropdetect_test.mp4");
    }

    public async Task InitializeAsync()
    {
        // Generate test video if it doesn't exist
        if (!File.Exists(_testVideo))
        {
            string ffmpegCmd = "-y -f lavfi -i testsrc=size=1000x600:rate=24 -f lavfi -i sine=frequency=1000:duration=10 -filter_complex \"[0:v]pad=1280:720:100:60:color=black[video];[1:a]volume=3[audio]\" -map \"[video]\" -map \"[audio]\" -c:v libx264 -preset ultrafast -crf 30 -c:a aac -b:a 32k -t 10 \"" + _testVideo + "\"";

            CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));
            ExecResult result = await Shell.ExecAsync(AppFiles.FfmpegPath, ffmpegCmd, cts: cts);

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to generate test video: {result.StandardError}");
        }
    }

    [Fact]
    public async Task StaticMethod_ReturnsExpectedCrop()
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

        string crop = await DetectFrameCrop.GetStatic(_testVideo, cts);

        // Expected crop based on how the test video was created (pad to 992,592 with offsets 104,64)
        string expectedCrop = "992:592:104:64";
        Assert.Equal(expectedCrop, crop);
    }

    [Fact]
    public async Task InstanceMethod_ReturnsExpectedCrop()
    {
        DetectFrameCrop detector = new(_testVideo);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await detector.Run(cts);
        string result = detector.Get();

        // Expected crop based on how the test video was created (pad to 992,592 with offsets 104,64)
        string expectedCrop = "992:592:104:64";
        Assert.Equal(expectedCrop, result);
    }
    
    public Task DisposeAsync()
    {
        if (File.Exists(_testVideo))
            File.Delete(_testVideo);
        
        return Task.CompletedTask;
    }
}
