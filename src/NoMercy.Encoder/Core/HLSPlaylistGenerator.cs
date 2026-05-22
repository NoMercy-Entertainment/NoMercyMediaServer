using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using static NoMercy.Encoder.Core.IsoLanguageMapper;

namespace NoMercy.Encoder.Core;

public static class HlsPlaylistGenerator
{
    public static async Task Build(
        string basePath,
        string filename,
        List<string>? priorityLanguages = null
    )
    {
        priorityLanguages ??= ["eng", "jpn"];

        if (!Directory.Exists(basePath))
            return;

        string[] folders = Directory
            .GetDirectories(basePath)
            .Where(f =>
                Path.GetFileName(f)
                    .StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase)
                || Path.GetFileName(f)
                    .StartsWith("video_", StringComparison.InvariantCultureIgnoreCase)
            )
            .ToArray();

        List<string> videoFileList = folders
            .Where(f =>
                Path.GetFileName(f)
                    .StartsWith("video_", StringComparison.InvariantCultureIgnoreCase)
            )
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"))
            .ToList();

        List<string> audioFiles = folders
            .Where(f =>
                Path.GetFileName(f)
                    .StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase)
            )
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"))
            .ToList();

        // Order audio tracks by priority language, then language name, then folder size
        audioFiles = audioFiles
            .OrderBy(f =>
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(f).OrEmpty());
                string[] parts = folderName.Split('_');
                string language = parts.Length > 1 ? parts[1] : "und";
                int idx = priorityLanguages.IndexOf(language);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ThenBy(f =>
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(f).OrEmpty());
                string[] parts = folderName.Split('_');
                string language = parts.Length > 1 ? parts[1] : "und";
                return language.OrEmpty();
            })
            .ThenBy(f => GetTotalSize(Path.GetDirectoryName(f).OrEmpty()))
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
            string folderName = Path.GetFileName(Path.GetDirectoryName(audioFile).OrEmpty());
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

            string langDisplay = IsoToLanguage.TryGetValue(language, out string? langName)
                ? ToTitleCase(langName)
                : ToTitleCase(language);
            string uri = Path.Combine(folderName, Path.GetFileName(audioFile)).Replace("\\", "/");
            string defaultFlag = (index == 0) ? "YES" : "NO";

            masterPlaylist.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_{codecName}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={defaultFlag},URI=\"{uri}\",NAME=\"{langDisplay} {codecName}\""
            );
        }

        masterPlaylist.AppendLine();

        // Check if any video folder has _SDR or _HDR suffix
        bool hasExplicitSdrOrHdr = folders
            .Where(f =>
                Path.GetFileName(f)
                    .StartsWith("video_", StringComparison.InvariantCultureIgnoreCase)
            )
            .Select(f => Path.GetFileName(f).ToLowerInvariant())
            .Any(name => name.EndsWith("_sdr") || name.Contains("_hdr"));

        // Probe video variants in parallel with bounded concurrency (global throttle)
        List<Task<VideoVariantInfo>> probeTasks = videoFileList
            .Select(async videoFile =>
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(videoFile).OrEmpty())
                    .OrEmpty();

                // folder name expected like: video_1920x1080[_TAG]
                string resolution = "";
                bool isSdr = true;
                try
                {
                    Match match = Regex.Match(
                        folderName,
                        @"video_(\d+)x(\d+)(?:_(.+))?",
                        RegexOptions.IgnoreCase
                    );
                    if (match.Success)
                    {
                        resolution = $"{match.Groups[1].Value}x{match.Groups[2].Value}";
                        string tag = match.Groups[3].Value;
                        if (!string.IsNullOrEmpty(tag) && tag.ToUpperInvariant().Contains("HDR"))
                            isSdr = false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.App(
                        $"Failed to parse resolution from folder {folderName}: {ex.Message}"
                    );
                }

                // Simple folder name convention for HDR detection:
                // If NO folders have _SDR or _HDR suffix at all, treat everything as SDR (preventive measure)
                // Otherwise:
                //   - Folders ending with _SDR are SDR versions
                //   - All others (including _HDR or no suffix) are HDR versions
                // When ANY folder is explicitly tagged _SDR/_HDR, untagged folders default to HDR
                // (true). When NO folder carries an explicit tag, default everything to SDR
                // (false). The reason strings now match the boolean.
                bool detectedHdr = hasExplicitSdrOrHdr;
                string detectedReason = hasExplicitSdrOrHdr
                    ? "siblings carry _SDR/_HDR; untagged folder defaults to HDR"
                    : "no _SDR/_HDR markers in sibling set; defaulting to SDR";

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

                // Folder-name heuristic — kept as the fallback. The stream-metadata read
                // a few lines below takes priority once the probe lands.
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
                string codecName = "";
                string profile = "";
                string levelStr = "";
                string frameRateStr = "";
                string colorTransfer = "";
                string colorSpace = "";
                double duration = 0;

                await FfProbeThrottle.WaitAsync();
                try
                {
                    try
                    {
                        string folderPath = Path.Combine(basePath, folderName.OrEmpty());
                        string probeTarget = videoFile;

                        // Try to get a .ts file for more accurate info
                        try
                        {
                            string? firstTs = Directory
                                .EnumerateFiles(folderPath, "*.ts")
                                .FirstOrDefault();
                            if (!string.IsNullOrEmpty(firstTs))
                                probeTarget = firstTs;
                        }
                        catch (Exception ex)
                        {
                            Logger.App(
                                $"Failed to enumerate .ts files in {folderPath}: {ex.Message}"
                            );
                        }

                        string probeResult = (
                            await Shell.ExecStdOutAsync(
                                AppFiles.FfProbePath,
                                $"-v error -select_streams v:0 -show_entries stream=codec_name,profile,level,r_frame_rate,color_transfer,color_space -of default=noprint_wrappers=1:nokey=1 \"{probeTarget}\""
                            )
                        ).Trim();

                        if (!string.IsNullOrEmpty(probeResult))
                        {
                            string[] parts = probeResult.Split(
                                ['\r', '\n'],
                                StringSplitOptions.RemoveEmptyEntries
                            );
                            if (parts.Length > 0)
                                codecName = parts[0].Trim();
                            if (parts.Length > 1)
                                profile = parts[1].Trim();
                            if (parts.Length > 2)
                                levelStr = parts[2].Trim();
                            if (parts.Length > 3)
                                frameRateStr = parts[3].Trim();
                            if (parts.Length > 4)
                                colorTransfer = parts[4].Trim();
                            if (parts.Length > 5)
                                colorSpace = parts[5].Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.App($"Failed to probe codec info for {videoFile}: {ex.Message}");
                    }

                    duration = await GetVideoDurationAsync(videoFile);
                }
                finally
                {
                    FfProbeThrottle.Release();
                }

                int level = int.TryParse(levelStr, out int l) ? l : 40;
                string vCodecProfile = MapVideoCodec(codecName, profile, level);

                // Authoritative HDR detection: read transfer characteristics from the bitstream.
                // bt2020nc + smpte2084 = HDR10 (PQ). bt2020nc + arib-std-b67 = HLG. Anything else
                // (bt709, smpte170m, gamma22, empty/unknown) = SDR. Fall back to the folder-name
                // heuristic only when the probe didn't return a useful transfer value.
                if (IsHdrTransfer(colorTransfer))
                    isSdr = false;
                else if (IsSdrTransfer(colorTransfer))
                    isSdr = true;

                // Parse frame rate (e.g., "24000/1001" or "30/1")
                double frameRate = ParseFrameRate(frameRateStr);

                long totalSize = GetTotalSize(Path.Combine(basePath, folderName.OrEmpty()));

                // HLS spec: BANDWIDTH = peak per-segment bitrate, AVERAGE-BANDWIDTH = mean.
                // Both must include audio if the variant is muxed or grouped with an audio rendition.
                // We estimate audio at 128 kbps for both (default AAC stereo) — accurate enough until
                // we read the actual audio bitrate from the rendition.
                const long AudioOverheadBps = 128_000;
                double videoAverageBps = duration > 0 ? (totalSize * 8.0 / duration) : 0.0;
                long averageBandwidth = (long)Math.Round(videoAverageBps) + AudioOverheadBps;
                long peakBandwidth = (long)Math.Round(videoAverageBps * 1.1) + AudioOverheadBps;

                return new VideoVariantInfo
                {
                    Resolution = resolution,
                    FolderName = folderName!,
                    VideoFile = videoFile,
                    VCodecProfile = vCodecProfile,
                    Bandwidth = peakBandwidth,
                    AverageBandwidth = averageBandwidth,
                    FrameRate = frameRate,
                    IsSdr = isSdr,
                };
            })
            .ToList();

        VideoVariantInfo[] probeResults = await Task.WhenAll(probeTasks);

        List<IGrouping<string, VideoVariantInfo>> videoGroups = probeResults
            .Where(x => !string.IsNullOrEmpty(x.Resolution))
            .GroupBy(v => v.Resolution)
            .ToList();

        foreach (IGrouping<string, VideoVariantInfo> group in videoGroups)
        {
            // Order HDR/SDR so SDR appears first if present
            foreach (VideoVariantInfo video in group.OrderByDescending(v => v.IsSdr))
            {
                foreach (string audioGroup in audioGroups.Keys)
                {
                    string streamName = $"{video.Resolution} {(video.IsSdr ? "SDR" : "HDR")}";

                    string audioCodec = MapAudioCodec(audioGroup);

                    string codecs = video.VCodecProfile;
                    if (!string.IsNullOrEmpty(codecs))
                        codecs += $",{audioCodec}";
                    else
                        codecs = audioCodec;

                    // Build complete STREAM-INF tag with all metadata
                    StringBuilder streamInf = new();
                    streamInf.Append($"#EXT-X-STREAM-INF:BANDWIDTH={video.Bandwidth}");
                    streamInf.Append($",AVERAGE-BANDWIDTH={video.AverageBandwidth}");
                    streamInf.Append($",RESOLUTION={video.Resolution}");

                    if (video.FrameRate > 0)
                        streamInf.Append($",FRAME-RATE={video.FrameRate:F3}");

                    streamInf.Append($",CODECS=\"{codecs}\"");
                    streamInf.Append($",AUDIO=\"audio_{audioGroup}\"");

                    // Add VIDEO-RANGE and COLOUR-SPACE.
                    // REQ-VIDEO-LAYOUT takes layout strings (e.g. CH-STEREO) — not HDR signaling.
                    // VIDEO-RANGE=PQ is the canonical HDR10 marker; players read transfer characteristics
                    // from the bitstream itself, not from a separate attribute.
                    if (video.IsSdr)
                    {
                        streamInf.Append(",VIDEO-RANGE=SDR,COLOUR-SPACE=BT.709");
                    }
                    else
                    {
                        streamInf.Append(",VIDEO-RANGE=PQ,COLOUR-SPACE=BT.2020");
                    }

                    streamInf.Append($",NAME=\"{streamName}\"");

                    masterPlaylist.AppendLine(streamInf.ToString());
                    masterPlaylist.AppendLine(
                        $"{video.FolderName}/{Path.GetFileName(video.VideoFile)}"
                    );
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
                try
                {
                    totalSize += new FileInfo(segmentFile).Length;
                }
                catch (Exception)
                { /* File may have been deleted between enumeration and access */
                }
            }

            return totalSize;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static async Task<double> GetVideoDurationAsync(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return 0;

        string output = (
            await Shell.ExecStdOutAsync(
                AppFiles.FfProbePath,
                $"-v error -select_streams 0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\""
            )
        ).Trim();

        string trimmed = output.Replace("N/A", "0").Trim();
        if (string.IsNullOrEmpty(trimmed))
            return 0;
        if (
            double.TryParse(
                trimmed,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double seconds
            )
        )
            return seconds;
        return 0;
    }

    /// <summary>
    ///     <c>true</c> when the ffprobe <c>color_transfer</c> value identifies an HDR transfer
    ///     function — SMPTE 2084 (HDR10 / PQ) or ARIB STD-B67 (HLG). Empty or unrecognized values
    ///     return <c>false</c> so callers fall back to their own heuristic.
    /// </summary>
    public static bool IsHdrTransfer(string colorTransfer)
    {
        string normalized = (colorTransfer ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "smpte2084" => true,
            "arib-std-b67" => true,
            "bt2020-10" => true,
            "bt2020-12" => true,
            _ => false,
        };
    }

    /// <summary>
    ///     <c>true</c> when the ffprobe <c>color_transfer</c> value names a known SDR transfer
    ///     (bt709, smpte170m, gamma22, gamma28, bt470m, bt470bg, linear, log, iec61966-2-1).
    ///     Used together with <see cref="IsHdrTransfer"/> to decide HDR/SDR without folder-name
    ///     heuristics.
    /// </summary>
    public static bool IsSdrTransfer(string colorTransfer)
    {
        string normalized = (colorTransfer ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "bt709" => true,
            "smpte170m" => true,
            "bt470m" => true,
            "bt470bg" => true,
            "gamma22" => true,
            "gamma28" => true,
            "linear" => true,
            "log" => true,
            "log100" => true,
            "log316" => true,
            "iec61966-2-1" => true,
            "iec61966-2-4" => true,
            "bt1361e" => true,
            "smpte240m" => true,
            _ => false,
        };
    }

    /// <summary>
    ///     Build the RFC 6381 CODECS string for a video rendition. Falls back to H.264 Main 4.0
    ///     (<c>avc1.4D0028</c>) when the codec name is unknown — matches the historical default for
    ///     callers that never read codec_name from ffprobe.
    /// </summary>
    public static string MapVideoCodec(string codecName, string profile, int level)
    {
        string normalized = (codecName ?? string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "hevc" or "h265" or "x265" => MapHevcCodec(profile, level),
            "av1" => MapAv1Codec(profile, level),
            "vp9" => MapVp9Codec(profile, level),
            _ => MapH264Codec(profile, level),
        };
    }

    /// <summary>
    ///     Map an audio rendition codec name to its RFC 6381 fourCC. Falls back to AAC-LC when the
    ///     codec name is unknown — matches what the HLS folder convention emits for the encoder's
    ///     default audio profile.
    /// </summary>
    public static string MapAudioCodec(string codecName)
    {
        return (codecName ?? string.Empty).ToLowerInvariant() switch
        {
            "aac" => "mp4a.40.2",
            "eac3" or "e-ac-3" => "ec-3",
            "ac3" or "ac-3" => "ac-3",
            "opus" => "opus",
            "flac" => "fLaC",
            "mp3" => "mp4a.40.34",
            _ => "mp4a.40.2",
        };
    }

    private static string MapH264Codec(string profile, int level)
    {
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
            _ => "4D",
        };

        string constraintHex =
            profile?.Contains("Constrained", StringComparison.OrdinalIgnoreCase) == true
                ? "40"
                : "00";

        string levelHex = level.ToString("X2");
        return $"avc1.{profileHex}{constraintHex}{levelHex}";
    }

    private static string MapHevcCodec(string profile, int level)
    {
        // RFC 7798: hvc1.{profile_space}{profile_idc}.{compat_flags}.{tier}{level}.{constraint}
        // profile_space is 0 for typical profiles → emit empty (omit prefix digit).
        // ffprobe encodes the level as 30 × actual_level (e.g. Main 4.0 = 120, Main 5.1 = 153).
        (string profileIdc, string compatFlags) = profile switch
        {
            "Main" => ("1", "6"),
            "Main 10" => ("2", "4"),
            "Main 10 Intra" => ("2", "4"),
            "Main Still Picture" => ("3", "2"),
            "Main 4:2:2 10" or "Rext" => ("4", "8"),
            _ => ("1", "6"),
        };

        int hevcLevel = level > 0 ? level : 120; // ffprobe default → Main 4.0
        return $"hvc1.{profileIdc}.{compatFlags}.L{hevcLevel}.B0";
    }

    private static string MapAv1Codec(string profile, int level)
    {
        // AV1 codec string: av01.{seq_profile}.{seq_level_idx}{tier}.{bit_depth}
        // seq_profile: 0 = Main (8/10-bit 4:2:0), 1 = High (8/10-bit 4:4:4), 2 = Professional.
        // Tier letter: M = Main, H = High. seq_level_idx is two digits.
        string seqProfile = profile switch
        {
            "High" => "1",
            "Professional" => "2",
            _ => "0",
        };

        int av1Level = level > 0 ? level : 4; // default to level 4.0
        return $"av01.{seqProfile}.{av1Level:D2}M.08";
    }

    private static string MapVp9Codec(string profile, int level)
    {
        // VP9: vp09.{profile}.{level}.{bit_depth}
        string vp9Profile = profile switch
        {
            "Profile 1" => "01",
            "Profile 2" => "02",
            "Profile 3" => "03",
            _ => "00",
        };

        int vp9Level = level > 0 ? level : 40;
        return $"vp09.{vp9Profile}.{vp9Level:D2}.08";
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
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
                if (
                    parts.Length == 2
                    && double.TryParse(
                        parts[0],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double numerator
                    )
                    && double.TryParse(
                        parts[1],
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double denominator
                    )
                    && denominator != 0
                )
                {
                    return numerator / denominator;
                }
            }
            else if (
                double.TryParse(
                    frameRateStr,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double rate
                )
            )
            {
                return rate;
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed to parse frame rate '{frameRateStr}': {ex.Message}");
        }

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
        public double FrameRate { get; init; }
        public bool IsSdr { get; init; }
    }
}
