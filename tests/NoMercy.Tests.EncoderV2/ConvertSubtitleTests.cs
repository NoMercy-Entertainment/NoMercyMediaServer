using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class ConvertSubtitleTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _testVideo;
    private readonly string _fileName = "convert_sub_test";
    private readonly string _language = "eng";
    private readonly string _instanceType = "full";
    private readonly string _staticType = "sign";
    private readonly string _ext = "vtt";
    private readonly string _instanceSubtitleFile;
    private readonly string _staticSubtitleFile;
    private readonly string _subtitleFolder;

    private readonly string _assetSup;

    public ConvertSubtitleTests()
    {
        AppFiles.CreateAppFolders().Wait();
        
        _assetSup = Path.Combine(_cwd, "Assets", "test.sup");
        _testVideo = Path.Combine(_cwd, "convertSubtitle_input.mkv");
        _instanceSubtitleFile = Path.Combine(_cwd, $"{_fileName}.{_language}.{_instanceType}.{_ext}");
        _staticSubtitleFile = Path.Combine(_cwd, $"{_fileName}.{_language}.{_staticType}.{_ext}");
        _subtitleFolder = Path.Combine(_cwd, "subtitles");
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(_assetSup))
            throw new FileNotFoundException("Missing SUP asset file", _assetSup);

        // Create a simple test video and mux the SUP into it
        string cmd =
            "-y -f lavfi -i testsrc=size=640x360:rate=25:duration=6 " +
            $"-i \"{_assetSup}\" " +
            "-map 0:v -c:v libx264 -preset fast " +
            "-map 1:s:0 -c:s copy " +
            $"-metadata:s:s:0 language={_language} -disposition:s:0 default " +
            $"\"{_testVideo}\"";

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        ExecResult r = await Shell.ExecAsync(AppFiles.FfmpegPath, cmd, cts: cts);

        if (r.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create MKV with SUP subtitle: {r.StandardError}");
    }

    [Fact]
    public async Task InstanceMethod_ConvertsImageSubtitleToVtt()
    {
        ConvertSubtitle conv = new(_testVideo, _cwd, _fileName, _language, _instanceType);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        await conv.Run(cts);

        Assert.True(File.Exists(_instanceSubtitleFile), "VTT output file was not created");

        string content = await File.ReadAllTextAsync(_instanceSubtitleFile, cts.Token);
        Assert.Contains("WEBVTT", content);
    }

    [Fact]
    public async Task StaticMethod_ConvertsImageSubtitleToVtt()
    {        
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        await ConvertSubtitle.RunStatic(_testVideo, _cwd, _fileName, _language, _staticType, cts);

        Assert.True(File.Exists(_staticSubtitleFile), "VTT output file was not created by static method");

        string content = await File.ReadAllTextAsync(_staticSubtitleFile, cts.Token);
        Assert.Contains("WEBVTT", content);
    }
    
    public async Task DisposeAsync()
    {
        if (File.Exists(_testVideo))
            File.Delete(_testVideo);
        
        if (File.Exists(_instanceSubtitleFile))
            File.Delete(_instanceSubtitleFile);
        
        if (File.Exists(_staticSubtitleFile))
            File.Delete(_staticSubtitleFile);
        
        if (Directory.Exists(_subtitleFolder))
            Directory.Delete(_subtitleFolder, true);
        
        await Task.CompletedTask;
    }
}
