using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using static NoMercy.Encoder.Core.IsoLanguageMapper;

namespace NoMercy.Encoder.Core;

public static class HlsPlaylistGenerator
{
    private const int MaxConcurrentProbes = 3;

    public static async Task Build(string basePath, string filename, List<string>? priorityLanguages = null)
    {
        priorityLanguages ??= ["eng", "jpn"];

        if (!Directory.Exists(basePath))
            return;

        string[] folders = Directory.GetDirectories(basePath)
            .Where(f => Path.GetFileName(f).StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase) ||
                        Path.GetFileName(f).StartsWith("video_", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();

        List<string> videoFileList = folders
            .Where(f => Path.GetFileName(f).StartsWith("video_", StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"))
            .ToList();

        List<string> audioFiles = folders
            .Where(f => Path.GetFileName(f).StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"))
            .ToList();

        // Order audio tracks by priority language, then language name, then folder size
        audioFiles = audioFiles
            .OrderBy(f =>
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(f) ?? string.Empty);
                string[] parts = folderName.Split('_');
                string language = parts.Length > 1 ? parts[1] : "und";
                int idx = priorityLanguages.IndexOf(language);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ThenBy(f =>
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(f) ?? string.Empty);
                string[] parts = folderName.Split('_');
                string language = parts.Length > 1 ? parts[1] : "und";
                return language ?? string.Empty;
            })
            .ThenBy(f => GetTotalSize(Path.GetDirectoryName(f) ?? string.Empty))
            .ToList();

        StringBuilder masterPlaylist = new();
        masterPlaylist.AppendLine("#EXTM3U");
        masterPlaylist.AppendLine("#EXT-X-VERSION:6");
        masterPlaylist.AppendLine();

        // Build audio groups
        Dictionary<string, List<string>> audioGroups = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < audioFiles.Count; index++)
        {
            string audioFile = audioFiles[index];
            string folderName = Path.GetFileName(Path.GetDirectoryName(audioFile) ?? string.Empty) ?? string.Empty;
            string[] parts = folderName.Split('_');
            string language = parts.Length > 1 ? parts[1] : "und";
            string codecName = parts.Length > 2 ? parts[2] : "aac";

            if (!audioGroups.TryGetValue(codecName, out List<string>? langs))
            {
                langs = [];
                audioGroups[codecName] = langs;
            }

            if (!langs.Contains(language, StringComparer.OrdinalIgnoreCase))
                langs.Add(language);

            string langDisplay = IsoToLanguage.TryGetValue(language, out string? langName) ? ToTitleCase(langName) : ToTitleCase(language);
            string uri = Path.Combine(folderName, Path.GetFileName(audioFile)).Replace("\\", "/");
            string defaultFlag = (index == 0) ? "YES" : "NO";

            masterPlaylist.AppendLine($"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_{codecName}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={defaultFlag},URI=\"{uri}\",NAME=\"{langDisplay} {codecName}\"");
        }

        masterPlaylist.AppendLine();

        // Check if any video folder has _SDR or _HDR suffix
        bool hasExplicitSdrOrHdr = folders
            .Where(f => Path.GetFileName(f).StartsWith("video_", StringComparison.InvariantCultureIgnoreCase))
            .Select(f => Path.GetFileName(f).ToLowerInvariant())
            .Any(name => name.EndsWith("_sdr") || name.Contains("_hdr"));

        // Probe video variants in parallel with bounded concurrency
        SemaphoreSlim semaphore = new(MaxConcurrentProbes);
        List<Task<VideoVariantInfo>> probeTasks = videoFileList.Select(async videoFile =>
        {
            string folderName = Path.GetFileName(Path.GetDirectoryName(videoFile) ?? string.Empty);

            // folder name expected like: video_1920x1080[_TAG]
            string resolution = "";
            bool isSdr = true;
            try
            {
                Match match = Regex.Match(folderName, @"video_(\d+)x(\d+)(?:_(.+))?", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    resolution = $"{match.Groups[1].Value}x{match.Groups[2].Value}";
                    string tag = match.Groups[3].Value;
                    if (!string.IsNullOrEmpty(tag) && tag.ToUpperInvariant().Contains("HDR"))
                        isSdr = false;
                }
            }
            catch (Exception ex) { Logger.App($"Failed to parse resolution from folder {folderName}: {ex.Message}"); }

            // Simple folder name convention for HDR detection:
            // If NO folders have _SDR or _HDR suffix at all, treat everything as SDR (preventive measure)
            // Otherwise:
            //   - Folders ending with _SDR are SDR versions
            //   - All others (including _HDR or no suffix) are HDR versions
            bool detectedHdr = hasExplicitSdrOrHdr;
            string detectedReason = hasExplicitSdrOrHdr
                ? "default (no _SDR suffix)"
                : "no explicit _SDR/_HDR markers found, defaulting to SDR";

            if (!string.IsNullOrEmpty(folderName))
            {
                string folderLower = folderName.ToLowerInvariant();
                if (folderLower.EndsWith("_sdr"))
                {
                    detectedHdr = false;
                    detectedReason = "folder ends with _SDR";
                }
                else if (folderLower.Contains("_hdr"))
                {
                    detectedHdr = true;
                    detectedReason = "folder contains _HDR";
                }
            }

            if (detectedHdr)
            {
                isSdr = false;
                Logger.App($"HDR: {folderName} - {detectedReason}");
            }
            else
            {
                Logger.App($"SDR: {folderName} - {detectedReason}");
            }

            // Get codec info for CODECS attribute (simplified - probe one file)
            string profile = "";
            string levelStr = "";
            string frameRateStr = "";
            double duration = 0;

            await semaphore.WaitAsync();
            try
            {
                try
                {
                    string folderPath = Path.Combine(basePath, folderName ?? string.Empty);
                    string probeTarget = videoFile;

                    // Try to get a .ts file for more accurate info
                    try
                    {
                        string? firstTs = Directory.EnumerateFiles(folderPath, "*.ts").FirstOrDefault();
                        if (!string.IsNullOrEmpty(firstTs)) probeTarget = firstTs;
                    }
                    catch (Exception ex) { Logger.App($"Failed to enumerate .ts files in {folderPath}: {ex.Message}"); }

                    string probeResult = (await Shell.ExecStdOutAsync(AppFiles.FfProbePath,
                        $"-v error -select_streams v:0 -show_entries stream=profile,level,r_frame_rate -of default=noprint_wrappers=1:nokey=1 \"{probeTarget}\"")).Trim();

                    if (!string.IsNullOrEmpty(probeResult))
                    {
                        string[] parts = probeResult.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0) profile = parts[0].Trim();
                        if (parts.Length > 1) levelStr = parts[1].Trim();
                        if (parts.Length > 2) frameRateStr = parts[2].Trim();
                    }
                }
                catch (Exception ex) { Logger.App($"Failed to probe codec info for {videoFile}: {ex.Message}"); }

                duration = await GetVideoDurationAsync(videoFile);
            }
            finally
            {
                semaphore.Release();
            }

            int level = int.TryParse(levelStr, out int l) ? l : 40;
            string vCodecProfile = MapProfileToCodec(profile, level);

            // Parse frame rate (e.g., "24000/1001" or "30/1")
            double frameRate = ParseFrameRate(frameRateStr);

            long totalSize = GetTotalSize(Path.Combine(basePath, folderName ?? string.Empty));

            double bandwidth = duration > 0 ? (totalSize * 8.0 / duration) : 0.0;
            bandwidth = Math.Round(bandwidth);
            long averageBandwidth = (long)bandwidth;
            long maxBandwidth = (long)(bandwidth * 1.1); // 10% peak over average
            bandwidth += 128000; // audio overhead estimate

            // Target duration is typically ceil of max segment duration (usually 6-10 seconds)
            int targetDuration = Math.Max(6, (int)Math.Ceiling(10.0));

            return new VideoVariantInfo
            {
                Resolution = resolution,
                FolderName = folderName,
                VideoFile = videoFile,
                VCodecProfile = vCodecProfile,
                Bandwidth = (long)bandwidth,
                AverageBandwidth = averageBandwidth,
                MaxBandwidth = maxBandwidth,
                FrameRate = frameRate,
                TargetDuration = targetDuration,
                IsSdr = isSdr
            };
        }).ToList();

        VideoVariantInfo[] probeResults = await Task.WhenAll(probeTasks);

        var videoGroups = probeResults
            .Where(x => !string.IsNullOrEmpty(x.Resolution))
            .GroupBy(v => v.Resolution)
            .ToList();

        foreach (var group in videoGroups)
        {
            // Order HDR/SDR so SDR appears first if present
            foreach (var video in group.OrderByDescending(v => v.IsSdr))
            {
                foreach (string audioGroup in audioGroups.Keys)
                {
                    string streamName = $"{video.Resolution} {(video.IsSdr ? "SDR" : "HDR")}";
                    
                    // Map audio codec name to proper codec string
                    string audioCodec = audioGroup.ToLowerInvariant() switch
                    {
                        "aac" => "mp4a.40.2",      // AAC-LC
                        "eac3" => "ec-3",          // E-AC-3 (Dolby Digital Plus)
                        "ac3" => "ac-3",           // AC-3 (Dolby Digital)
                        _ => "mp4a.40.2"           // Default to AAC-LC
                    };
                    
                    string codecs = video.VCodecProfile;
                    if (!string.IsNullOrEmpty(codecs)) 
                        codecs += $",{audioCodec}";
                    else 
                        codecs = audioCodec;

                    // Build complete STREAM-INF tag with all metadata
                    string streamInf = $"#EXT-X-STREAM-INF:BANDWIDTH={video.Bandwidth}";
                    streamInf += $",AVERAGE-BANDWIDTH={video.AverageBandwidth}";
                    streamInf += $",RESOLUTION={video.Resolution}";
                    
                    if (video.FrameRate > 0)
                        streamInf += $",FRAME-RATE={video.FrameRate:F3}";
                    
                    streamInf += $",CODECS=\"{codecs}\"";
                    streamInf += $",AUDIO=\"audio_{audioGroup}\"";
                    
                    // Add VIDEO-RANGE and COLOUR-SPACE
                    if (video.IsSdr)
                    {
                        streamInf += ",VIDEO-RANGE=SDR,COLOUR-SPACE=BT.709";
                    }
                    else
                    {
                        streamInf += ",VIDEO-RANGE=PQ,COLOUR-SPACE=BT.2020";
                        // Extended HDR metadata for PQ (HDR10)
                        streamInf += ",REQ-VIDEO-LAYOUT=BYTE";
                    }
                    
                    streamInf += $",NAME=\"{streamName}\"";
                    
                    masterPlaylist.AppendLine(streamInf);
                    masterPlaylist.AppendLine($"{video.FolderName}/{Path.GetFileName(video.VideoFile)}");
                    masterPlaylist.AppendLine();
                }
            }
        }

        string outPath = Path.Combine(basePath, filename + ".m3u8");
        await File.WriteAllTextAsync(outPath, masterPlaylist.ToString());
    }

    private static long GetTotalSize(string videoFolderPath)
    {
        if (string.IsNullOrEmpty(videoFolderPath) || !Directory.Exists(videoFolderPath))
            return 0;

        try
        {
            IEnumerable<string> segmentFiles = Directory.EnumerateFiles(videoFolderPath, "*.ts");
            long totalSize = 0;
            foreach (string segmentFile in segmentFiles)
            {
                try { totalSize += new FileInfo(segmentFile).Length; }
                catch (Exception) { /* File may have been deleted between enumeration and access */ }
            }

            return totalSize;
        }
        catch (Exception) { return 0; }
    }

    private static async Task<double> GetVideoDurationAsync(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return 0;

        string output = (await Shell.ExecStdOutAsync(AppFiles.FfProbePath,
                $"-v error -select_streams 0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\""))
            .Trim();

        string x = output.Replace("N/A", "0").Trim();
        if (string.IsNullOrEmpty(x)) return 0;
        if (double.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            return d;
        return 0;
    }

    private static string MapProfileToCodec(string profile, int level)
    {
        // Profile to hex mapping for H.264
        string profileHex = profile switch
        {
            "Baseline" => "42",
            "Constrained Baseline" => "42",
            "Main" => "4D",
            "Extended" => "58",
            "High" => "64",
            "High 10" => "6E",
            "High 4:2:2" => "7A",
            "High 4:4:4" => "F4",
            _ => "4D" // Default to Main
        };

        // Constraint flags (00 for most profiles, 40 for Constrained Baseline)
        string constraintHex = profile?.Contains("Constrained", StringComparison.OrdinalIgnoreCase) == true ? "40" : "00";

        // Level is provided as integer (e.g., 30 = 3.0, 40 = 4.0, 41 = 4.1, 51 = 5.1)
        // Convert to hex
        string levelHex = level.ToString("X2");

        return $"avc1.{profileHex}{constraintHex}{levelHex}";
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    private static double ParseFrameRate(string frameRateStr)
    {
        if (string.IsNullOrEmpty(frameRateStr))
            return 0.0;

        try
        {
            // Handle fraction format like "24000/1001" or "30/1"
            if (frameRateStr.Contains('/'))
            {
                string[] parts = frameRateStr.Split('/');
                if (parts.Length == 2 && 
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double numerator) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double denominator) &&
                    denominator != 0)
                {
                    return numerator / denominator;
                }
            }
            else if (double.TryParse(frameRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double rate))
            {
                return rate;
            }
        }
        catch (Exception ex) { Logger.App($"Failed to parse frame rate '{frameRateStr}': {ex.Message}"); }

        return 0.0;
    }

    private sealed class VideoVariantInfo
    {
        public string Resolution { get; init; } = "";
        public string FolderName { get; init; } = "";
        public string VideoFile { get; init; } = "";
        public string VCodecProfile { get; init; } = "";
        public long Bandwidth { get; init; }
        public long AverageBandwidth { get; init; }
        public long MaxBandwidth { get; init; }
        public double FrameRate { get; init; }
        public int TargetDuration { get; init; }
        public bool IsSdr { get; init; }
    }
}




