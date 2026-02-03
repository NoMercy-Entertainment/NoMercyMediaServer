using System.Text;
using Xunit;
using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class ExtractChaptersTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _testVideo;
    private readonly string _tmpMeta;
    private readonly string _instanceVttOutput;
    private readonly string _staticVttOutput;

    public ExtractChaptersTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _testVideo = Path.Combine(_cwd, "extractChapters_test.mkv");
        _tmpMeta = Path.Combine(_cwd, "chapters.ffmeta");
        _instanceVttOutput = Path.Combine(_cwd, "instance_chapters.vtt");
        _staticVttOutput = Path.Combine(_cwd, "static_chapters.vtt");

    }

    public async Task InitializeAsync()
    {
        // Create ffmetadata file with two chapters
        StringBuilder sb = new();
        sb.AppendLine(";FFMETADATA1");
        sb.AppendLine("[CHAPTER]");
        sb.AppendLine("TIMEBASE=1/1000");
        sb.AppendLine("START=0");
        sb.AppendLine("END=5000");
        sb.AppendLine("title=Intro");
        sb.AppendLine();
        sb.AppendLine("[CHAPTER]");
        sb.AppendLine("TIMEBASE=1/1000");
        sb.AppendLine("START=5000");
        sb.AppendLine("END=10000");
        sb.AppendLine("title=Main");

        await File.WriteAllTextAsync(_tmpMeta, sb.ToString());

        // Create the MKV directly: two lavfi inputs (video+audio) and the ffmetadata input
        string ffmpegCmd = $"-y -f lavfi -i testsrc=size=640x360:rate=24 -f lavfi -i sine=frequency=440:duration=10 -f ffmetadata -i \"{_tmpMeta}\" -map 0 -map 1 -map_metadata 2 -c:v libx264 -preset ultrafast -crf 30 -c:a aac -b:a 64k -t 10 \"{_testVideo}\"";
        
        CancellationTokenSource cts = new(TimeSpan.FromMinutes(1));
        ExecResult mergeResult = await Shell.ExecAsync(AppFiles.FfmpegPath, ffmpegCmd, cts: cts);
        if (mergeResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to generate test MKV with chapters: {mergeResult.StandardError}");
    }

    [Fact]
    public async Task InstanceMethod_GeneratesVttFromEmbeddedChapters()
    {
        ExtractChapters task = new(_testVideo,  _cwd,"instance_chapters");
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await task.Run(cts);

        Assert.True(File.Exists(_instanceVttOutput), "VTT file was not created");

        string[] lines = await File.ReadAllLinesAsync(_instanceVttOutput, cts.Token);
        string content = string.Join('\n', lines);

        // Basic checks for expected chapter titles and WEBVTT header
        Assert.Contains("WEBVTT", content);
        Assert.Contains("Intro", content);
        Assert.Contains("Main", content);
        // check for time format (HH:MM:SS)
        Assert.Matches(@"\d{2}:\d{2}:\d{2}", content);
    }

    [Fact]
    public async Task StaticMethod_GeneratesVttFromEmbeddedChapters()
    {
        Directory.CreateDirectory(_cwd);
        
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await ExtractChapters.RunStatic(_testVideo, _cwd, "static_chapters", cts);

        Assert.True(File.Exists(_staticVttOutput), "VTT file was not created by static method");

        // Verify contents to ensure extraction succeeded, not just file creation
        string[] lines = await File.ReadAllLinesAsync(_staticVttOutput, cts.Token);
        string content = string.Join('\n', lines);
        Assert.Contains("WEBVTT", content);
        Assert.Contains("Intro", content);
        Assert.Contains("Main", content);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_testVideo))
            File.Delete(_testVideo);
        
        if (File.Exists(_tmpMeta))
            File.Delete(_tmpMeta);
        
        if (File.Exists(_instanceVttOutput))
            File.Delete(_instanceVttOutput);
        
        if (File.Exists(_staticVttOutput))
            File.Delete(_staticVttOutput);
        
        return Task.CompletedTask;
    }
}
