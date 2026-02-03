using FFMpegCore;
using NoMercy.Encoder;

namespace NoMercy.EncoderV2.Specifications.HLS;

/// <summary>
/// Orchestrates creation of V1-compatible HLS output structure with separate video/audio folders
/// </summary>
public interface IHLSOutputOrchestrator
{
    /// <summary>
    /// Creates folder structure for separate video and audio streams from MediaAnalysis
    /// </summary>
    Task<HLSOutputStructure> CreateOutputStructureAsync(
        string basePath,
        string baseFilename,
        MediaAnalysis analysis,
        HLSSpecification spec);

    /// <summary>
    /// Creates folder structure for separate video and audio streams from StreamAnalysis
    /// </summary>
    Task<HLSOutputStructure> CreateOutputStructureFromStreamAnalysisAsync(
        string basePath,
        string baseFilename,
        Streams.StreamAnalysis analysis,
        HLSSpecification spec);

    /// <summary>
    /// Generates all playlists (master + individual media playlists) after encoding
    /// </summary>
    Task GeneratePlaylistsAsync(HLSOutputStructure structure, TimeSpan totalDuration);

    /// <summary>
    /// Generates folder name for video stream
    /// </summary>
    string GetVideoFolderName(int width, int height, bool isHdr);

    /// <summary>
    /// Generates folder name for audio stream
    /// </summary>
    string GetAudioFolderName(string language, string codec);
}

public class HLSOutputOrchestrator : IHLSOutputOrchestrator
{
    private readonly IHLSPlaylistGenerator _playlistGenerator;

    public HLSOutputOrchestrator(IHLSPlaylistGenerator playlistGenerator)
    {
        _playlistGenerator = playlistGenerator;
    }

    public async Task<HLSOutputStructure> CreateOutputStructureAsync(
        string basePath,
        string baseFilename,
        MediaAnalysis analysis,
        HLSSpecification spec)
    {
        HLSOutputStructure structure = new()
        {
            BasePath = basePath,
            BaseFilename = baseFilename,
            MasterPlaylistPath = Path.Combine(basePath, $"{baseFilename}.m3u8")
        };

        // Create base directory
        Directory.CreateDirectory(basePath);

        // Create video output folders
        foreach (VideoStream videoStream in analysis.VideoStreams)
        {
            int width = videoStream.Width;
            int height = videoStream.Height;
            bool isHdr = IsHdrStream(videoStream);

            string folderName = GetVideoFolderName(width, height, isHdr);
            string folderPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(folderPath);

            HLSVideoOutput videoOutput = new()
            {
                FolderName = folderName,
                FolderPath = folderPath,
                PlaylistFilename = $"{folderName}.m3u8",
                PlaylistPath = Path.Combine(folderPath, $"{folderName}.m3u8"),
                SegmentPattern = $"{folderName}_%05d.ts",
                Width = width,
                Height = height,
                IsHdr = isHdr,
                StreamIndex = videoStream.Index
            };

            structure.VideoOutputs.Add(videoOutput);
        }

        // Create audio output folders
        foreach (AudioStream audioStream in analysis.AudioStreams)
        {
            string language = audioStream.Language ?? "und";
            string codec = DetermineAudioCodec(audioStream);

            string folderName = GetAudioFolderName(language, codec);
            string folderPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(folderPath);

            HLSAudioOutput audioOutput = new()
            {
                FolderName = folderName,
                FolderPath = folderPath,
                PlaylistFilename = $"{folderName}.m3u8",
                PlaylistPath = Path.Combine(folderPath, $"{folderName}.m3u8"),
                SegmentPattern = $"{folderName}_%05d.ts",
                Language = language,
                Codec = codec,
                StreamIndex = audioStream.Index,
                Channels = (int)audioStream.Channels
            };

            structure.AudioOutputs.Add(audioOutput);
        }

        return await Task.FromResult(structure);
    }

    public async Task<HLSOutputStructure> CreateOutputStructureFromStreamAnalysisAsync(
        string basePath,
        string baseFilename,
        Streams.StreamAnalysis analysis,
        HLSSpecification spec)
    {
        HLSOutputStructure structure = new()
        {
            BasePath = basePath,
            BaseFilename = baseFilename,
            MasterPlaylistPath = Path.Combine(basePath, $"{baseFilename}.m3u8")
        };

        // Create base directory
        Directory.CreateDirectory(basePath);

        // Create video output folders from StreamAnalysis
        foreach (Encoder.Dto.VideoStream videoStream in analysis.VideoStreams)
        {
            int width = videoStream.Width;
            int height = videoStream.Height;
            bool isHdr = analysis.IsHDR; // StreamAnalysis has this at the top level

            string folderName = GetVideoFolderName(width, height, isHdr);
            string folderPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(folderPath);

            HLSVideoOutput videoOutput = new()
            {
                FolderName = folderName,
                FolderPath = folderPath,
                PlaylistFilename = $"{folderName}.m3u8",
                PlaylistPath = Path.Combine(folderPath, $"{folderName}.m3u8"),
                SegmentPattern = $"{folderName}_%05d.ts",
                Width = width,
                Height = height,
                IsHdr = isHdr,
                StreamIndex = videoStream.Index
            };

            structure.VideoOutputs.Add(videoOutput);
        }

        // Create audio output folders from StreamAnalysis
        foreach (Encoder.Dto.AudioStream audioStream in analysis.AudioStreams)
        {
            string language = audioStream.Language ?? "und";
            string codec = DetermineAudioCodecFromCodecName(audioStream.CodecName);

            string folderName = GetAudioFolderName(language, codec);
            string folderPath = Path.Combine(basePath, folderName);
            Directory.CreateDirectory(folderPath);

            HLSAudioOutput audioOutput = new()
            {
                FolderName = folderName,
                FolderPath = folderPath,
                PlaylistFilename = $"{folderName}.m3u8",
                PlaylistPath = Path.Combine(folderPath, $"{folderName}.m3u8"),
                SegmentPattern = $"{folderName}_%05d.ts",
                Language = language,
                Codec = codec,
                StreamIndex = audioStream.Index,
                Channels = (int)(audioStream.Channels ?? 2)
            };

            structure.AudioOutputs.Add(audioOutput);
        }

        return await Task.FromResult(structure);
    }

    public async Task GeneratePlaylistsAsync(HLSOutputStructure structure, TimeSpan totalDuration)
    {
        // Generate individual media playlists for each video stream
        foreach (HLSVideoOutput videoOutput in structure.VideoOutputs)
        {
            List<string> segments = Directory.GetFiles(videoOutput.FolderPath, "*.ts")
                .OrderBy(f => f)
                .ToList();

            if (segments.Count > 0)
            {
                HLSSpecification spec = new()
                {
                    Version = 3,
                    TargetDuration = 10,
                    SegmentDuration = 6,
                    PlaylistType = "VOD",
                    IndependentSegments = true
                };

                await _playlistGenerator.WriteMediaPlaylistAsync(
                    videoOutput.PlaylistPath,
                    spec,
                    segments,
                    totalDuration);
            }
        }

        // Generate individual media playlists for each audio stream
        foreach (HLSAudioOutput audioOutput in structure.AudioOutputs)
        {
            List<string> segments = Directory.GetFiles(audioOutput.FolderPath, "*.ts")
                .OrderBy(f => f)
                .ToList();

            if (segments.Count > 0)
            {
                HLSSpecification spec = new()
                {
                    Version = 3,
                    TargetDuration = 10,
                    SegmentDuration = 6,
                    PlaylistType = "VOD",
                    IndependentSegments = true
                };

                await _playlistGenerator.WriteMediaPlaylistAsync(
                    audioOutput.PlaylistPath,
                    spec,
                    segments,
                    totalDuration);
            }
        }

        // Generate master playlist
        List<HLSVariantStream> variants = [];
        List<HLSMediaGroup> mediaGroups = [];

        // Add audio media groups
        foreach (HLSAudioOutput audioOutput in structure.AudioOutputs)
        {
            HLSMediaGroup mediaGroup = new()
            {
                Type = "AUDIO",
                GroupId = "audio",
                Name = $"{audioOutput.Language.ToUpper()} - {audioOutput.Codec.ToUpper()}",
                Language = audioOutput.Language,
                IsDefault = audioOutput == structure.AudioOutputs.First(),
                Autoselect = true,
                Uri = $"{audioOutput.FolderName}/{audioOutput.PlaylistFilename}"
            };
            mediaGroups.Add(mediaGroup);
        }

        // Add video variant streams
        foreach (HLSVideoOutput videoOutput in structure.VideoOutputs)
        {
            // Estimate bandwidth (rough approximation)
            int bandwidth = EstimateBandwidth(videoOutput.Width, videoOutput.Height, videoOutput.IsHdr);

            HLSVariantStream variant = new()
            {
                Bandwidth = bandwidth,
                Resolution = $"{videoOutput.Width}x{videoOutput.Height}",
                Codecs = "avc1.640028,mp4a.40.2",
                PlaylistUri = $"{videoOutput.FolderName}/{videoOutput.PlaylistFilename}",
                AudioGroup = "audio"
            };
            variants.Add(variant);
        }

        await _playlistGenerator.WriteMasterPlaylistAsync(
            structure.MasterPlaylistPath,
            variants,
            mediaGroups);
    }

    public string GetVideoFolderName(int width, int height, bool isHdr)
    {
        string colorSpace = isHdr ? "HDR" : "SDR";
        return $"video_{width}x{height}_{colorSpace}";
    }

    public string GetAudioFolderName(string language, string codec)
    {
        return $"audio_{language}_{codec}";
    }

    private bool IsHdrStream(VideoStream stream)
    {
        // Check for HDR indicators
        return stream.ColorTransfer?.Contains("smpte2084", StringComparison.OrdinalIgnoreCase) == true ||
               stream.ColorTransfer?.Contains("arib-std-b67", StringComparison.OrdinalIgnoreCase) == true ||
               stream.ColorSpace?.Contains("bt2020", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string DetermineAudioCodec(AudioStream stream)
    {
        // Map FFmpeg codec names to output codec names
        return stream.CodecName?.ToLower() switch
        {
            "aac" => "aac",
            "eac3" => "eac3",
            "ac3" => "ac3",
            "opus" => "opus",
            _ => "aac" // Default to AAC
        };
    }

    private string DetermineAudioCodecFromCodecName(string? codecName)
    {
        // Map FFmpeg codec names to output codec names
        return codecName?.ToLower() switch
        {
            "aac" => "aac",
            "eac3" => "eac3",
            "ac3" => "ac3",
            "opus" => "opus",
            _ => "aac" // Default to AAC
        };
    }

    private int EstimateBandwidth(int width, int height, bool isHdr)
    {
        // Rough bandwidth estimation based on resolution
        int baseBandwidth = (width, height) switch
        {
            ( >= 3840, >= 2160) => 25_000_000, // 4K
            ( >= 2560, >= 1440) => 12_000_000, // 1440p
            ( >= 1920, >= 1080) => 8_000_000,  // 1080p
            ( >= 1280, >= 720) => 5_000_000,   // 720p
            _ => 3_000_000                      // SD
        };

        // HDR content typically has higher bitrate
        if (isHdr)
        {
            baseBandwidth = (int)(baseBandwidth * 1.3);
        }

        return baseBandwidth;
    }
}

/// <summary>
/// Represents the complete HLS output structure with separate video/audio folders
/// </summary>
public class HLSOutputStructure
{
    public string BasePath { get; set; } = string.Empty;
    public string BaseFilename { get; set; } = string.Empty;
    public string MasterPlaylistPath { get; set; } = string.Empty;
    public List<HLSVideoOutput> VideoOutputs { get; set; } = [];
    public List<HLSAudioOutput> AudioOutputs { get; set; } = [];
}

/// <summary>
/// Represents a video-only HLS output stream
/// </summary>
public class HLSVideoOutput
{
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string PlaylistFilename { get; set; } = string.Empty;
    public string PlaylistPath { get; set; } = string.Empty;
    public string SegmentPattern { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsHdr { get; set; }
    public int StreamIndex { get; set; }
}

/// <summary>
/// Represents an audio-only HLS output stream
/// </summary>
public class HLSAudioOutput
{
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string PlaylistFilename { get; set; } = string.Empty;
    public string PlaylistPath { get; set; } = string.Empty;
    public string SegmentPattern { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public int StreamIndex { get; set; }
    public int Channels { get; set; }
}
