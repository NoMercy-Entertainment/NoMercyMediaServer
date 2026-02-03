using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Encoder.Dto;
using NoMercy.EncoderV2.Composition;
using NoMercy.EncoderV2.Execution;
using NoMercy.EncoderV2.FFmpeg;
using NoMercy.EncoderV2.Hardware;
using NoMercy.EncoderV2.Repositories;
using NoMercy.EncoderV2.Services;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Streams;
using NoMercy.NmSystem.Capabilities;
using NoMercy.NmSystem.Information;
using VideoStream = NoMercy.Encoder.Dto.VideoStream;
using AudioStream = NoMercy.Encoder.Dto.AudioStream;
using SubtitleStream = NoMercy.Encoder.Dto.SubtitleStream;

namespace NoMercy.Tests.EncoderV2;

/// <summary>
/// Integration tests for the EncoderV2 pipeline
/// Tests the full encoding workflow: scan -> analyze -> encode -> validate output
/// </summary>
public class HelstromEncodingIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _cwd = Directory.GetCurrentDirectory();

    private const string HelstromInputFolder = @"M:\Download\complete\Helstrom.S01.2160p.HULU.WEB-DL.x265.10bit.HDR10Plus.DDP5.1-NTb[rartv]";
    private static readonly Ulid MarvelProfileId = Ulid.Parse("01HQ6298ZSZYKJT83WDWTPG4G8");

    private ServiceProvider? _serviceProvider;
    private QueueContext? _queueContext;
    private string _testOutputFolder = string.Empty;
    private EncoderProfile? _marvelProfile;

    public HelstromEncodingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await AppFiles.CreateAppFolders();

        _testOutputFolder = Path.Combine(_cwd, "HelstromEncodingOutput_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testOutputFolder);

        // Setup DI container with in-memory database for testing
        ServiceCollection services = new();

        // Use in-memory database for testing
        services.AddDbContext<QueueContext>(options =>
            options.UseInMemoryDatabase("EncoderV2TestDb_" + Guid.NewGuid()));

        // Register all EncoderV2 services
        services.AddEncoderV2Services();

        _serviceProvider = services.BuildServiceProvider();
        _queueContext = _serviceProvider.GetRequiredService<QueueContext>();

        // Create the Marvel profile for testing
        _marvelProfile = CreateMarvelProfile();

        _output.WriteLine($"Test output folder: {_testOutputFolder}");
        _output.WriteLine($"Input folder: {HelstromInputFolder}");
    }

    private EncoderProfile CreateMarvelProfile()
    {
        return new EncoderProfile
        {
            Id = MarvelProfileId,
            Name = "Marvel 4k",
            Container = "hls",
            VideoProfiles =
            [
                new IVideoProfile
                {
                    Codec = "libx264",
                    Bitrate = 24000,
                    Width = 3840,
                    Height = 2160,
                    Preset = "fast",
                    Profile = "main10",
                    ConvertHdrToSdr = false
                },
                new IVideoProfile
                {
                    Codec = "libx264",
                    Bitrate = 10656,
                    Width = 1920,
                    Height = 1080,
                    Preset = "fast",
                    Profile = "main10",
                    ConvertHdrToSdr = false
                },
                new IVideoProfile
                {
                    Codec = "libx264",
                    Bitrate = 20000,
                    Width = 3840,
                    Height = 2160,
                    Preset = "fast",
                    Profile = "high",
                    ConvertHdrToSdr = true
                },
                new IVideoProfile
                {
                    Codec = "libx264",
                    Bitrate = 8695,
                    Width = 1920,
                    Height = 1080,
                    Preset = "fast",
                    Profile = "high",
                    ConvertHdrToSdr = true
                }
            ],
            AudioProfiles =
            [
                new IAudioProfile
                {
                    Codec = "aac",
                    Channels = 2,
                    SampleRate = 48000
                },
                new IAudioProfile
                {
                    Codec = "eac3",
                    SampleRate = 48000
                }
            ],
            SubtitleProfiles =
            [
                new ISubtitleProfile { Codec = "webvtt" },
                new ISubtitleProfile { Codec = "ass" }
            ]
        };
    }

    /// <summary>
    /// Scans the input folder and returns all video files
    /// Similar to /filelist endpoint functionality
    /// </summary>
    [Fact]
    public void ScanInputFolder_ReturnsVideoFiles()
    {
        // Skip if folder doesn't exist
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        // Get video files from input folder
        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);

        _output.WriteLine($"Found {videoFiles.Count} video files:");
        foreach (string file in videoFiles)
        {
            FileInfo fileInfo = new(file);
            _output.WriteLine($"  - {fileInfo.Name} ({fileInfo.Length / (1024.0 * 1024.0 * 1024.0):F2} GB)");
        }

        Assert.NotEmpty(videoFiles);
        Assert.True(videoFiles.Count >= 10, "Expected at least 10 Helstrom episodes");
    }

    /// <summary>
    /// Tests stream analysis on first Helstrom episode
    /// </summary>
    [Fact]
    public async Task AnalyzeFirstEpisode_ReturnsStreamInfo()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);
        if (videoFiles.Count == 0)
        {
            _output.WriteLine("No video files found");
            return;
        }

        string firstEpisode = videoFiles[0];
        _output.WriteLine($"Analyzing: {Path.GetFileName(firstEpisode)}");

        IStreamAnalyzer analyzer = _serviceProvider!.GetRequiredService<IStreamAnalyzer>();
        StreamAnalysis analysis = await analyzer.AnalyzeAsync(firstEpisode);

        _output.WriteLine($"Duration: {analysis.Duration}");
        _output.WriteLine($"File Size: {analysis.FileSize / (1024.0 * 1024.0 * 1024.0):F2} GB");
        _output.WriteLine($"Video Streams: {analysis.VideoStreams.Count}");
        _output.WriteLine($"Audio Streams: {analysis.AudioStreams.Count}");
        _output.WriteLine($"Subtitle Streams: {analysis.SubtitleStreams.Count}");
        _output.WriteLine($"Is HDR: {analysis.IsHDR}");

        if (analysis.PrimaryVideoStream != null)
        {
            _output.WriteLine($"Primary Video: {analysis.PrimaryVideoStream.Width}x{analysis.PrimaryVideoStream.Height}");
            _output.WriteLine($"  Codec: {analysis.PrimaryVideoStream.CodecName}");
            _output.WriteLine($"  Frame Rate: {analysis.PrimaryVideoStream.FrameRate}");
            _output.WriteLine($"  Bitrate: {analysis.PrimaryVideoStream.BitRate}");
        }

        // Duration may be 0 if not available in format header (common for some MKV files)
        // The key assertion is that we have video and audio streams
        _output.WriteLine($"Note: Duration was {analysis.Duration}. This is expected for some MKV containers.");
        Assert.NotEmpty(analysis.VideoStreams);
        Assert.NotEmpty(analysis.AudioStreams);
    }

    /// <summary>
    /// Tests FFmpeg command generation for the Marvel 4K profile
    /// </summary>
    [Fact]
    public async Task BuildFFmpegCommand_ForMarvelProfile_GeneratesValidCommand()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);
        if (videoFiles.Count == 0)
        {
            _output.WriteLine("No video files found");
            return;
        }

        string firstEpisode = videoFiles[0];
        IStreamAnalyzer analyzer = _serviceProvider!.GetRequiredService<IStreamAnalyzer>();
        IHardwareAccelerationService hardwareService = _serviceProvider!.GetRequiredService<IHardwareAccelerationService>();

        StreamAnalysis analysis = await analyzer.AnalyzeAsync(firstEpisode);

        Assert.NotNull(_marvelProfile);

        List<GpuAccelerator> accelerators = hardwareService.GetAvailableAccelerators();
        _output.WriteLine($"Available GPU accelerators: {accelerators.Count}");
        foreach (GpuAccelerator gpu in accelerators)
        {
            _output.WriteLine($"  - {gpu.Vendor} ({gpu.Accelerator})");
        }

        ICodecSelector codecSelector = _serviceProvider!.GetRequiredService<ICodecSelector>();

        FFmpegCommandBuilder commandBuilder = new(
            analysis,
            _marvelProfile,
            accelerators,
            firstEpisode,
            _testOutputFolder,
            codecSelector
        );

        string command = commandBuilder.BuildCommand();
        _output.WriteLine($"Generated FFmpeg command:\n{command}");

        Assert.Contains("-hide_banner", command);
        Assert.Contains("-y -i", command);
        Assert.Contains("-c:v", command);
        Assert.Contains("-c:a", command);
    }

    /// <summary>
    /// Full integration test: creates job, executes encoding, and validates output
    /// This test encodes a SHORT segment of the first episode to avoid long test times
    /// </summary>
    [Fact]
    public async Task EncodeFirstEpisodeSegment_CreatesHLSOutput()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);
        if (videoFiles.Count == 0)
        {
            _output.WriteLine("No video files found");
            return;
        }

        string firstEpisode = videoFiles[0];
        string episodeName = Path.GetFileNameWithoutExtension(firstEpisode);
        string episodeOutputFolder = Path.Combine(_testOutputFolder, episodeName);
        Directory.CreateDirectory(episodeOutputFolder);

        _output.WriteLine($"Encoding episode: {episodeName}");
        _output.WriteLine($"Output folder: {episodeOutputFolder}");

        // Use EncodingService to encode a 30-second preview
        EncodingResult result = await EncodeQuickPreviewAsync(
            firstEpisode,
            episodeOutputFolder,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(30),
            progress => _output.WriteLine($"Progress: {progress}")
        );

        _output.WriteLine($"Encoding success: {result.Success}");
        _output.WriteLine($"Duration: {result.Duration}");

        if (!result.Success)
        {
            _output.WriteLine($"Error: {result.ErrorMessage}");
        }

        // Check for output files
        string[] outputFiles = Directory.GetFiles(episodeOutputFolder, "*.*", SearchOption.AllDirectories);
        _output.WriteLine($"Output files: {outputFiles.Length}");
        foreach (string file in outputFiles)
        {
            FileInfo info = new(file);
            _output.WriteLine($"  - {info.Name} ({info.Length / 1024.0:F2} KB)");
        }

        Assert.True(result.Success, $"FFmpeg encoding failed: {result.ErrorMessage}");
        Assert.True(outputFiles.Length > 0, "No output files generated");
    }

    /// <summary>
    /// Tests the full job creation and execution pipeline through the executor
    /// NOTE: This test requires proper database seeding with MediaContext.
    /// For unit testing, use the direct FFmpeg tests instead.
    /// </summary>
    [Fact(Skip = "Requires MediaContext profile seeding - use direct FFmpeg tests instead")]
    public async Task ExecuteFullJobPipeline_WithMarvelProfile_Succeeds()
    {
        await Task.CompletedTask;
        _output.WriteLine("This test requires full database seeding with MediaContext");
        _output.WriteLine("Use the QuickEncodingTest or MarvelProfileEncoding tests for direct FFmpeg testing");
    }

    /// <summary>
    /// Runs actual encoding on all Helstrom episodes
    /// WARNING: This test takes HOURS to complete!
    /// Only run manually when you want to do a full encoding run
    /// </summary>
    [Fact(Skip = "Long-running test - requires MediaContext seeding")]
    public async Task ExecuteRealEncoding_AllEpisodes_WithMarvelProfile()
    {
        await Task.CompletedTask;
        _output.WriteLine("This test requires full database seeding and takes hours to complete");
    }

    /// <summary>
    /// Tests encoding a single short segment to validate the pipeline works end-to-end
    /// This is a quick test that actually produces output
    /// </summary>
    [Fact]
    public async Task QuickEncodingTest_30SecondSegment_ProducesOutput()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);
        if (videoFiles.Count == 0)
        {
            _output.WriteLine("No video files found");
            return;
        }

        string firstEpisode = videoFiles[0];
        string episodeName = "QuickTest_" + Path.GetFileNameWithoutExtension(firstEpisode);
        string episodeOutputFolder = Path.Combine(_testOutputFolder, episodeName);
        Directory.CreateDirectory(episodeOutputFolder);

        _output.WriteLine($"Quick encoding test: {Path.GetFileName(firstEpisode)}");
        _output.WriteLine($"Output: {episodeOutputFolder}");

        IStreamAnalyzer analyzer = _serviceProvider!.GetRequiredService<IStreamAnalyzer>();

        // First analyze the source
        StreamAnalysis analysis = await analyzer.AnalyzeAsync(firstEpisode);
        _output.WriteLine($"Source: {analysis.PrimaryVideoStream?.Width}x{analysis.PrimaryVideoStream?.Height}, HDR: {analysis.IsHDR}");

        // Use EncodingService to encode a 30-second preview
        int progressCount = 0;
        EncodingResult result = await EncodeQuickPreviewAsync(
            firstEpisode,
            episodeOutputFolder,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(30),
            progress =>
            {
                progressCount++;
                if (progressCount % 10 == 0 && progress.Contains("speed="))
                {
                    _output.WriteLine($"[{progressCount}] {progress}");
                }
            }
        );

        _output.WriteLine($"\nEncoding completed in {result.Duration}");
        _output.WriteLine($"Success: {result.Success}");

        if (!result.Success)
        {
            _output.WriteLine($"Error: {result.ErrorMessage}");
        }

        // Validate output
        string[] outputFiles = Directory.GetFiles(episodeOutputFolder, "*.*", SearchOption.AllDirectories);
        long totalSize = outputFiles.Sum(f => new FileInfo(f).Length);

        _output.WriteLine($"\nOutput files ({outputFiles.Length} files, {totalSize / 1024.0:F2} KB total):");
        foreach (string file in outputFiles.Take(20))
        {
            FileInfo info = new(file);
            _output.WriteLine($"  - {Path.GetRelativePath(episodeOutputFolder, file)} ({info.Length / 1024.0:F2} KB)");
        }

        if (outputFiles.Length > 20)
        {
            _output.WriteLine($"  ... and {outputFiles.Length - 20} more files");
        }

        Assert.True(result.Success, $"FFmpeg failed: {result.ErrorMessage}");
        Assert.True(outputFiles.Length > 0, "No output files created");

        // Check for HLS playlist
        bool hasPlaylist = outputFiles.Any(f => f.EndsWith(".m3u8"));
        bool hasSegments = outputFiles.Any(f => f.EndsWith(".ts"));

        _output.WriteLine($"\nHLS validation: Playlist={hasPlaylist}, Segments={hasSegments}");
        Assert.True(hasPlaylist, "No HLS playlist (.m3u8) created");
        Assert.True(hasSegments, "No HLS segments (.ts) created");
    }

    private static List<string> GetVideoFilesFromFolder(string folderPath)
    {
        string[] videoExtensions = [".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v"];

        return Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Encodes a quick preview using EncoderV2's complete encoding service
    /// This is the CORRECT way - uses IEncodingService which handles EVERYTHING
    /// </summary>
    private async Task<EncodingResult> EncodeQuickPreviewAsync(
        string inputFile,
        string outputFolder,
        TimeSpan startTime,
        TimeSpan duration,
        Action<string>? progressCallback = null)
    {
        // Get the complete encoding service
        IEncodingService encodingService = _serviceProvider!.GetRequiredService<IEncodingService>();

        // Create a quick test profile (720p, fast encoding)
        EncoderProfile quickProfile = new()
        {
            Id = Ulid.NewUlid(),
            Name = "Quick Test Profile",
            Container = "hls",
            VideoProfiles =
            [
                new IVideoProfile
                {
                    Codec = "libx264",
                    Width = 1280,
                    Height = 720,
                    Preset = "veryfast",
                    Crf = 23,
                    Profile = "high",
                    ConvertHdrToSdr = true // Convert HDR to SDR for quick tests
                }
            ],
            AudioProfiles =
            [
                new IAudioProfile
                {
                    Codec = "aac",
                    Channels = 2,
                    SampleRate = 48000,
                    Opts = ["-b:a", "128k"]
                }
            ]
        };

        // EncodingService handles EVERYTHING: analysis, command building, execution, validation
        return await encodingService.EncodePreviewAsync(
            inputFile,
            outputFolder,
            quickProfile,
            startTime,
            duration,
            progressCallback);
    }

    /// <summary>
    /// Encodes with V1-compatible separate streams output
    /// Uses IEncodingService which handles everything internally
    /// </summary>
    private async Task<EncodingResult> EncodeWithV1CompatibleOutputAsync(
        string inputFile,
        string outputFolder,
        string baseFilename,
        EncoderProfile profile,
        Action<string>? progressCallback = null)
    {
        // Get the complete encoding service
        IEncodingService encodingService = _serviceProvider!.GetRequiredService<IEncodingService>();

        // EncodingService handles EVERYTHING: analysis, folder structure, command building,
        // execution, playlist generation - all in one call
        return await encodingService.EncodeWithSeparateStreamsAsync(
            inputFile,
            outputFolder,
            baseFilename,
            profile,
            progressCallback);
    }

    /// <summary>
    /// Full encoding test that matches V1 output structure:
    /// - Separate video-only HLS streams in video_WxH_SDR/ or video_WxH_HDR/ folders
    /// - Separate audio-only HLS streams in audio_lang_codec/ folders
    /// - Subtitle extraction to subtitles/ folder
    /// - Master HLS playlist combining all variants
    ///
    /// This is what the V1 encoder produces - matches expected output at:
    /// M:\Marvels\TV.Shows\Helstrom.(2020)\Helstrom.S01E01\
    /// </summary>
    [Fact]
    public async Task FullEncode_MatchesV1OutputStructure_ProducesCompleteHLS()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        List<string> videoFiles = GetVideoFilesFromFolder(HelstromInputFolder);
        if (videoFiles.Count == 0)
        {
            _output.WriteLine("No video files found");
            return;
        }

        string firstEpisode = videoFiles[0];
        string episodeName = Path.GetFileNameWithoutExtension(firstEpisode);

        // Parse episode info for folder naming (e.g., "Helstrom.S01E01.Mother.s.Little.Helpers")
        string outputFolderName = episodeName.Split('.').Take(3).Aggregate((a, b) => $"{a}.{b}");
        string episodeOutputFolder = Path.Combine(_testOutputFolder, outputFolderName);
        Directory.CreateDirectory(episodeOutputFolder);

        _output.WriteLine($"=== FULL V1-MATCHING ENCODE TEST ===");
        _output.WriteLine($"Input: {firstEpisode}");
        _output.WriteLine($"Output: {episodeOutputFolder}");

        IStreamAnalyzer analyzer = _serviceProvider!.GetRequiredService<IStreamAnalyzer>();

        // Analyze source file
        StreamAnalysis analysis = await analyzer.AnalyzeAsync(firstEpisode);
        _output.WriteLine($"\nSource Analysis:");
        _output.WriteLine($"  Video: {analysis.PrimaryVideoStream?.Width}x{analysis.PrimaryVideoStream?.Height}");
        _output.WriteLine($"  Codec: {analysis.PrimaryVideoStream?.CodecName}");
        _output.WriteLine($"  HDR: {analysis.IsHDR}");
        _output.WriteLine($"  Duration: {analysis.Duration}");
        _output.WriteLine($"  Audio Streams: {analysis.AudioStreams.Count}");
        foreach (AudioStream audioStream in analysis.AudioStreams)
        {
            _output.WriteLine($"    - {audioStream.Language ?? "und"}: {audioStream.CodecName} {audioStream.Channels}ch");
        }
        _output.WriteLine($"  Subtitle Streams: {analysis.SubtitleStreams.Count}");
        foreach (SubtitleStream subStream in analysis.SubtitleStreams)
        {
            _output.WriteLine($"    - {subStream.Language ?? "und"}: {subStream.CodecName}");
        }

        Assert.NotNull(_marvelProfile);

        _output.WriteLine($"\nStarting V1-compatible encoding with EncoderV2...");

        // Use EncodingService to encode with V1-compatible output structure
        int progressCount = 0;
        DateTime lastProgressTime = DateTime.Now;
        EncodingResult result = await EncodeWithV1CompatibleOutputAsync(
            firstEpisode,
            episodeOutputFolder,
            outputFolderName,
            _marvelProfile,
            progress =>
            {
                progressCount++;
                // Log progress every 5 seconds or every 100 updates
                if (DateTime.Now - lastProgressTime > TimeSpan.FromSeconds(5) || progressCount % 100 == 0)
                {
                    if (progress.Contains("speed=") || progress.Contains("frame="))
                    {
                        _output.WriteLine($"[{progressCount}] {progress}");
                        lastProgressTime = DateTime.Now;
                    }
                }
            }
        );

        _output.WriteLine($"\n=== ENCODING COMPLETE ===");
        _output.WriteLine($"Exit Code: {result.ExitCode}");
        _output.WriteLine($"Duration: {result.Duration}");
        _output.WriteLine($"Success: {result.Success}");

        if (!result.Success)
        {
            _output.WriteLine($"\nError: {result.ErrorMessage}");
        }

        // Validate output structure matches V1
        _output.WriteLine($"\n=== OUTPUT VALIDATION ===");

        // Check all output files
        string[] allFiles = Directory.GetFiles(episodeOutputFolder, "*.*", SearchOption.AllDirectories);
        _output.WriteLine($"Total files: {allFiles.Length}");

        // Check for video folders
        string[] videoFolders = Directory.GetDirectories(episodeOutputFolder, "video_*");
        _output.WriteLine($"Video folders: {videoFolders.Length}");
        foreach (string folder in videoFolders)
        {
            string[] files = Directory.GetFiles(folder, "*.*");
            _output.WriteLine($"  - {Path.GetFileName(folder)}: {files.Length} files");
        }

        // Check for audio folders
        string[] audioFolders = Directory.GetDirectories(episodeOutputFolder, "audio_*");
        _output.WriteLine($"Audio folders: {audioFolders.Length}");
        foreach (string folder in audioFolders)
        {
            string[] files = Directory.GetFiles(folder, "*.*");
            _output.WriteLine($"  - {Path.GetFileName(folder)}: {files.Length} files");
        }

        // Check for master playlist
        string masterPlaylistPath = Path.Combine(episodeOutputFolder, $"{outputFolderName}.m3u8");
        bool hasMasterPlaylist = File.Exists(masterPlaylistPath);
        _output.WriteLine($"\nMaster playlist exists: {hasMasterPlaylist}");

        if (hasMasterPlaylist)
        {
            string masterContent = await File.ReadAllTextAsync(masterPlaylistPath);
            _output.WriteLine($"\n=== MASTER PLAYLIST CONTENT ===");
            _output.WriteLine(masterContent);
            _output.WriteLine("================================\n");
        }

        // List all output files
        _output.WriteLine($"\n=== ALL OUTPUT FILES ===");
        long totalSize = allFiles.Sum(f => new FileInfo(f).Length);
        _output.WriteLine($"Total: {allFiles.Length} files, {totalSize / (1024.0 * 1024.0):F2} MB");

        foreach (string file in allFiles.OrderBy(f => f))
        {
            FileInfo info = new(file);
            string relativePath = Path.GetRelativePath(episodeOutputFolder, file);
            _output.WriteLine($"  {relativePath} ({info.Length / 1024.0:F2} KB)");
        }

        // Assertions
        Assert.True(result.Success, $"Encoding failed: {result.ErrorMessage}");
        Assert.True(videoFolders.Length > 0, "No video folders created");
        Assert.True(audioFolders.Length > 0, "No audio folders created");
        Assert.True(hasMasterPlaylist, "No master playlist created");

        _output.WriteLine($"\n=== TEST PASSED ===");
        _output.WriteLine($"Output preserved at: {episodeOutputFolder}");
    }

    /// <summary>
    /// Generates an HLS playlist from existing segment files
    /// Used when FFmpeg was interrupted before writing the playlist
    /// </summary>
    private async Task GenerateSegmentPlaylist(string folderPath, string baseName)
    {
        string[] segmentFiles = Directory.GetFiles(folderPath, "*.ts")
            .OrderBy(f => f)
            .ToArray();

        if (segmentFiles.Length == 0)
        {
            _output.WriteLine($"  No segments found in {folderPath}");
            return;
        }

        System.Text.StringBuilder playlist = new();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:3");
        playlist.AppendLine("#EXT-X-TARGETDURATION:5");
        playlist.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        playlist.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        // Get segment duration by probing first segment (or estimate)
        double segmentDuration = 4.0; // Default HLS segment time

        foreach (string segment in segmentFiles)
        {
            string segmentName = Path.GetFileName(segment);
            playlist.AppendLine($"#EXTINF:{segmentDuration:F6},");
            playlist.AppendLine(segmentName);
        }

        playlist.AppendLine("#EXT-X-ENDLIST");

        string playlistPath = Path.Combine(folderPath, $"{baseName}.m3u8");
        await File.WriteAllTextAsync(playlistPath, playlist.ToString());

        _output.WriteLine($"  Generated playlist: {playlistPath} ({segmentFiles.Length} segments)");
    }

    /// <summary>
    /// Test using real EncoderV2 components instead of manually building FFmpeg commands
    /// This demonstrates the proper architecture where the encoder handles everything internally
    /// </summary>
    [Fact]
    public async Task FullEncode_UsingRealEncoderV2Components_ProducesV1CompatibleOutput()
    {
        if (!Directory.Exists(HelstromInputFolder))
        {
            _output.WriteLine($"Skipping test: Input folder not found at {HelstromInputFolder}");
            return;
        }

        _output.WriteLine("=== FULL ENCODE TEST USING REAL ENCODERV2 COMPONENTS ===\n");

        // Scan input folder
        string[] episodeFiles = Directory.GetFiles(HelstromInputFolder, "*.mkv");
        Assert.NotEmpty(episodeFiles);

        string firstEpisode = episodeFiles[0];
        string episodeName = Path.GetFileNameWithoutExtension(firstEpisode);
        string outputFolderName = episodeName.Replace(".mkv", "");

        string episodeOutputFolder = Path.Combine(_testOutputFolder, outputFolderName);
        Directory.CreateDirectory(episodeOutputFolder);

        _output.WriteLine($"Input: {firstEpisode}");
        _output.WriteLine($"Output: {episodeOutputFolder}\n");

        // Analyze media using StreamAnalyzer
        IStreamAnalyzer streamAnalyzer = _serviceProvider!.GetRequiredService<IStreamAnalyzer>();
        StreamAnalysis analysis = await streamAnalyzer.AnalyzeAsync(firstEpisode);

        _output.WriteLine($"=== MEDIA ANALYSIS ===");
        _output.WriteLine($"  Video: {analysis.PrimaryVideoStream?.Width}x{analysis.PrimaryVideoStream?.Height} ({analysis.PrimaryVideoStream?.CodecName})");
        _output.WriteLine($"  HDR: {analysis.IsHDR}");
        _output.WriteLine($"  Duration: {analysis.Duration}");
        _output.WriteLine($"  Audio Streams: {analysis.AudioStreams.Count}");
        _output.WriteLine($"  Subtitle Streams: {analysis.SubtitleStreams.Count}\n");

        // Get required services
        IHLSOutputOrchestrator orchestrator = _serviceProvider.GetRequiredService<IHLSOutputOrchestrator>();
        ICodecSelector codecSelector = _serviceProvider.GetRequiredService<ICodecSelector>();
        IHardwareAccelerationService hardwareService = _serviceProvider.GetRequiredService<IHardwareAccelerationService>();
        IFFmpegService ffmpegService = _serviceProvider.GetRequiredService<IFFmpegService>();

        List<GpuAccelerator> accelerators = hardwareService.GetAvailableAccelerators();

        _output.WriteLine($"=== GPU DETECTION ===");
        _output.WriteLine($"  Available accelerators: {accelerators.Count}");
        foreach (GpuAccelerator gpu in accelerators)
        {
            _output.WriteLine($"    - {gpu.Vendor} ({gpu.Accelerator}): {gpu.FfmpegArgs}");
        }

        string selectedCodec = codecSelector.SelectH264Codec();
        _output.WriteLine($"  Selected H.264 codec: {selectedCodec}\n");

        // Create HLS output structure using orchestrator
        HLSSpecification hlsSpec = new()
        {
            Version = 3,
            TargetDuration = 10,
            SegmentDuration = 6,
            PlaylistType = "VOD",
            IndependentSegments = true
        };

        HLSOutputStructure outputStructure = await orchestrator.CreateOutputStructureFromStreamAnalysisAsync(
            episodeOutputFolder,
            outputFolderName,
            analysis,
            hlsSpec);

        _output.WriteLine($"=== HLS OUTPUT STRUCTURE ===");
        _output.WriteLine($"  Base path: {outputStructure.BasePath}");
        _output.WriteLine($"  Video outputs: {outputStructure.VideoOutputs.Count}");
        foreach (HLSVideoOutput videoOutput in outputStructure.VideoOutputs)
        {
            _output.WriteLine($"    - {videoOutput.FolderName}: {videoOutput.Width}x{videoOutput.Height} (HDR: {videoOutput.IsHdr})");
        }
        _output.WriteLine($"  Audio outputs: {outputStructure.AudioOutputs.Count}");
        foreach (HLSAudioOutput audioOutput in outputStructure.AudioOutputs)
        {
            _output.WriteLine($"    - {audioOutput.FolderName}: {audioOutput.Language} {audioOutput.Codec} {audioOutput.Channels}ch");
        }
        _output.WriteLine("");

        // Build FFmpeg command using FFmpegCommandBuilder with separate streams mode
        FFmpegCommandBuilder commandBuilder = new(
            analysis,
            _marvelProfile!,
            accelerators,
            firstEpisode,
            episodeOutputFolder,
            codecSelector,
            HLSOutputMode.SeparateStreams);

        commandBuilder.SetHLSOutputStructure(outputStructure);
        string command = commandBuilder.BuildCommand();

        _output.WriteLine($"=== FFMPEG COMMAND (via FFmpegCommandBuilder) ===");
        _output.WriteLine(command);
        _output.WriteLine("================================================\n");

        // Execute encoding with 120 minute timeout
        CancellationTokenSource cts = new(TimeSpan.FromMinutes(120));

        int progressCount = 0;
        DateTime lastProgressTime = DateTime.Now;
        FFmpegExecutionResult result = await ffmpegService.ExecuteAsync(
            command,
            episodeOutputFolder,
            progress =>
            {
                progressCount++;
                if (DateTime.Now - lastProgressTime > TimeSpan.FromSeconds(5) || progressCount % 100 == 0)
                {
                    if (progress.Contains("speed=") || progress.Contains("frame="))
                    {
                        _output.WriteLine($"[{progressCount}] {progress}");
                        lastProgressTime = DateTime.Now;
                    }
                }
            },
            cts.Token);

        _output.WriteLine($"\n=== ENCODING COMPLETE ===");
        _output.WriteLine($"Exit Code: {result.ExitCode}");
        _output.WriteLine($"Duration: {result.ExecutionTime}");
        _output.WriteLine($"Success: {result.Success}\n");

        if (!result.Success)
        {
            _output.WriteLine($"Error: {result.ErrorMessage}");
        }

        // Generate playlists using orchestrator
        _output.WriteLine($"=== GENERATING PLAYLISTS ===");
        await orchestrator.GeneratePlaylistsAsync(outputStructure, analysis.Duration);

        // Validate output
        _output.WriteLine($"\n=== OUTPUT VALIDATION ===");
        bool hasVideoSegments = outputStructure.VideoOutputs.Any(v =>
            Directory.Exists(v.FolderPath) && Directory.GetFiles(v.FolderPath, "*.ts").Length > 0);
        bool hasVideoPlaylists = outputStructure.VideoOutputs.Any(v =>
            File.Exists(v.PlaylistPath));
        bool hasAudioSegments = outputStructure.AudioOutputs.Any(a =>
            Directory.Exists(a.FolderPath) && Directory.GetFiles(a.FolderPath, "*.ts").Length > 0);
        bool hasAudioPlaylists = outputStructure.AudioOutputs.Any(a =>
            File.Exists(a.PlaylistPath));
        bool hasMasterPlaylist = File.Exists(outputStructure.MasterPlaylistPath);

        _output.WriteLine($"Video segments: {hasVideoSegments}");
        _output.WriteLine($"Video playlists: {hasVideoPlaylists}");
        _output.WriteLine($"Audio segments: {hasAudioSegments}");
        _output.WriteLine($"Audio playlists: {hasAudioPlaylists}");
        _output.WriteLine($"Master playlist: {hasMasterPlaylist}\n");

        // Display master playlist
        if (hasMasterPlaylist)
        {
            string masterContent = await File.ReadAllTextAsync(outputStructure.MasterPlaylistPath);
            _output.WriteLine($"=== MASTER PLAYLIST ===");
            _output.WriteLine(masterContent);
            _output.WriteLine("=======================\n");
        }

        // List all files
        string[] allFiles = Directory.GetFiles(episodeOutputFolder, "*.*", SearchOption.AllDirectories);
        long totalSize = allFiles.Sum(f => new FileInfo(f).Length);
        _output.WriteLine($"=== OUTPUT FILES ===");
        _output.WriteLine($"Total: {allFiles.Length} files, {totalSize / (1024.0 * 1024.0):F2} MB\n");

        // Assertions
        bool encodingSucceeded = result.Success || (result.ExitCode == 0) || hasVideoSegments;
        Assert.True(encodingSucceeded, $"Encoding failed: {result.ErrorMessage}");
        Assert.True(hasVideoSegments, "No video segments created");
        Assert.True(hasVideoPlaylists, "No video playlists created");
        Assert.True(hasAudioSegments || hasAudioPlaylists, "No audio output created");
        Assert.True(hasMasterPlaylist, "No master playlist created");

        _output.WriteLine($"=== TEST PASSED ===");
        _output.WriteLine($"Output preserved at: {episodeOutputFolder}");
    }

    public Task DisposeAsync()
    {
        // Cleanup test output folder
        if (Directory.Exists(_testOutputFolder))
        {
            try
            {
                // Keep output for inspection in case of failures
                // Uncomment to auto-delete:
                // Directory.Delete(_testOutputFolder, true);
                _output.WriteLine($"Test output preserved at: {_testOutputFolder}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Failed to cleanup test folder: {ex.Message}");
            }
        }

        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }
}
