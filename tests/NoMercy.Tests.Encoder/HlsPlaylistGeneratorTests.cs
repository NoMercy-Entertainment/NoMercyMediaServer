using NoMercy.Encoder.Core;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class HlsPlaylistGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public HlsPlaylistGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NoMercy_HlsTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void CreateVideoFolder(string folderName, string playlistContent = "#EXTM3U\n#EXT-X-VERSION:6\n")
    {
        string folderPath = Path.Combine(_tempDir, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "playlist.m3u8"), playlistContent);
    }

    private void CreateAudioFolder(string folderName, string playlistContent = "#EXTM3U\n#EXT-X-VERSION:6\n")
    {
        string folderPath = Path.Combine(_tempDir, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "playlist.m3u8"), playlistContent);
    }

    [Fact]
    public async Task Build_EmptyDirectory_CreatesPlaylistWithHeadersOnly()
    {
        string emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        await HlsPlaylistGenerator.Build(emptyDir, "master");

        string playlistPath = Path.Combine(emptyDir, "master.m3u8");
        Assert.True(File.Exists(playlistPath));

        string content = await File.ReadAllTextAsync(playlistPath);
        Assert.StartsWith("#EXTM3U", content);
        Assert.DoesNotContain("#EXT-X-STREAM-INF", content);
    }

    [Fact]
    public async Task Build_NonExistentDirectory_DoesNotThrow()
    {
        string noDir = Path.Combine(_tempDir, "nonexistent");

        await HlsPlaylistGenerator.Build(noDir, "master");

        Assert.False(Directory.Exists(noDir));
    }

    [Fact]
    public async Task Build_WithAudioAndVideo_CreatesPlaylistWithHeaders()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string playlistPath = Path.Combine(_tempDir, "master.m3u8");
        Assert.True(File.Exists(playlistPath));

        string content = await File.ReadAllTextAsync(playlistPath);
        Assert.StartsWith("#EXTM3U", content);
        Assert.Contains("#EXT-X-VERSION:6", content);
    }

    [Fact]
    public async Task Build_AudioGroups_ContainCorrectAttributes()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("TYPE=AUDIO", content);
        Assert.Contains("GROUP-ID=\"audio_aac\"", content);
        Assert.Contains("LANGUAGE=\"eng\"", content);
        Assert.Contains("DEFAULT=YES", content);
    }

    [Fact]
    public async Task Build_MultipleAudioLanguages_FirstIsDefault()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");
        CreateAudioFolder("audio_jpn_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        // eng should be default (first in priority list)
        string[] lines = content.Split('\n');
        string? engLine = lines.FirstOrDefault(l => l.Contains("LANGUAGE=\"eng\""));
        string? jpnLine = lines.FirstOrDefault(l => l.Contains("LANGUAGE=\"jpn\""));

        Assert.NotNull(engLine);
        Assert.NotNull(jpnLine);
        Assert.Contains("DEFAULT=YES", engLine);
        Assert.Contains("DEFAULT=NO", jpnLine);
    }

    [Fact]
    public async Task Build_VideoVariant_ContainsCodecsAttribute()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        // Should have CODECS attribute (defaults when ffprobe fails)
        Assert.Contains("CODECS=", content);
        Assert.Contains("RESOLUTION=1920x1080", content);
    }

    [Fact]
    public async Task Build_VideoVariant_ContainsDefaultCodecWhenProbeFails()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        // When ffprobe fails, defaults to Main profile (4D), no constraints (00), level 40 (28 hex)
        Assert.Contains("avc1.4D0028", content);
        Assert.Contains("mp4a.40.2", content);
    }

    [Fact]
    public async Task Build_SdrVideo_ContainsSdrAttributes()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("VIDEO-RANGE=SDR", content);
        Assert.Contains("COLOUR-SPACE=BT.709", content);
    }

    [Fact]
    public async Task Build_HdrVideo_ContainsHdrAttributes()
    {
        CreateVideoFolder("video_1920x1080_SDR");
        CreateVideoFolder("video_3840x2160_HDR");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("VIDEO-RANGE=PQ", content);
        Assert.Contains("COLOUR-SPACE=BT.2020", content);
        Assert.Contains("VIDEO-RANGE=SDR", content);
    }

    [Fact]
    public async Task Build_MultipleResolutions_AllPresentInPlaylist()
    {
        CreateVideoFolder("video_1920x1080");
        CreateVideoFolder("video_1280x720");
        CreateVideoFolder("video_854x480");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("RESOLUTION=1920x1080", content);
        Assert.Contains("RESOLUTION=1280x720", content);
        Assert.Contains("RESOLUTION=854x480", content);
    }

    [Fact]
    public async Task Build_MultipleAudioCodecs_CreatesSeparateGroups()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");
        CreateAudioFolder("audio_eng_eac3");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("GROUP-ID=\"audio_aac\"", content);
        Assert.Contains("GROUP-ID=\"audio_eac3\"", content);
    }

    [Fact]
    public async Task Build_EAC3Audio_MapsToCorrectCodecString()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_eac3");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("ec-3", content);
    }

    [Fact]
    public async Task Build_NoExplicitSdrHdr_DefaultsToSdr()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("VIDEO-RANGE=SDR", content);
        Assert.DoesNotContain("VIDEO-RANGE=PQ", content);
    }

    [Fact]
    public async Task Build_StreamInfContainsBandwidth()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        Assert.Contains("#EXT-X-STREAM-INF:BANDWIDTH=", content);
        Assert.Contains("AVERAGE-BANDWIDTH=", content);
    }
}
