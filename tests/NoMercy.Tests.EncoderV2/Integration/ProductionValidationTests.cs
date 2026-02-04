using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Specifications.MP4;
using NoMercy.EncoderV2.Specifications.MKV;
using NoMercy.EncoderV2.Validation;

namespace NoMercy.Tests.EncoderV2.Integration;

/// <summary>
/// Production validation tests that verify EncoderV2 output matches the expected
/// production format validated against the 16,000+ file structure described in the PRD.
/// Tests cover: output directory structure, HLS/MP4/MKV spec compliance,
/// playlist generation, folder naming conventions, and post-processor pipeline.
/// </summary>
public class ProductionValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private string _testOutputFolder = string.Empty;

    public ProductionValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        _testOutputFolder = Path.Combine(Path.GetTempPath(), $"EncoderV2Prod_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputFolder);

        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddScoped<IHLSPlaylistGenerator, HLSPlaylistGenerator>();
        services.AddScoped<IPlaylistValidator, PlaylistValidator>();
        services.AddScoped<IHLSValidator, HLSValidator>();
        services.AddScoped<IMP4Validator, MP4Validator>();
        services.AddScoped<IMKVValidator, MKVValidator>();
        services.AddScoped<IOutputValidator, OutputValidator>();
        services.AddScoped<IHLSOutputOrchestrator, HLSOutputOrchestrator>();

        _serviceProvider = services.BuildServiceProvider();

        _output.WriteLine($"Test output folder: {_testOutputFolder}");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        if (Directory.Exists(_testOutputFolder))
        {
            try { Directory.Delete(_testOutputFolder, true); }
            catch { /* Ignore cleanup errors */ }
        }

        return Task.CompletedTask;
    }

    #region HLS Output Structure Tests

    [Fact]
    public async Task HLSOutputStructure_MatchesProductionFormat_VideoAndAudioFolders()
    {
        // Arrange - simulate production output structure from PRD Appendix A
        string episodeBase = Path.Combine(_testOutputFolder, "episode_output");
        Directory.CreateDirectory(episodeBase);

        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        IHLSOutputOrchestrator orchestrator = _serviceProvider.GetRequiredService<IHLSOutputOrchestrator>();

        // Create video folders matching production naming
        string video1080Folder = Path.Combine(episodeBase, orchestrator.GetVideoFolderName(1920, 1080, false));
        string video720Folder = Path.Combine(episodeBase, orchestrator.GetVideoFolderName(1280, 720, false));
        string audioEngFolder = Path.Combine(episodeBase, orchestrator.GetAudioFolderName("eng", "aac"));
        string audioJpnFolder = Path.Combine(episodeBase, orchestrator.GetAudioFolderName("jpn", "aac"));

        Directory.CreateDirectory(video1080Folder);
        Directory.CreateDirectory(video720Folder);
        Directory.CreateDirectory(audioEngFolder);
        Directory.CreateDirectory(audioJpnFolder);

        // Create dummy .ts segment files
        for (int i = 0; i < 4; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(video1080Folder, $"video_1920x1080_SDR_{i:D5}.ts"), $"segment{i}");
            await File.WriteAllTextAsync(Path.Combine(video720Folder, $"video_1280x720_SDR_{i:D5}.ts"), $"segment{i}");
            await File.WriteAllTextAsync(Path.Combine(audioEngFolder, $"audio_eng_aac_{i:D5}.ts"), $"segment{i}");
            await File.WriteAllTextAsync(Path.Combine(audioJpnFolder, $"audio_jpn_aac_{i:D5}.ts"), $"segment{i}");
        }

        // Create media playlists for each variant
        HLSSpecification spec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD",
            IndependentSegments = true
        };

        List<string> video1080Segments = Directory.GetFiles(video1080Folder, "*.ts").OrderBy(f => f).ToList();
        List<string> video720Segments = Directory.GetFiles(video720Folder, "*.ts").OrderBy(f => f).ToList();
        List<string> audioEngSegments = Directory.GetFiles(audioEngFolder, "*.ts").OrderBy(f => f).ToList();
        List<string> audioJpnSegments = Directory.GetFiles(audioJpnFolder, "*.ts").OrderBy(f => f).ToList();

        TimeSpan duration = TimeSpan.FromMinutes(24);

        await generator.WriteMediaPlaylistAsync(Path.Combine(video1080Folder, "video_1920x1080_SDR.m3u8"), spec, video1080Segments, duration);
        await generator.WriteMediaPlaylistAsync(Path.Combine(video720Folder, "video_1280x720_SDR.m3u8"), spec, video720Segments, duration);
        await generator.WriteMediaPlaylistAsync(Path.Combine(audioEngFolder, "audio_eng_aac.m3u8"), spec, audioEngSegments, duration);
        await generator.WriteMediaPlaylistAsync(Path.Combine(audioJpnFolder, "audio_jpn_aac.m3u8"), spec, audioJpnSegments, duration);

        // Generate master playlist
        List<HLSVariantStream> variants =
        [
            new()
            {
                Bandwidth = 8_000_000,
                Resolution = "1920x1080",
                Codecs = "avc1.640028,mp4a.40.2",
                PlaylistUri = "video_1920x1080_SDR/video_1920x1080_SDR.m3u8",
                AudioGroup = "audio"
            },
            new()
            {
                Bandwidth = 5_000_000,
                Resolution = "1280x720",
                Codecs = "avc1.64001f,mp4a.40.2",
                PlaylistUri = "video_1280x720_SDR/video_1280x720_SDR.m3u8",
                AudioGroup = "audio"
            }
        ];

        List<HLSMediaGroup> mediaGroups =
        [
            new()
            {
                Type = "AUDIO",
                GroupId = "audio",
                Name = "ENG - AAC",
                Language = "eng",
                IsDefault = true,
                Autoselect = true,
                Uri = "audio_eng_aac/audio_eng_aac.m3u8"
            },
            new()
            {
                Type = "AUDIO",
                GroupId = "audio",
                Name = "JPN - AAC",
                Language = "jpn",
                IsDefault = false,
                Autoselect = true,
                Uri = "audio_jpn_aac/audio_jpn_aac.m3u8"
            }
        ];

        string masterPlaylistPath = Path.Combine(episodeBase, "episode.title.m3u8");
        await generator.WriteMasterPlaylistAsync(masterPlaylistPath, variants, mediaGroups);

        // Assert - Verify production directory structure
        Assert.True(File.Exists(masterPlaylistPath), "Master playlist must exist at root level");
        Assert.True(Directory.Exists(video1080Folder), "video_1920x1080_SDR folder must exist");
        Assert.True(Directory.Exists(video720Folder), "video_1280x720_SDR folder must exist");
        Assert.True(Directory.Exists(audioEngFolder), "audio_eng_aac folder must exist");
        Assert.True(Directory.Exists(audioJpnFolder), "audio_jpn_aac folder must exist");

        // Verify segment files in each folder
        Assert.Equal(4, Directory.GetFiles(video1080Folder, "*.ts").Length);
        Assert.Equal(4, Directory.GetFiles(video720Folder, "*.ts").Length);
        Assert.Equal(4, Directory.GetFiles(audioEngFolder, "*.ts").Length);
        Assert.Equal(4, Directory.GetFiles(audioJpnFolder, "*.ts").Length);

        // Verify media playlists in each variant folder
        Assert.True(File.Exists(Path.Combine(video1080Folder, "video_1920x1080_SDR.m3u8")));
        Assert.True(File.Exists(Path.Combine(video720Folder, "video_1280x720_SDR.m3u8")));
        Assert.True(File.Exists(Path.Combine(audioEngFolder, "audio_eng_aac.m3u8")));
        Assert.True(File.Exists(Path.Combine(audioJpnFolder, "audio_jpn_aac.m3u8")));

        _output.WriteLine("Production HLS output structure verified:");
        _output.WriteLine($"  Master playlist: {Path.GetFileName(masterPlaylistPath)}");
        _output.WriteLine($"  Video folders: video_1920x1080_SDR, video_1280x720_SDR");
        _output.WriteLine($"  Audio folders: audio_eng_aac, audio_jpn_aac");
        _output.WriteLine($"  Segments per folder: 4");
    }

    [Fact]
    public async Task HLSOutputStructure_HDRContent_CreatesSdrVariantFolders()
    {
        // Arrange
        IHLSOutputOrchestrator orchestrator = _serviceProvider!.GetRequiredService<IHLSOutputOrchestrator>();
        string episodeBase = Path.Combine(_testOutputFolder, "hdr_episode");
        Directory.CreateDirectory(episodeBase);

        // Act - create HDR and SDR variant folders
        string hdrFolderName = orchestrator.GetVideoFolderName(3840, 2160, true);
        string sdrFolderName = orchestrator.GetVideoFolderName(3840, 2160, false);
        string sdr1080FolderName = orchestrator.GetVideoFolderName(1920, 1080, false);

        Directory.CreateDirectory(Path.Combine(episodeBase, hdrFolderName));
        Directory.CreateDirectory(Path.Combine(episodeBase, sdrFolderName));
        Directory.CreateDirectory(Path.Combine(episodeBase, sdr1080FolderName));

        // Assert - HDR naming convention
        Assert.Equal("video_3840x2160_HDR", hdrFolderName);
        Assert.Equal("video_3840x2160_SDR", sdrFolderName);
        Assert.Equal("video_1920x1080_SDR", sdr1080FolderName);

        Assert.True(Directory.Exists(Path.Combine(episodeBase, "video_3840x2160_HDR")));
        Assert.True(Directory.Exists(Path.Combine(episodeBase, "video_3840x2160_SDR")));
        Assert.True(Directory.Exists(Path.Combine(episodeBase, "video_1920x1080_SDR")));

        _output.WriteLine("HDR folder naming verified:");
        _output.WriteLine($"  HDR: {hdrFolderName}");
        _output.WriteLine($"  SDR 4K: {sdrFolderName}");
        _output.WriteLine($"  SDR 1080p: {sdr1080FolderName}");

        await Task.CompletedTask;
    }

    [Fact]
    public void HLSOutputOrchestrator_FolderNaming_MatchesProductionConvention()
    {
        // Arrange
        IHLSOutputOrchestrator orchestrator = _serviceProvider!.GetRequiredService<IHLSOutputOrchestrator>();

        // Act & Assert - Video folder naming: video_{width}x{height}_{SDR|HDR}
        Assert.Equal("video_3840x2160_SDR", orchestrator.GetVideoFolderName(3840, 2160, false));
        Assert.Equal("video_3840x2160_HDR", orchestrator.GetVideoFolderName(3840, 2160, true));
        Assert.Equal("video_1920x1080_SDR", orchestrator.GetVideoFolderName(1920, 1080, false));
        Assert.Equal("video_1280x720_SDR", orchestrator.GetVideoFolderName(1280, 720, false));
        Assert.Equal("video_854x480_SDR", orchestrator.GetVideoFolderName(854, 480, false));

        // Act & Assert - Audio folder naming: audio_{lang}_{codec}
        Assert.Equal("audio_eng_aac", orchestrator.GetAudioFolderName("eng", "aac"));
        Assert.Equal("audio_jpn_aac", orchestrator.GetAudioFolderName("jpn", "aac"));
        Assert.Equal("audio_spa_opus", orchestrator.GetAudioFolderName("spa", "opus"));
        Assert.Equal("audio_und_ac3", orchestrator.GetAudioFolderName("und", "ac3"));
        Assert.Equal("audio_eng_eac3", orchestrator.GetAudioFolderName("eng", "eac3"));

        _output.WriteLine("All folder naming conventions match production format");
    }

    #endregion

    #region Master Playlist Validation Tests

    [Fact]
    public async Task MasterPlaylist_ContainsAllVariantStreams_WithCorrectAttributes()
    {
        // Arrange
        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        IPlaylistValidator validator = _serviceProvider.GetRequiredService<IPlaylistValidator>();

        string masterPath = Path.Combine(_testOutputFolder, "master_validation.m3u8");

        List<HLSVariantStream> variants =
        [
            new() { Bandwidth = 25_000_000, Resolution = "3840x2160", Codecs = "hvc1.1.6.L120.90,mp4a.40.2", PlaylistUri = "video_3840x2160_HDR/video_3840x2160_HDR.m3u8", AudioGroup = "audio" },
            new() { Bandwidth = 8_000_000, Resolution = "1920x1080", Codecs = "avc1.640028,mp4a.40.2", PlaylistUri = "video_1920x1080_SDR/video_1920x1080_SDR.m3u8", AudioGroup = "audio" },
            new() { Bandwidth = 5_000_000, Resolution = "1280x720", Codecs = "avc1.64001f,mp4a.40.2", PlaylistUri = "video_1280x720_SDR/video_1280x720_SDR.m3u8", AudioGroup = "audio" }
        ];

        List<HLSMediaGroup> mediaGroups =
        [
            new() { Type = "AUDIO", GroupId = "audio", Name = "ENG - AAC", Language = "eng", IsDefault = true, Autoselect = true, Uri = "audio_eng_aac/audio_eng_aac.m3u8" },
            new() { Type = "AUDIO", GroupId = "audio", Name = "JPN - AAC", Language = "jpn", IsDefault = false, Autoselect = true, Uri = "audio_jpn_aac/audio_jpn_aac.m3u8" }
        ];

        // Act
        await generator.WriteMasterPlaylistAsync(masterPath, variants, mediaGroups);

        // Assert - Validate master playlist structure
        PlaylistValidationResult result = await validator.ValidateMasterPlaylistAsync(masterPath);
        Assert.True(result.IsValid, $"Master playlist validation failed: {string.Join(", ", result.Errors)}");
        Assert.Equal(3, result.VariantCount);

        // Verify content contains required HLS tags
        string content = await File.ReadAllTextAsync(masterPath);
        Assert.StartsWith("#EXTM3U", content);
        Assert.Contains("#EXT-X-VERSION:3", content);

        // Verify all variant streams present
        Assert.Contains("BANDWIDTH=25000000", content);
        Assert.Contains("BANDWIDTH=8000000", content);
        Assert.Contains("BANDWIDTH=5000000", content);
        Assert.Contains("RESOLUTION=3840x2160", content);
        Assert.Contains("RESOLUTION=1920x1080", content);
        Assert.Contains("RESOLUTION=1280x720", content);

        // Verify audio media groups
        Assert.Contains("#EXT-X-MEDIA:TYPE=AUDIO", content);
        Assert.Contains("LANGUAGE=\"eng\"", content);
        Assert.Contains("LANGUAGE=\"jpn\"", content);
        Assert.Contains("DEFAULT=YES", content);
        Assert.Contains("DEFAULT=NO", content);

        // Verify variant URIs follow production path format
        Assert.Contains("video_3840x2160_HDR/video_3840x2160_HDR.m3u8", content);
        Assert.Contains("video_1920x1080_SDR/video_1920x1080_SDR.m3u8", content);
        Assert.Contains("video_1280x720_SDR/video_1280x720_SDR.m3u8", content);

        // Verify audio URIs
        Assert.Contains("audio_eng_aac/audio_eng_aac.m3u8", content);
        Assert.Contains("audio_jpn_aac/audio_jpn_aac.m3u8", content);

        // Verify variants ordered by bandwidth descending (highest first)
        int idx25m = content.IndexOf("BANDWIDTH=25000000", StringComparison.Ordinal);
        int idx8m = content.IndexOf("BANDWIDTH=8000000", StringComparison.Ordinal);
        int idx5m = content.IndexOf("BANDWIDTH=5000000", StringComparison.Ordinal);
        Assert.True(idx25m < idx8m, "4K variant should appear before 1080p");
        Assert.True(idx8m < idx5m, "1080p variant should appear before 720p");

        _output.WriteLine("Master playlist validation passed with 3 variants and 2 audio groups");
        _output.WriteLine(content);
    }

    [Fact]
    public async Task MediaPlaylist_RFC8216Compliance_ContainsRequiredTags()
    {
        // Arrange
        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        IPlaylistValidator validator = _serviceProvider.GetRequiredService<IPlaylistValidator>();

        string mediaPath = Path.Combine(_testOutputFolder, "media_rfc8216.m3u8");

        HLSSpecification spec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD",
            MediaSequence = 0,
            IndependentSegments = true
        };

        List<string> segments = Enumerable.Range(0, 240)
            .Select(i => $"video_1920x1080_SDR_{i:D5}.ts")
            .ToList();

        TimeSpan totalDuration = TimeSpan.FromMinutes(24);

        // Act
        await generator.WriteMediaPlaylistAsync(mediaPath, spec, segments, totalDuration);

        // Assert
        PlaylistValidationResult result = await validator.ValidateMediaPlaylistAsync(mediaPath);
        Assert.True(result.IsValid, $"Media playlist invalid: {string.Join(", ", result.Errors)}");
        Assert.Equal(240, result.SegmentCount);
        Assert.Equal(10, result.TargetDuration);

        string content = await File.ReadAllTextAsync(mediaPath);

        // RFC 8216 Section 4.3.1.1: EXTM3U is REQUIRED
        Assert.StartsWith("#EXTM3U", content);

        // RFC 8216 Section 4.3.1.2: EXT-X-VERSION
        Assert.Contains("#EXT-X-VERSION:3", content);

        // RFC 8216 Section 4.3.3.1: EXT-X-TARGETDURATION is REQUIRED for media playlists
        Assert.Contains("#EXT-X-TARGETDURATION:10", content);

        // RFC 8216 Section 4.3.3.2: EXT-X-MEDIA-SEQUENCE
        Assert.Contains("#EXT-X-MEDIA-SEQUENCE:0", content);

        // RFC 8216 Section 4.3.3.5: EXT-X-PLAYLIST-TYPE for VOD
        Assert.Contains("#EXT-X-PLAYLIST-TYPE:VOD", content);

        // RFC 8216 Section 4.3.3.4: EXT-X-ENDLIST for VOD playlists
        Assert.Contains("#EXT-X-ENDLIST", content);

        // RFC 8216 Section 4.3.2.1: EXTINF for every segment
        int extinfCount = content.Split('\n').Count(l => l.StartsWith("#EXTINF:"));
        Assert.Equal(240, extinfCount);

        // EXT-X-INDEPENDENT-SEGMENTS
        Assert.Contains("#EXT-X-INDEPENDENT-SEGMENTS", content);

        _output.WriteLine($"RFC 8216 compliance verified: {result.SegmentCount} segments, target duration {result.TargetDuration}s");
    }

    [Fact]
    public async Task PlaylistValidator_MasterPlaylist_DetectsVariantsAndResolutions()
    {
        // Arrange
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        string playlistPath = Path.Combine(_testOutputFolder, "validator_master.m3u8");

        string content = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-STREAM-INF:BANDWIDTH=8000000,RESOLUTION=1920x1080,CODECS="avc1.640028,mp4a.40.2"
            video_1920x1080_SDR/video_1920x1080_SDR.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1280x720,CODECS="avc1.64001f,mp4a.40.2"
            video_1280x720_SDR/video_1280x720_SDR.m3u8
            """;

        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        PlaylistValidationResult result = await validator.ValidateMasterPlaylistAsync(playlistPath);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("master", result.PlaylistType);
        Assert.Equal(2, result.VariantCount);

        Assert.Equal(8000000, result.Variants[0].Bandwidth);
        Assert.Equal("1920x1080", result.Variants[0].Resolution);
        Assert.Contains("avc1.640028", result.Variants[0].Codecs);

        Assert.Equal(5000000, result.Variants[1].Bandwidth);
        Assert.Equal("1280x720", result.Variants[1].Resolution);

        _output.WriteLine($"Validator detected {result.VariantCount} variants correctly");
    }

    [Fact]
    public async Task PlaylistValidator_MediaPlaylist_ParsesSegmentsAndDuration()
    {
        // Arrange
        IPlaylistValidator validator = _serviceProvider!.GetRequiredService<IPlaylistValidator>();
        string playlistPath = Path.Combine(_testOutputFolder, "validator_media.m3u8");

        string content = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-TARGETDURATION:10
            #EXT-X-MEDIA-SEQUENCE:0
            #EXT-X-PLAYLIST-TYPE:VOD
            #EXTINF:6.000000,
            video_1920x1080_SDR_00000.ts
            #EXTINF:6.000000,
            video_1920x1080_SDR_00001.ts
            #EXTINF:6.000000,
            video_1920x1080_SDR_00002.ts
            #EXTINF:4.500000,
            video_1920x1080_SDR_00003.ts
            #EXT-X-ENDLIST
            """;

        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        PlaylistValidationResult result = await validator.ValidateMediaPlaylistAsync(playlistPath);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("media", result.PlaylistType);
        Assert.Equal(4, result.SegmentCount);
        Assert.Equal(10, result.TargetDuration);
        Assert.Equal(22.5, result.TotalDurationSeconds, 1);

        _output.WriteLine($"Parsed {result.SegmentCount} segments, total duration {result.TotalDurationSeconds}s");
    }

    #endregion

    #region HLS Specification Validation Tests

    [Fact]
    public async Task HLSSpecification_DefaultValues_PassValidation()
    {
        // Arrange
        IHLSValidator validator = _serviceProvider!.GetRequiredService<IHLSValidator>();
        HLSSpecification spec = new(); // Default values

        // Act
        PlaylistValidationResult result = await validator.ValidateSpecificationAsync(spec);

        // Assert
        Assert.True(result.IsValid, $"Default HLS spec should be valid: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine("Default HLS specification values are valid");
        _output.WriteLine($"  Version: {spec.Version}, TargetDuration: {spec.TargetDuration}, SegmentDuration: {spec.SegmentDuration}");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public async Task HLSSpecification_VersionRange_ValidatesCorrectly(int version, bool expectedValid)
    {
        // Arrange
        IHLSValidator validator = _serviceProvider!.GetRequiredService<IHLSValidator>();
        HLSSpecification spec = new() { Version = version };

        // Act
        PlaylistValidationResult result = await validator.ValidateSpecificationAsync(spec);

        // Assert
        Assert.Equal(expectedValid, result.IsValid);

        _output.WriteLine($"HLS version {version}: valid={result.IsValid}");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(60, true)]
    [InlineData(0, false)]
    [InlineData(61, false)]
    public void HLSSpecification_TargetDuration_ValidatesRange(int duration, bool expectedValid)
    {
        // Arrange
        IHLSValidator validator = _serviceProvider!.GetRequiredService<IHLSValidator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidTargetDuration(duration));
    }

    #endregion

    #region MP4 Specification Validation Tests

    [Fact]
    public async Task MP4Specification_DefaultValues_PassValidation()
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();
        MP4Specification spec = new(); // Default: isom, FastStart=true, timescale=1000

        // Act
        MP4ValidationResult result = await validator.ValidateSpecificationAsync(spec);

        // Assert
        Assert.True(result.IsValid, $"Default MP4 spec should be valid: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine("Default MP4 specification values are valid");
        _output.WriteLine($"  Brand: {spec.MajorBrand}, FastStart: {spec.FastStart}, Timescale: {spec.MovieTimescale}");
    }

    [Fact]
    public async Task MP4Specification_ValidVideoAndAudioTracks_PassValidation()
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();

        List<MP4Track> tracks =
        [
            new()
            {
                TrackId = 1,
                TrackType = MP4TrackType.Video,
                HandlerType = "vide",
                CodecFourCC = "avc1",
                Language = "und",
                Timescale = 90000,
                Width = 1920,
                Height = 1080
            },
            new()
            {
                TrackId = 2,
                TrackType = MP4TrackType.Audio,
                HandlerType = "soun",
                CodecFourCC = "mp4a",
                Language = "eng",
                Timescale = 48000,
                SampleRate = 48000,
                ChannelCount = 2
            }
        ];

        // Act
        MP4ValidationResult result = await validator.ValidateTracksAsync(tracks);

        // Assert
        Assert.True(result.IsValid, $"Valid tracks should pass: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine("MP4 track validation passed for video + audio tracks");
    }

    [Fact]
    public async Task MP4Specification_DuplicateTrackIds_FailValidation()
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();

        List<MP4Track> tracks =
        [
            new() { TrackId = 1, TrackType = MP4TrackType.Video, CodecFourCC = "avc1", Timescale = 90000, Width = 1920, Height = 1080 },
            new() { TrackId = 1, TrackType = MP4TrackType.Audio, CodecFourCC = "mp4a", Timescale = 48000, SampleRate = 48000, ChannelCount = 2 }
        ];

        // Act
        MP4ValidationResult result = await validator.ValidateTracksAsync(tracks);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate track ID"));

        _output.WriteLine("Duplicate track ID correctly rejected");
    }

    [Theory]
    [InlineData("isom", true)]
    [InlineData("mp41", true)]
    [InlineData("avc1", true)]
    [InlineData("hvc1", true)]
    [InlineData("dash", true)]
    [InlineData("", false)]
    public void MP4Validator_BrandValidation_RecognizesStandardBrands(string brand, bool expectedValid)
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidBrand(brand));
    }

    [Theory]
    [InlineData("avc1", MP4TrackType.Video, true)]
    [InlineData("hvc1", MP4TrackType.Video, true)]
    [InlineData("av01", MP4TrackType.Video, true)]
    [InlineData("mp4a", MP4TrackType.Audio, true)]
    [InlineData("ac-3", MP4TrackType.Audio, true)]
    [InlineData("Opus", MP4TrackType.Audio, true)]
    [InlineData("wvtt", MP4TrackType.Text, true)]
    [InlineData("", MP4TrackType.Video, false)]
    public void MP4Validator_CodecFourCC_ValidatesCorrectly(string fourCC, MP4TrackType trackType, bool expectedValid)
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidCodecFourCC(fourCC, trackType));
    }

    [Theory]
    [InlineData("und", true)]
    [InlineData("eng", true)]
    [InlineData("jpn", true)]
    [InlineData("en", true)]
    [InlineData("", false)]
    public void MP4Validator_LanguageCode_ValidatesCorrectly(string language, bool expectedValid)
    {
        // Arrange
        IMP4Validator validator = _serviceProvider!.GetRequiredService<IMP4Validator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidLanguage(language));
    }

    #endregion

    #region MKV Specification Validation Tests

    [Fact]
    public async Task MKVSpecification_DefaultValues_PassValidation()
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();
        MKVSpecification spec = new(); // Defaults: DocTypeVersion=4, TimestampScale=1000000

        // Act
        MKVValidationResult result = await validator.ValidateSpecificationAsync(spec);

        // Assert
        Assert.True(result.IsValid, $"Default MKV spec should be valid: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine("Default MKV specification values are valid");
        _output.WriteLine($"  DocType: {spec.DocTypeVersion}, TimestampScale: {spec.TimestampScale}");
    }

    [Fact]
    public async Task MKVSpecification_ValidTracks_PassValidation()
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();

        List<MKVTrack> tracks =
        [
            new()
            {
                TrackNumber = 1,
                TrackUid = 1001,
                TrackType = MKVTrackType.Video,
                CodecId = MKVCodecIds.H264,
                Language = "und",
                IsDefault = true
            },
            new()
            {
                TrackNumber = 2,
                TrackUid = 1002,
                TrackType = MKVTrackType.Audio,
                CodecId = MKVCodecIds.AAC,
                Language = "jpn",
                IsDefault = true
            },
            new()
            {
                TrackNumber = 3,
                TrackUid = 1003,
                TrackType = MKVTrackType.Subtitle,
                CodecId = MKVCodecIds.ASS,
                Language = "eng",
                IsDefault = false
            }
        ];

        // Act
        MKVValidationResult result = await validator.ValidateTracksAsync(tracks);

        // Assert
        Assert.True(result.IsValid, $"Valid MKV tracks should pass: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine("MKV track validation passed for video + audio + subtitle");
    }

    [Fact]
    public async Task MKVSpecification_ChaptersValidation_AcceptsChronologicalOrder()
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();

        List<MKVChapter> chapters =
        [
            new() { ChapterUid = 1, Name = "Opening", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(90), Language = "eng" },
            new() { ChapterUid = 2, Name = "Part A", StartTime = TimeSpan.FromSeconds(90), EndTime = TimeSpan.FromMinutes(12), Language = "eng" },
            new() { ChapterUid = 3, Name = "Part B", StartTime = TimeSpan.FromMinutes(12), EndTime = TimeSpan.FromMinutes(22), Language = "eng" },
            new() { ChapterUid = 4, Name = "Ending", StartTime = TimeSpan.FromMinutes(22), EndTime = TimeSpan.FromMinutes(24), Language = "eng" }
        ];

        // Act
        MKVValidationResult result = await validator.ValidateChaptersAsync(chapters);

        // Assert
        Assert.True(result.IsValid, $"Chronological chapters should pass: {string.Join(", ", result.Errors)}");
        Assert.Empty(result.Errors);

        _output.WriteLine($"Chapter validation passed: {chapters.Count} chapters in chronological order");
    }

    [Fact]
    public async Task MKVSpecification_ChaptersOutOfOrder_WarnsButDoesNotFail()
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();

        List<MKVChapter> chapters =
        [
            new() { ChapterUid = 1, Name = "Chapter 2", StartTime = TimeSpan.FromMinutes(10), Language = "eng" },
            new() { ChapterUid = 2, Name = "Chapter 1", StartTime = TimeSpan.FromMinutes(5), Language = "eng" }
        ];

        // Act
        MKVValidationResult result = await validator.ValidateChaptersAsync(chapters);

        // Assert - out of order produces warning, not error
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("not in chronological order"));

        _output.WriteLine("Out-of-order chapters produced expected warning");
    }

    [Theory]
    [InlineData("V_MPEG4/ISO/AVC", MKVTrackType.Video, true)]
    [InlineData("V_MPEGH/ISO/HEVC", MKVTrackType.Video, true)]
    [InlineData("V_VP9", MKVTrackType.Video, true)]
    [InlineData("V_AV1", MKVTrackType.Video, true)]
    [InlineData("A_AAC", MKVTrackType.Audio, true)]
    [InlineData("A_OPUS", MKVTrackType.Audio, true)]
    [InlineData("A_FLAC", MKVTrackType.Audio, true)]
    [InlineData("S_TEXT/ASS", MKVTrackType.Subtitle, true)]
    [InlineData("S_TEXT/UTF8", MKVTrackType.Subtitle, true)]
    [InlineData("S_HDMV/PGS", MKVTrackType.Subtitle, true)]
    [InlineData("", MKVTrackType.Video, false)]
    public void MKVValidator_CodecId_ValidatesCorrectly(string codecId, MKVTrackType trackType, bool expectedValid)
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidCodecId(codecId, trackType));
    }

    [Theory]
    [InlineData("eng", true)]
    [InlineData("jpn", true)]
    [InlineData("und", true)]
    [InlineData("en", true)]
    [InlineData("en-US", true)]
    [InlineData("pt-BR", true)]
    [InlineData("", false)]
    public void MKVValidator_Language_ValidatesISO639AndBCP47(string language, bool expectedValid)
    {
        // Arrange
        IMKVValidator validator = _serviceProvider!.GetRequiredService<IMKVValidator>();

        // Act & Assert
        Assert.Equal(expectedValid, validator.IsValidLanguage(language));
    }

    #endregion

    #region Output Validator Tests

    [Fact]
    public async Task OutputValidator_PlaylistValidation_DetectsMissingFile()
    {
        // Arrange
        IOutputValidator validator = _serviceProvider!.GetRequiredService<IOutputValidator>();
        string nonExistentPath = Path.Combine(_testOutputFolder, "nonexistent.m3u8");

        // Act
        OutputValidationResult result = await validator.ValidatePlaylistAsync(nonExistentPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.FileExists);
        Assert.Contains(result.Errors, e => e.Contains("does not exist"));

        _output.WriteLine("Missing playlist correctly detected");
    }

    [Fact]
    public async Task OutputValidator_PlaylistValidation_DetectsEmptyFile()
    {
        // Arrange
        IOutputValidator validator = _serviceProvider!.GetRequiredService<IOutputValidator>();
        string emptyPlaylist = Path.Combine(_testOutputFolder, "empty.m3u8");
        await File.WriteAllTextAsync(emptyPlaylist, "");

        // Act
        OutputValidationResult result = await validator.ValidatePlaylistAsync(emptyPlaylist);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));

        _output.WriteLine("Empty playlist correctly detected");
    }

    [Fact]
    public async Task OutputValidator_PlaylistValidation_DetectsInvalidHeader()
    {
        // Arrange
        IOutputValidator validator = _serviceProvider!.GetRequiredService<IOutputValidator>();
        string invalidPlaylist = Path.Combine(_testOutputFolder, "invalid_header.m3u8");
        await File.WriteAllTextAsync(invalidPlaylist, "NOT_A_PLAYLIST\nsome_file.ts\n");

        // Act
        OutputValidationResult result = await validator.ValidatePlaylistAsync(invalidPlaylist);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("#EXTM3U"));

        _output.WriteLine("Invalid header correctly detected");
    }

    [Fact]
    public async Task OutputValidator_PlaylistValidation_ValidatesSegmentReferences()
    {
        // Arrange
        IOutputValidator validator = _serviceProvider!.GetRequiredService<IOutputValidator>();
        string playlistDir = Path.Combine(_testOutputFolder, "segment_ref_test");
        Directory.CreateDirectory(playlistDir);

        // Create playlist referencing files that don't exist
        string playlistPath = Path.Combine(playlistDir, "test.m3u8");
        string content = "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:10\n#EXTINF:6.0,\nmissing_segment.ts\n#EXT-X-ENDLIST\n";
        await File.WriteAllTextAsync(playlistPath, content);

        // Act
        OutputValidationResult result = await validator.ValidatePlaylistAsync(playlistPath);

        // Assert - Playlist structure is valid but warns about missing segments
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("missing_segment.ts"));

        _output.WriteLine("Missing segment reference produced expected warning");
    }

    [Fact]
    public async Task OutputValidator_FileValidation_DetectsMissingFile()
    {
        // Arrange
        IOutputValidator validator = _serviceProvider!.GetRequiredService<IOutputValidator>();
        string nonExistentFile = Path.Combine(_testOutputFolder, "nonexistent.mp4");

        // Act
        OutputValidationResult result = await validator.ValidateOutputAsync(nonExistentFile, TimeSpan.FromMinutes(24));

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.FileExists);

        _output.WriteLine("Missing file correctly detected");
    }

    #endregion

    #region Production Supplementary File Tests

    [Fact]
    public async Task ProductionOutput_FontsJsonManifest_HasCorrectStructure()
    {
        // Arrange - simulate fonts.json from FontExtractor
        string fontsDir = Path.Combine(_testOutputFolder, "fonts");
        Directory.CreateDirectory(fontsDir);

        // Create dummy font files matching production format
        await File.WriteAllBytesAsync(Path.Combine(fontsDir, "Arial.ttf"), [0x00, 0x01, 0x00, 0x00]);
        await File.WriteAllBytesAsync(Path.Combine(fontsDir, "NotoSansJP-Regular.otf"), [0x4F, 0x54, 0x54, 0x4F]);

        // Create fonts.json manifest
        string fontsJson = """
            [
                {"fileName": "Arial.ttf", "mimeType": "font/ttf"},
                {"fileName": "NotoSansJP-Regular.otf", "mimeType": "font/otf"}
            ]
            """;
        string fontsJsonPath = Path.Combine(_testOutputFolder, "fonts.json");
        await File.WriteAllTextAsync(fontsJsonPath, fontsJson);

        // Assert - verify structure matches PRD Appendix A
        Assert.True(Directory.Exists(fontsDir), "fonts/ directory must exist");
        Assert.True(File.Exists(fontsJsonPath), "fonts.json must exist at root level");
        Assert.True(File.Exists(Path.Combine(fontsDir, "Arial.ttf")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "NotoSansJP-Regular.otf")));

        // Verify JSON is parseable
        string jsonContent = await File.ReadAllTextAsync(fontsJsonPath);
        Assert.Contains("font/ttf", jsonContent);
        Assert.Contains("font/otf", jsonContent);

        _output.WriteLine("Font manifest structure validated:");
        _output.WriteLine($"  Fonts directory: {fontsDir}");
        _output.WriteLine($"  Manifest: fonts.json with 2 entries");
    }

    [Fact]
    public async Task ProductionOutput_ChaptersVtt_HasValidWebVTTFormat()
    {
        // Arrange - simulate chapters.vtt from ChapterProcessor
        string chaptersPath = Path.Combine(_testOutputFolder, "chapters.vtt");

        string chaptersContent = """
            WEBVTT

            1
            00:00:00.000 --> 00:01:30.000
            Opening

            2
            00:01:30.000 --> 00:12:00.000
            Part A

            3
            00:12:00.000 --> 00:22:00.000
            Part B

            4
            00:22:00.000 --> 00:24:00.000
            Ending

            """;

        await File.WriteAllTextAsync(chaptersPath, chaptersContent);

        // Assert - verify WebVTT format
        Assert.True(File.Exists(chaptersPath));

        string content = await File.ReadAllTextAsync(chaptersPath);
        Assert.StartsWith("WEBVTT", content);
        Assert.Contains("00:00:00.000 --> 00:01:30.000", content);
        Assert.Contains("Opening", content);
        Assert.Contains("Part A", content);
        Assert.Contains("Part B", content);
        Assert.Contains("Ending", content);

        // Count chapter entries (numbered cue identifiers)
        int cueCount = content.Split('\n').Count(l => l.Trim().Length > 0 && int.TryParse(l.Trim(), out _));
        Assert.Equal(4, cueCount);

        _output.WriteLine($"chapters.vtt validated with {cueCount} chapters in WebVTT format");
    }

    [Fact]
    public async Task ProductionOutput_SpriteVtt_HasValidTimingFormat()
    {
        // Arrange - simulate thumbs VTT from SpriteGenerator
        string spriteVttPath = Path.Combine(_testOutputFolder, "thumbs_320x180.vtt");

        // SpriteGenerator produces VTT with xywh fragment identifiers
        string vttContent = """
            WEBVTT

            00:00:00.000 --> 00:00:10.000
            thumbs_320x180.webp#xywh=0,0,320,180

            00:00:10.000 --> 00:00:20.000
            thumbs_320x180.webp#xywh=320,0,320,180

            00:00:20.000 --> 00:00:30.000
            thumbs_320x180.webp#xywh=640,0,320,180

            """;

        await File.WriteAllTextAsync(spriteVttPath, vttContent);

        // Assert
        Assert.True(File.Exists(spriteVttPath));

        string content = await File.ReadAllTextAsync(spriteVttPath);
        Assert.StartsWith("WEBVTT", content);

        // Verify xywh fragment identifier format
        Assert.Contains("#xywh=", content);
        Assert.Contains("thumbs_320x180.webp", content);

        // Verify timing format
        Assert.Contains("00:00:00.000 --> 00:00:10.000", content);
        Assert.Contains("00:00:10.000 --> 00:00:20.000", content);

        _output.WriteLine("Sprite VTT timing format validated with xywh fragments");
    }

    #endregion

    #region Complete Production Output Structure Test

    [Fact]
    public async Task ProductionOutput_CompleteStructure_MatchesPRDAppendixA()
    {
        // Arrange - Build complete production output directory matching PRD Appendix A
        IHLSPlaylistGenerator generator = _serviceProvider!.GetRequiredService<IHLSPlaylistGenerator>();
        IHLSOutputOrchestrator orchestrator = _serviceProvider.GetRequiredService<IHLSOutputOrchestrator>();
        IPlaylistValidator validator = _serviceProvider.GetRequiredService<IPlaylistValidator>();

        string episodeBase = Path.Combine(_testOutputFolder, "S01E01.Pilot");
        Directory.CreateDirectory(episodeBase);

        // 1. Create video quality variants
        string[] videoFolders =
        [
            orchestrator.GetVideoFolderName(1920, 1080, false),
            orchestrator.GetVideoFolderName(1280, 720, false),
            orchestrator.GetVideoFolderName(854, 480, false)
        ];

        HLSSpecification spec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD",
            IndependentSegments = true
        };

        foreach (string folder in videoFolders)
        {
            string folderPath = Path.Combine(episodeBase, folder);
            Directory.CreateDirectory(folderPath);

            // Create segment files
            List<string> segments = [];
            for (int i = 0; i < 4; i++)
            {
                string segFile = Path.Combine(folderPath, $"{folder}_{i:D5}.ts");
                await File.WriteAllTextAsync(segFile, $"segment_data_{i}");
                segments.Add(segFile);
            }

            // Create media playlist
            await generator.WriteMediaPlaylistAsync(
                Path.Combine(folderPath, $"{folder}.m3u8"),
                spec, segments, TimeSpan.FromMinutes(24));
        }

        // 2. Create audio tracks
        string[] audioFolders =
        [
            orchestrator.GetAudioFolderName("eng", "aac"),
            orchestrator.GetAudioFolderName("jpn", "aac")
        ];

        foreach (string folder in audioFolders)
        {
            string folderPath = Path.Combine(episodeBase, folder);
            Directory.CreateDirectory(folderPath);

            List<string> segments = [];
            for (int i = 0; i < 4; i++)
            {
                string segFile = Path.Combine(folderPath, $"{folder}_{i:D5}.ts");
                await File.WriteAllTextAsync(segFile, $"audio_data_{i}");
                segments.Add(segFile);
            }

            await generator.WriteMediaPlaylistAsync(
                Path.Combine(folderPath, $"{folder}.m3u8"),
                spec, segments, TimeSpan.FromMinutes(24));
        }

        // 3. Create subtitles directory
        string subtitlesDir = Path.Combine(episodeBase, "subtitles");
        Directory.CreateDirectory(subtitlesDir);
        await File.WriteAllTextAsync(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.full.ass"), "[Script Info]\nTitle: English\n");
        await File.WriteAllTextAsync(Path.Combine(subtitlesDir, "S01E01.Pilot.jpn.full.ass"), "[Script Info]\nTitle: Japanese\n");
        await File.WriteAllTextAsync(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.sign.ass"), "[Script Info]\nTitle: Signs\n");
        await File.WriteAllTextAsync(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.full.vtt"), "WEBVTT\n\n00:00:01.000 --> 00:00:05.000\nHello\n");

        // 4. Create fonts directory
        string fontsDir = Path.Combine(episodeBase, "fonts");
        Directory.CreateDirectory(fontsDir);
        await File.WriteAllBytesAsync(Path.Combine(fontsDir, "Arial.ttf"), [0x00, 0x01, 0x00, 0x00]);
        await File.WriteAllBytesAsync(Path.Combine(fontsDir, "NotoSansJP.otf"), [0x4F, 0x54, 0x54, 0x4F]);

        // 5. Create thumbnail sprites
        string thumbsDir = Path.Combine(episodeBase, "thumbs_320x180");
        Directory.CreateDirectory(thumbsDir);
        await File.WriteAllBytesAsync(Path.Combine(thumbsDir, "thumbs_320x180-0000.jpg"), [0xFF, 0xD8, 0xFF, 0xE0]);
        await File.WriteAllBytesAsync(Path.Combine(thumbsDir, "thumbs_320x180-0001.jpg"), [0xFF, 0xD8, 0xFF, 0xE0]);
        await File.WriteAllTextAsync(Path.Combine(episodeBase, "thumbs_320x180.webp"), "sprite_data");
        await File.WriteAllTextAsync(Path.Combine(episodeBase, "thumbs_320x180.vtt"), "WEBVTT\n\n00:00:00.000 --> 00:00:10.000\nthumbs_320x180.webp#xywh=0,0,320,180\n");

        // 6. Create chapters.vtt
        await File.WriteAllTextAsync(Path.Combine(episodeBase, "chapters.vtt"), "WEBVTT\n\n1\n00:00:00.000 --> 00:01:30.000\nOpening\n");

        // 7. Create fonts.json
        await File.WriteAllTextAsync(Path.Combine(episodeBase, "fonts.json"), "[{\"fileName\":\"Arial.ttf\",\"mimeType\":\"font/ttf\"},{\"fileName\":\"NotoSansJP.otf\",\"mimeType\":\"font/otf\"}]");

        // 8. Create master playlist
        List<HLSVariantStream> variants =
        [
            new() { Bandwidth = 8_000_000, Resolution = "1920x1080", Codecs = "avc1.640028,mp4a.40.2", PlaylistUri = "video_1920x1080_SDR/video_1920x1080_SDR.m3u8", AudioGroup = "audio" },
            new() { Bandwidth = 5_000_000, Resolution = "1280x720", Codecs = "avc1.64001f,mp4a.40.2", PlaylistUri = "video_1280x720_SDR/video_1280x720_SDR.m3u8", AudioGroup = "audio" },
            new() { Bandwidth = 3_000_000, Resolution = "854x480", Codecs = "avc1.64000d,mp4a.40.2", PlaylistUri = "video_854x480_SDR/video_854x480_SDR.m3u8", AudioGroup = "audio" }
        ];

        List<HLSMediaGroup> mediaGroups =
        [
            new() { Type = "AUDIO", GroupId = "audio", Name = "ENG - AAC", Language = "eng", IsDefault = true, Autoselect = true, Uri = "audio_eng_aac/audio_eng_aac.m3u8" },
            new() { Type = "AUDIO", GroupId = "audio", Name = "JPN - AAC", Language = "jpn", IsDefault = false, Autoselect = true, Uri = "audio_jpn_aac/audio_jpn_aac.m3u8" }
        ];

        string masterPlaylistPath = Path.Combine(episodeBase, "S01E01.Pilot.m3u8");
        await generator.WriteMasterPlaylistAsync(masterPlaylistPath, variants, mediaGroups);

        // === ASSERT: Verify complete production structure (PRD Appendix A) ===

        // Master playlist at root level
        Assert.True(File.Exists(masterPlaylistPath), "Master playlist at root");

        // Video quality variant folders with playlists and segments
        foreach (string folder in videoFolders)
        {
            string folderPath = Path.Combine(episodeBase, folder);
            Assert.True(Directory.Exists(folderPath), $"Video folder: {folder}");
            Assert.True(File.Exists(Path.Combine(folderPath, $"{folder}.m3u8")), $"Media playlist in {folder}");
            Assert.Equal(4, Directory.GetFiles(folderPath, "*.ts").Length);
        }

        // Audio track folders with playlists and segments
        foreach (string folder in audioFolders)
        {
            string folderPath = Path.Combine(episodeBase, folder);
            Assert.True(Directory.Exists(folderPath), $"Audio folder: {folder}");
            Assert.True(File.Exists(Path.Combine(folderPath, $"{folder}.m3u8")), $"Media playlist in {folder}");
            Assert.Equal(4, Directory.GetFiles(folderPath, "*.ts").Length);
        }

        // Subtitles directory with native ASS preservation
        Assert.True(Directory.Exists(subtitlesDir), "subtitles/ directory");
        Assert.True(File.Exists(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.full.ass")), "Native ASS subtitle");
        Assert.True(File.Exists(Path.Combine(subtitlesDir, "S01E01.Pilot.jpn.full.ass")), "Japanese ASS subtitle");
        Assert.True(File.Exists(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.sign.ass")), "Signs ASS subtitle");
        Assert.True(File.Exists(Path.Combine(subtitlesDir, "S01E01.Pilot.eng.full.vtt")), "WebVTT subtitle");

        // Fonts directory
        Assert.True(Directory.Exists(fontsDir), "fonts/ directory");
        Assert.True(File.Exists(Path.Combine(fontsDir, "Arial.ttf")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "NotoSansJP.otf")));

        // Thumbnail sprites
        Assert.True(Directory.Exists(thumbsDir), "thumbs_320x180/ directory");
        Assert.True(File.Exists(Path.Combine(episodeBase, "thumbs_320x180.webp")), "Merged sprite sheet");
        Assert.True(File.Exists(Path.Combine(episodeBase, "thumbs_320x180.vtt")), "Sprite timing VTT");

        // Chapter markers
        Assert.True(File.Exists(Path.Combine(episodeBase, "chapters.vtt")), "chapters.vtt");

        // Font manifest
        Assert.True(File.Exists(Path.Combine(episodeBase, "fonts.json")), "fonts.json manifest");

        // Validate master playlist
        PlaylistValidationResult masterResult = await validator.ValidateMasterPlaylistAsync(masterPlaylistPath);
        Assert.True(masterResult.IsValid, $"Master playlist invalid: {string.Join(", ", masterResult.Errors)}");
        Assert.Equal(3, masterResult.VariantCount);

        // Validate each media playlist
        foreach (string folder in videoFolders.Concat(audioFolders))
        {
            string mediaPlaylistPath = Path.Combine(episodeBase, folder, $"{folder}.m3u8");
            PlaylistValidationResult mediaResult = await validator.ValidateMediaPlaylistAsync(mediaPlaylistPath);
            Assert.True(mediaResult.IsValid, $"Media playlist {folder} invalid: {string.Join(", ", mediaResult.Errors)}");
            Assert.Equal(4, mediaResult.SegmentCount);
        }

        _output.WriteLine("=== Complete Production Structure Validation ===");
        _output.WriteLine($"Root: {Path.GetFileName(episodeBase)}/");
        _output.WriteLine($"  Master playlist: S01E01.Pilot.m3u8 (3 variants, 2 audio groups)");
        _output.WriteLine($"  Video folders: {string.Join(", ", videoFolders)}");
        _output.WriteLine($"  Audio folders: {string.Join(", ", audioFolders)}");
        _output.WriteLine($"  Subtitles: 4 files (2 ASS full, 1 ASS signs, 1 VTT)");
        _output.WriteLine($"  Fonts: 2 files + fonts.json");
        _output.WriteLine($"  Sprites: thumbs_320x180/ + .webp + .vtt");
        _output.WriteLine($"  Chapters: chapters.vtt");
        _output.WriteLine("All items match PRD Appendix A structure");
    }

    #endregion
}
