using System.Diagnostics;
using System.Globalization;
using System.Text;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using static NoMercy.Encoder.Core.IsoLanguageMapper;

namespace NoMercy.Encoder.Core;

public static class HlsPlaylistGenerator
{
    public static Task Build(string basePath, string filename, List<string>? priorityLanguages = null)
    {
        priorityLanguages ??= ["eng", "jpn"];

        string[] folders = Directory.GetDirectories(basePath)
            .Where(f => Path.GetFileName(f).StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase) ||
                        Path.GetFileName(f).StartsWith("video_", StringComparison.InvariantCultureIgnoreCase))
            .ToArray();

        IEnumerable<string> videoFiles = folders
            .Where(f => Path.GetFileName(f).StartsWith("video_", StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"));

        List<string> audioFiles = folders
            .Where(f => Path.GetFileName(f).StartsWith("audio_", StringComparison.InvariantCultureIgnoreCase))
            .SelectMany(f => Directory.GetFiles(f, "*.m3u8"))
            .OrderBy(f =>
            {
                string? folderName = Path.GetFileName(Path.GetDirectoryName(f));
                string[] parts = folderName?.Split('_') ?? ["eng", "aac"];
                string language = parts[1];

                int priorityIndex = priorityLanguages.IndexOf(language);
                return priorityIndex >= 0 ? priorityIndex : int.MaxValue;
            })
            .ThenBy(f =>
            {
                string? folderName = Path.GetFileName(Path.GetDirectoryName(f));
                string[] parts = folderName?.Split('_') ?? ["eng", "aac"];
                return parts[1];
            })
            .ThenBy(f => GetTotalSize(Path.GetDirectoryName(f) ?? ""))
            .ToList();

        StringBuilder masterPlaylist = new();
        masterPlaylist.AppendLine("#EXTM3U");
        masterPlaylist.AppendLine("#EXT-X-VERSION:6");
        masterPlaylist.AppendLine();

        Dictionary<string, List<string>> audioGroups = new();
        foreach (string audioFile in audioFiles)
        {
            string? folderName = Path.GetFileName(Path.GetDirectoryName(audioFile));
            string[] parts = folderName?.Split('_') ?? ["eng", "aac"];
            string language = parts[1];
            string codecName = parts.Length > 2 ? parts[2] : "aac";
            int index = audioFiles.IndexOf(audioFile);

            if (!audioGroups.ContainsKey(codecName)) audioGroups[codecName] = [];
            audioGroups[codecName].Add(language);

            masterPlaylist.AppendLine(
                $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_{codecName}\",LANGUAGE=\"{language}\",AUTOSELECT=YES,DEFAULT={(index == 0 ? "YES" : "NO")},URI=\"{folderName}/{folderName}.m3u8\",NAME=\"{IsoToLanguage[language].ToTitleCase()} {codecName}\"");
        }

        masterPlaylist.AppendLine();

        var videoGroups = videoFiles
            .Select(videoFile =>
            {
                string? folderName = Path.GetFileName(Path.GetDirectoryName(videoFile));
                string[] parts = folderName?.Split('_', 'x') ?? ["1920", "1080", ""];
                string resolution = $"{parts[1]}x{parts[2]}";
                bool isSdr = parts.Length == 4 && parts[3] == "SDR";

                string vCodec = RunProcess(AppFiles.FfProbePath,
                        $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 {videoFile}")
                    .Trim();
                string profile = RunProcess(AppFiles.FfProbePath,
                        $"-v error -select_streams v:0 -show_entries stream=profile -of default=noprint_wrappers=1:nokey=1 {videoFile}")
                    .Trim();
                string vCodecProfile = MapProfileToCodec(profile);

                double duration = GetVideoDuration(videoFile) / 100000;
                double totalSize = GetTotalSize(Path.Combine(basePath, folderName ?? ""));

                double bandwidth = totalSize * 8 / duration;
                bandwidth = Math.Round(bandwidth);

                bandwidth += 128000; // Adding an estimated overhead for audio

                return new
                {
                    Resolution = resolution,
                    FolderName = folderName,
                    VideoFile = videoFile,
                    VCodecProfile = vCodecProfile,
                    Bandwidth = bandwidth,
                    VCodec = vCodec,
                    IsSdr = isSdr
                };
            })
            .GroupBy(v => new { v.Resolution });

        foreach (var group in videoGroups)
        foreach (var video in group.OrderByDescending(v => v.IsSdr))
        foreach (string audioGroup in audioGroups.Keys)
        {
            string streamName = $"{video.Resolution} {(video.IsSdr ? "SDR" : "HDR")}";
            masterPlaylist.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={video.Bandwidth},RESOLUTION={video.Resolution},CODECS=\"{video.VCodecProfile},mp4a.40.2\",AUDIO=\"audio_{audioGroup}\"{(video.IsSdr ? ",VIDEO-RANGE=SDR" : ",VIDEO-RANGE=PQ")},NAME=\"{streamName}\"");
            masterPlaylist.AppendLine($"{video.FolderName}/{Path.GetFileName(video.VideoFile)}");
            masterPlaylist.AppendLine();
        }

        File.WriteAllText(Path.Combine(basePath, filename + ".m3u8"), masterPlaylist.ToString());

        return Task.CompletedTask;
    }

    private static double GetTotalSize(string videoFolderPath)
    {
        string[] segmentFiles = Directory.GetFiles(videoFolderPath, "*.ts");
        long totalSize = segmentFiles.Sum(segmentFile => new FileInfo(segmentFile).Length);
        return totalSize;
    }

    private static double GetVideoDuration(string videoPath)
    {
        string output = RunProcess(AppFiles.FfProbePath,
            $"-v error -select_streams 0 -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"");
        
        string x = output.Trim().Replace("N/A", "0");
        if (x == "")
        {
            return 0;
        }
        return double.Parse(x, CultureInfo.InvariantCulture);
    }

    public static string RunProcess(string command, string arguments, string? cwd = null)
    {
        Process process = new()
        {
            StartInfo = new()
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }

    private static string MapProfileToCodec(string profile)
    {
        return profile switch
        {
            "Baseline" => "avc1.42E01E",
            "Main" => "avc1.4D401E",
            "High" => "avc1.64001F",
            _ => "avc1.4D401E"
        };
    }
}