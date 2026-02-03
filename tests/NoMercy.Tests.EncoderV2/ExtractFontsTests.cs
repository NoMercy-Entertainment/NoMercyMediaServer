using Newtonsoft.Json.Linq;
using Xunit;
using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class ExtractFontsTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _testVideo;
    private readonly string _font1;
    private readonly string _font2;
    private readonly string _fontJson;

    public ExtractFontsTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _testVideo = Path.Combine(_cwd, "extractFonts_test.mkv");
        _font1 = Path.Combine(_cwd, "dummy1.ttf");
        _font2 = Path.Combine(_cwd, "dummy2.otf");
        _fontJson = Path.Combine(_cwd, "fonts.json");
    }

    public async Task InitializeAsync()
    {
        // Create two small dummy font files to attach
        await File.WriteAllTextAsync(_font1, "This is a dummy font file 1");
        await File.WriteAllTextAsync(_font2, "This is a dummy font file 2");
        string font1Name = Path.GetFileName(_font1);
        string font2Name = Path.GetFileName(_font2);

        // Build MKV with attachments in a single ffmpeg command
        string ffmpegCmd = $"-y -f lavfi -i testsrc=size=320x180:rate=24 -f lavfi -i sine=frequency=440:duration=4 -attach \"{_font1}\" -metadata:s:t:0 filename=\"{font1Name}\" -metadata:s:t:0 mimetype=\"font/ttf\" -attach \"{_font2}\" -metadata:s:t:1 filename=\"{font2Name}\" -metadata:s:t:1 mimetype=\"font/otf\" -map 0 -map 1 -c:v libx264 -preset ultrafast -crf 30 -c:a aac -b:a 64k -t 4 \"{_testVideo}\"";

        CancellationTokenSource cts = new(TimeSpan.FromMinutes(1));
        ExecResult res = await Shell.ExecAsync(AppFiles.FfmpegPath, ffmpegCmd, cts: cts);
        if (res.ExitCode != 0)
            throw new InvalidOperationException($"Failed to generate MKV with attachments: {res.StandardError}");

    }

    [Fact]
    public async Task InstanceMethod_DumpsAttachedFontsToDestination()
    {
        ExtractFonts task = new(_testVideo, _cwd);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await task.Run(cts);

        Assert.True(File.Exists(_fontJson), "fonts.json was not created");

        // Validate JSON contents include our two attachments with correct filenames and mime types
        string json = await File.ReadAllTextAsync(_fontJson, cts.Token);
        JArray arr = JArray.Parse(json);
        string?[] filenames = arr.Select(x => x["file"]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string?[] mimes = arr.Select(x => x["mimeType"]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

        Assert.Contains(filenames, f => f.EndsWith("dummy1.ttf"));
        Assert.Contains(filenames, f => f.EndsWith("dummy2.otf"));
        Assert.Contains(mimes, m => m == "application/x-font-truetype");
        Assert.Contains(mimes, m => m == "application/x-font-opentype");
        
        if (File.Exists(_fontJson))
            File.Delete(_fontJson);
    }

    [Fact]
    public async Task StaticMethod_DumpsAttachedFontsToDestination()
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await ExtractFonts.RunStatic(_testVideo, _cwd, cts);

        Assert.True(File.Exists(_fontJson) || File.Exists(Path.Combine(_cwd, "fonts.json")), "fonts.json was not created by static method");

        string json = await File.ReadAllTextAsync(_fontJson, cts.Token);
        JArray arr = JArray.Parse(json);
        string?[] filenames = arr.Select(x => x["file"]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        string?[] mimes = arr.Select(x => x["mimeType"]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

        Assert.Contains(filenames, f => f.EndsWith("dummy1.ttf"));
        Assert.Contains(filenames, f => f.EndsWith("dummy2.otf"));
        Assert.Contains(mimes, m => m == "application/x-font-truetype");
        Assert.Contains(mimes, m => m == "application/x-font-opentype");
        
        if (File.Exists(_fontJson))
            File.Delete(_fontJson);
    }
    
    public Task DisposeAsync()
    {
        if (File.Exists(_testVideo))
            File.Delete(_testVideo);
        
        if (File.Exists(_font1))
            File.Delete(_font1);
        
        if (File.Exists(_font2))
            File.Delete(_font2);
        
        if (File.Exists(_fontJson))
            File.Delete(_fontJson);
        
        return Task.CompletedTask;
    }
}
