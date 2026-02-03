using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class GetDurationTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _testVideo;
    private const double ExpectedSeconds = 5.0;

    public GetDurationTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _testVideo = Path.Combine(_cwd, "getduration_test.wav");
    }

    public async Task InitializeAsync()
    {
        // Generate deterministic audio file with known duration
        if (!File.Exists(_testVideo))
        {
            string ffmpegPath = File.Exists(AppFiles.FfmpegPath) ? AppFiles.FfmpegPath : "ffmpeg";
            string cmd = $"-y -f lavfi -i sine=frequency=440:duration={ExpectedSeconds} -c:a pcm_s16le -ar 44100 -ac 1 \"{_testVideo}\"";
            CancellationTokenSource ctsGen = new(TimeSpan.FromMinutes(1));
            ExecResult genRes = await Shell.ExecAsync(ffmpegPath, cmd, cts: ctsGen);
            if (genRes.ExitCode != 0)
                throw new InvalidOperationException($"Failed to generate audio test file: {genRes.StandardError}");
        }
    }

    [Fact]
    public async Task StaticMethod_ReturnsApproxExpectedDuration()
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        double? duration = await GetDuration.GetStatic(_testVideo, cts);
        
        Assert.NotNull(duration);
        Assert.InRange(duration.Value, ExpectedSeconds - 0.2, ExpectedSeconds + 0.2);
    }

    [Fact]
    public async Task InstanceMethod_ReturnsApproxExpectedDuration()
    {
        GetDuration getter = new(_testVideo);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await getter.Run(cts);
        double? duration = getter.Get();
        
        Assert.NotNull(duration);
        Assert.InRange(duration.Value, ExpectedSeconds - 0.2, ExpectedSeconds + 0.2);
    }
    

    public Task DisposeAsync()
    {
        if (File.Exists(_testVideo))
            File.Delete(_testVideo);
        
        return Task.CompletedTask;
    }
}
