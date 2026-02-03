using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Tests.EncoderV2;

public class GenerateSpriteTests : IAsyncLifetime
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly string _thumbFolder;
    private readonly string _spriteFile;
    private readonly string _vttFile;
    private const int Width = 320;
    private const int Height = 180;

    public GenerateSpriteTests()
    {
        AppFiles.CreateAppFolders().Wait();
        _thumbFolder = Path.Combine(_cwd, $"thumbs_{Width}x{Height}");
        _spriteFile = Path.Combine(_cwd, $"thumbs_{Width}x{Height}.webp");
        _vttFile = Path.Combine(_cwd, $"thumbs_{Width}x{Height}.vtt");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_thumbFolder);

        // filter: [v:0]crop=1280:720:0:0,scale=320:-2,fps=1/10[i0_hls_0]
        string filter = $"[0:v]crop=1280:720:0:0,scale={Width}:-2,fps=1/10[i0_hls_0]";

        string imagePattern = Path.Combine(_thumbFolder, $"thumbs_{Width}x{Height}-%04d.jpg");

        string ffmpegCmd = $"-y -f lavfi -i testsrc=size=1280x720:rate=24 -filter_complex \"{filter}\" -map \"[i0_hls_0]\" -t 60 \"{imagePattern}\"";

        CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));
        ExecResult res = await Shell.ExecAsync(AppFiles.FfmpegPath, ffmpegCmd, cts: cts);
        if (res.ExitCode != 0)
            throw new InvalidOperationException($"Failed to generate thumbnails: {res.StandardError}");
    }

    [Fact]
    public async Task InstanceMethod_CreatesVttAndDeletesThumbnails()
    {
        GenerateSprite task = new(_cwd, Width, Height, 10);
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        await task.Run(cts);

        Assert.True(File.Exists(_vttFile), "VTT file was not created");
        Assert.True(File.Exists(_spriteFile), "Sprite image was not created");
        Assert.False(Directory.Exists(_thumbFolder), "Thumbnail folder should be deleted after sprite generation");

        string vtt = await File.ReadAllTextAsync(_vttFile);
        Assert.Contains("WEBVTT", vtt);
        Assert.Contains("#xywh=", vtt);
    }

    [Fact]
    public async Task StaticMethod_CreatesVttAndDeletesThumbnails()
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        await GenerateSprite.RunStatic(_cwd, Width, Height, 10, cts);

        Assert.True(File.Exists(_vttFile), "VTT file was not created");
        Assert.True(File.Exists(_spriteFile), "Sprite image was not created");
        Assert.False(Directory.Exists(_thumbFolder), "Thumbnail folder should be deleted after sprite generation");

        string vtt = await File.ReadAllTextAsync(_vttFile);
        Assert.Contains("WEBVTT", vtt);
        Assert.Contains("#xywh=", vtt);
    }
    
    public Task DisposeAsync()
    {
        if (File.Exists(_spriteFile))
            File.Delete(_spriteFile);
        
        if (File.Exists(_vttFile))
            File.Delete(_vttFile);
        
        if (Directory.Exists(_thumbFolder))
            Directory.Delete(_thumbFolder, true); 
        
        return Task.CompletedTask;
    }
}
