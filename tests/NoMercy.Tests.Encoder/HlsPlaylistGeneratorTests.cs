using NoMercy.Encoder.Core;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class HlsPlaylistGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public HlsPlaylistGeneratorTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "NoMercy_HlsTests_" + Guid.NewGuid().ToString("N")[..8]
        );
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void CreateVideoFolder(
        string folderName,
        string playlistContent = "#EXTM3U\n#EXT-X-VERSION:6\n"
    )
    {
        string folderPath = Path.Combine(_tempDir, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "playlist.m3u8"), playlistContent);
    }

    private void CreateAudioFolder(
        string folderName,
        string playlistContent = "#EXTM3U\n#EXT-X-VERSION:6\n"
    )
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

    [Fact]
    public async Task Build_HdrVariant_DoesNotEmitInvalidReqVideoLayout()
    {
        CreateVideoFolder("video_1920x1080_SDR");
        CreateVideoFolder("video_3840x2160_HDR");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));

        // REQ-VIDEO-LAYOUT takes layout strings (e.g. CH-STEREO), not "BYTE".
        // It is not an HDR signaling attribute. VIDEO-RANGE alone identifies HDR10.
        Assert.DoesNotContain("REQ-VIDEO-LAYOUT", content);
    }

    [Fact]
    public async Task Build_StreamInf_BandwidthIsAtLeastAverageBandwidth()
    {
        CreateVideoFolder("video_1920x1080");
        CreateAudioFolder("audio_eng_aac");

        await HlsPlaylistGenerator.Build(_tempDir, "master");

        string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "master.m3u8"));
        string streamInf = content
            .Split('\n')
            .First(l => l.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal));

        long peak = ParseAttribute(streamInf, "BANDWIDTH");
        long average = ParseAttribute(streamInf, "AVERAGE-BANDWIDTH");

        // HLS spec: BANDWIDTH is the peak per-segment bitrate, AVERAGE-BANDWIDTH the mean.
        // Peak must be >= average. The old code inverted this by adding audio overhead only to BANDWIDTH.
        Assert.True(
            peak >= average,
            $"BANDWIDTH ({peak}) must be >= AVERAGE-BANDWIDTH ({average})"
        );
    }

    private static long ParseAttribute(string streamInf, string name)
    {
        int start = streamInf.IndexOf(name + "=", StringComparison.Ordinal);
        if (start < 0)
            return 0;
        start += name.Length + 1;
        int end = streamInf.IndexOfAny([',', '\r', '\n'], start);
        if (end < 0)
            end = streamInf.Length;
        return long.Parse(streamInf[start..end]);
    }

    [Theory]
    [InlineData("h264", "Main", 40, "avc1.4D0028")]
    [InlineData("h264", "High", 41, "avc1.640029")]
    [InlineData("h264", "Constrained Baseline", 30, "avc1.42401E")]
    [InlineData("", "Main", 40, "avc1.4D0028")] // unknown codec falls back to H.264
    public void MapVideoCodec_H264_ReturnsAvc1FourCc(
        string codecName,
        string profile,
        int level,
        string expected
    )
    {
        Assert.Equal(expected, HlsPlaylistGenerator.MapVideoCodec(codecName, profile, level));
    }

    [Theory]
    [InlineData("hevc", "Main", 120, "hvc1.1.6.L120.B0")]
    [InlineData("hevc", "Main 10", 153, "hvc1.2.4.L153.B0")]
    [InlineData("h265", "Main", 90, "hvc1.1.6.L90.B0")]
    public void MapVideoCodec_Hevc_ReturnsHvc1FourCc(
        string codecName,
        string profile,
        int level,
        string expected
    )
    {
        Assert.Equal(expected, HlsPlaylistGenerator.MapVideoCodec(codecName, profile, level));
    }

    [Theory]
    [InlineData("av1", "Main", 4, "av01.0.04M.08")]
    [InlineData("av1", "High", 8, "av01.1.08M.08")]
    [InlineData("av1", "Main", 12, "av01.0.12M.08")]
    public void MapVideoCodec_Av1_ReturnsAv01FourCc(
        string codecName,
        string profile,
        int level,
        string expected
    )
    {
        Assert.Equal(expected, HlsPlaylistGenerator.MapVideoCodec(codecName, profile, level));
    }

    [Theory]
    [InlineData("vp9", "Profile 0", 40, "vp09.00.40.08")]
    [InlineData("vp9", "Profile 2", 51, "vp09.02.51.08")]
    public void MapVideoCodec_Vp9_ReturnsVp09FourCc(
        string codecName,
        string profile,
        int level,
        string expected
    )
    {
        Assert.Equal(expected, HlsPlaylistGenerator.MapVideoCodec(codecName, profile, level));
    }

    [Theory]
    [InlineData("aac", "mp4a.40.2")]
    [InlineData("AAC", "mp4a.40.2")]
    [InlineData("eac3", "ec-3")]
    [InlineData("ac3", "ac-3")]
    [InlineData("opus", "opus")]
    [InlineData("flac", "fLaC")]
    [InlineData("mp3", "mp4a.40.34")]
    [InlineData("", "mp4a.40.2")] // unknown falls back to AAC-LC
    public void MapAudioCodec_ReturnsRfc6381FourCc(string codecName, string expected)
    {
        Assert.Equal(expected, HlsPlaylistGenerator.MapAudioCodec(codecName));
    }

    [Theory]
    [InlineData("smpte2084", true)] // HDR10 (PQ)
    [InlineData("arib-std-b67", true)] // HLG
    [InlineData("SMPTE2084", true)] // case-insensitive
    [InlineData("bt2020-10", true)]
    [InlineData("bt2020-12", true)]
    [InlineData("bt709", false)] // standard SDR
    [InlineData("smpte170m", false)] // SD SDR
    [InlineData("gamma22", false)]
    [InlineData("", false)] // unknown → fall back to caller's heuristic
    [InlineData("unknown-transfer", false)]
    public void IsHdrTransfer_RecognisesPqAndHlg(string colorTransfer, bool expected)
    {
        Assert.Equal(expected, HlsPlaylistGenerator.IsHdrTransfer(colorTransfer));
    }

    [Theory]
    [InlineData("bt709", true)]
    [InlineData("smpte170m", true)]
    [InlineData("gamma22", true)]
    [InlineData("iec61966-2-1", true)]
    [InlineData("smpte2084", false)] // HDR
    [InlineData("arib-std-b67", false)] // HLG
    [InlineData("", false)] // unknown → fall back to caller's heuristic
    [InlineData("unknown-transfer", false)]
    public void IsSdrTransfer_RecognisesKnownSdrTransfers(string colorTransfer, bool expected)
    {
        Assert.Equal(expected, HlsPlaylistGenerator.IsSdrTransfer(colorTransfer));
    }

    [Fact]
    public void IsHdrTransfer_AndIsSdrTransfer_AreMutuallyExclusive()
    {
        string[] hdrSamples = ["smpte2084", "arib-std-b67", "bt2020-10", "bt2020-12"];
        string[] sdrSamples =
        [
            "bt709",
            "smpte170m",
            "bt470m",
            "bt470bg",
            "gamma22",
            "gamma28",
            "linear",
            "iec61966-2-1",
            "iec61966-2-4",
        ];

        foreach (string hdr in hdrSamples)
        {
            Assert.True(HlsPlaylistGenerator.IsHdrTransfer(hdr));
            Assert.False(HlsPlaylistGenerator.IsSdrTransfer(hdr));
        }

        foreach (string sdr in sdrSamples)
        {
            Assert.True(HlsPlaylistGenerator.IsSdrTransfer(sdr));
            Assert.False(HlsPlaylistGenerator.IsHdrTransfer(sdr));
        }
    }
}
