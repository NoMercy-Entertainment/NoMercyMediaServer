namespace NoMercy.Tests.Encoder.Integration;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Startup;

[Trait("Category", "Integration")]
[Collection("RealEncode")]
public class RealEncodeTests : IAsyncLifetime
{
    private string _testDir = string.Empty;
    private string _inputFile = string.Empty;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-encode-test-" + Guid.NewGuid().ToString("N")[..8]
        );
        Directory.CreateDirectory(_testDir);

        _inputFile = Path.Combine(_testDir, "test-input.mp4");

        // Generate a 3-second test clip — short to minimize encode time
        ProcessStartInfo psi = new()
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("testsrc2=size=320x180:rate=25:duration=3");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("sine=frequency=440:duration=3:sample_rate=44100");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("ultrafast");
        psi.ArgumentList.Add("-crf");
        psi.ArgumentList.Add("51");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("64k");
        psi.ArgumentList.Add(_inputFile);

        using Process process = Process.Start(psi)!;
        // Read both streams to prevent buffer deadlock
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg test clip generation failed: {stderr}");
        }

        // Build DI — full encoder pipeline
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = "ffmpeg";
            opts.FfprobePathOverride = "ffprobe";
        });

        _serviceProvider = services.BuildServiceProvider();

        // Probe FFmpeg capabilities
        HardwareInitializationService hwInit =
            _serviceProvider.GetRequiredService<HardwareInitializationService>();
        await hwInit.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _serviceProvider.Dispose();

        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task EncodeAsync_HlsProfile_ProducesPlaylistAndSegments()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        string outputDir = Path.Combine(_testDir, "output");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-180p",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new VideoOutput(
                    Codec: VideoCodecType.H264,
                    Width: 320,
                    Height: 180,
                    BitrateKbps: 200,
                    Crf: 40,
                    Preset: "ultrafast",
                    Profile: "baseline",
                    Level: "3.0",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new AudioOutput(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 44100,
                    AllowedLanguages: ["und"],
                    Loudness: LoudnessMode.None
                ),
            ],
            SubtitleOutputs: []
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
        result.OutputPath.Should().NotBeNullOrWhiteSpace();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Metrics.EncoderUsed.Should().NotBeNullOrWhiteSpace();

        // Verify HLS: at least one playlist and at least one segment
        string[] playlists = Directory.GetFiles(outputDir, "*.m3u8", SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(outputDir, "*.m4s", SearchOption.AllDirectories);

        playlists.Should().NotBeEmpty("HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty("HLS output should contain at least one .m4s segment");

        // Verify progress observer received at least one callback (stage-completed at end)
        (observer.StagesStarted.Count + observer.ProgressCallCount)
            .Should()
            .BeGreaterThan(0, "should receive at least one progress callback");
    }

    private class TestProgressObserver : IProgressObserver
    {
        public List<string> StagesStarted { get; } = [];
        public int ProgressCallCount { get; private set; }

        public void OnStageStarted(string stageName) => StagesStarted.Add(stageName);

        public void OnProgress(EncodingProgress progress) => ProgressCallCount++;

        public void OnStageCompleted(string stageName, TimeSpan duration) { }

        public void OnError(NoMercy.Encoder.Errors.EncodingError error) { }
    }
}
