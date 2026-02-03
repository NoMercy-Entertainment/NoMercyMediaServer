using Xunit;
using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class GenerateFingerprintTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _audioFile;
    private const string ExpectedFingerprint = "AQAAE0mUaEkSZSoAAAAAAAAA";

    public GenerateFingerprintTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _audioFile = Path.Combine(_cwd, "fingerprint_test.wav");
    }

    public async Task InitializeAsync()
    {
        // Create deterministic audio sample once per class
        if (!File.Exists(_audioFile))
        {
            string ffmpegPath = File.Exists(AppFiles.FfmpegPath) ? AppFiles.FfmpegPath : "ffmpeg";
            string cmd = $"-y -f lavfi -i sine=frequency=440:duration=5 -c:a pcm_s16le -ar 44100 -ac 1 \"{_audioFile}\"";
            CancellationTokenSource ctsGen = new(TimeSpan.FromMinutes(1));
            ExecResult genRes = await Shell.ExecAsync(ffmpegPath, cmd, cts: ctsGen);
            if (genRes.ExitCode != 0)
                throw new InvalidOperationException($"Failed to generate audio test file: {genRes.StandardError}");
        }
    }

    [Fact]
    public async Task StaticMethod_ReturnsExpectedFingerprint()
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        string? fingerprint = await GenerateFingerprint.GetStatic(_audioFile, cts);
        Assert.Equal(ExpectedFingerprint, fingerprint);
    }

    [Fact]
    public async Task InstanceMethod_ReturnsExpectedFingerprint()
    {
        GenerateFingerprint generator = new(_audioFile);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await generator.Run(cts);
        string? fingerprint = generator.Get();
        Assert.Equal(ExpectedFingerprint, fingerprint);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_audioFile))
            File.Delete(_audioFile);
        
        return Task.CompletedTask;
    }
}
