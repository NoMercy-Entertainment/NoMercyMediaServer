using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FFMpegCore;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;

namespace NoMercy.Encoder;
[Serializable]
public class FfMpeg : Classes
{
    internal string FfProbePath { get; set; } = AppFiles.FfProbePath;
    internal string FfmpegPath { get; set; } = AppFiles.FfmpegPath;

    private static readonly Dictionary<int,Process> FfmpegProcess = new();

    internal MediaAnalysis? MediaAnalysis;

    public FfMpeg()
    {
    }

    public FfMpeg(string ffmpeg, string ffProbePath)
    {
        FfmpegPath = ffmpeg;
        FfProbePath = ffProbePath;
    }

    public void SetFfMpegDriver(string ffmpegDriver)
    {
        FfmpegPath = ffmpegDriver;
    }

    public void SetFFprobe(string ffprobe)
    {
        FfProbePath = ffprobe;
    }

    public string GetFfMpegDriver()
    {
        return FfmpegPath;
    }

    public string GetFFprobe()
    {
        return FfProbePath;
    }

    public string GetFfMpegVersion()
    {
        return FfmpegPath;
    }

    public string GetFFprobeVersion()
    {
        return FfProbePath;
    }

    public VideoAudioFile Open(string path)
    {
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        // first ffprobe the file check for streams
        MediaAnalysis = new(FFProbe.Analyse(path), path);

        if (MediaAnalysis.VideoStreams.Count > 0)
            return new VideoFile(MediaAnalysis, FfmpegPath);

        if (MediaAnalysis.AudioStreams.Count > 0)
            return new AudioFile(MediaAnalysis, FfmpegPath);

        throw new("No streams found");
    }

    public class FolderAndFile
    {
        public string HostFolder { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    public VideoAudioFile Open(FolderAndFile? videoFile)
    {
        string inputFile = $"{videoFile?.HostFolder}{videoFile?.Filename}";
        return Open(inputFile);
    }

    public static async Task<string> Exec(string args, string? cwd = null, string? executable = null)
    {
        Process ffmpeg = new();

        ffmpeg.StartInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = executable ?? AppFiles.FfmpegPath,
            WorkingDirectory = cwd,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };

        // Logger.Encoder(ffmpeg.StartInfo.WorkingDirectory  + " " + ffmpeg.StartInfo.FileName + " " + ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();
        FfmpegProcess.Add(ffmpeg.Id, ffmpeg);

        string error = await ffmpeg.StandardError.ReadToEndAsync();

        FfmpegProcess.Remove(ffmpeg.Id);
        ffmpeg.Close();

        return error;
    }

    public static async Task<string> Ffprobe(string args, string? cwd = null, string? executable = null)
    {
        Process ffprobe = new();
        ffprobe.StartInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = executable ?? AppFiles.FfProbePath,
            WorkingDirectory = cwd,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };

        ffprobe.Start();

        string result = await ffprobe.StandardOutput.ReadToEndAsync();

        ffprobe.Close();

        return result;
    }

    public static async Task<string> Run(string args, string cwd, ProgressMeta meta)
    {
        Process ffmpeg = new();

        ffmpeg.StartInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = AppFiles.FfmpegPath,

            WorkingDirectory = cwd,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };

        // Logger.Encoder(ffmpeg.StartInfo.WorkingDirectory  + " " + ffmpeg.StartInfo.FileName + " " + ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();
        FfmpegProcess.Add(ffmpeg.Id, ffmpeg);

        ffmpeg.BeginOutputReadLine();
        ffmpeg.BeginErrorReadLine();

        StringBuilder output = new();

        TimeSpan totalDuration = TimeSpan.Zero;
        bool durationFound = false;
        double progressPercentage = 0.0;
        TimeSpan currentTime = TimeSpan.Zero;

        StringBuilder output2 = new();
        ffmpeg.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                if (!durationFound)
                {
                    // Extract duration from the log
                    Regex durationRegex = new(@"Duration:\s(\d{2}):(\d{2}):(\d{2})\.(\d+)");
                    Match durationMatch = durationRegex.Match(e.Data);

                    if (durationMatch.Success)
                    {
                        int hours = int.Parse(durationMatch.Groups[1].Value);
                        int minutes = int.Parse(durationMatch.Groups[2].Value);
                        int seconds = int.Parse(durationMatch.Groups[3].Value);
                        int milliseconds = int.Parse(durationMatch.Groups[4].Value);

                        totalDuration = new(0, hours, minutes, seconds, milliseconds * 10);
                        durationFound = true;
                        // Logger.Encoder($"Total Duration: {totalDuration}");
                    }
                }
        };

        ffmpeg.OutputDataReceived += (_, e) =>
        {
            try
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);

                    output2.AppendLine(e.Data);

                    string[] x = Regex.Split( output2.ToString(), @"[\r\n]+");

                    IEnumerable<string[]> enumerable = x
                        .Select(y => y.Split("="))
                        .Where(y => y.Length == 2);

                    Dictionary<string, dynamic> enumerable2 = new();
                    foreach (string[] strings in enumerable)
                        enumerable2.Add(strings.FirstOrDefault() ?? "", strings.LastOrDefault() ?? "");

                    enumerable2.Add("totalDuration", totalDuration);

                    Regex progressRegex = new(@"(\d{2}):(\d{2}):(\d{2})\.(\d+)");
                    dynamic? progressMatch =
                        progressRegex.Match(enumerable2.GetValueOrDefault("out_time", ""));

                    if (progressMatch.Success)
                    {
                        int hours = int.Parse(progressMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                        int minutes = int.Parse(progressMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                        int seconds = int.Parse(progressMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                        int milliseconds = int.Parse(progressMatch.Groups[4].Value, CultureInfo.InvariantCulture);

                        currentTime = new(0, hours, minutes, seconds, milliseconds / 100);
                        progressPercentage = currentTime.TotalMilliseconds / totalDuration.TotalMilliseconds * 100;
                    }

                    double speed = enumerable2.TryGetValue("speed", out dynamic? s) ? double.Parse(s.Replace("N/A", "0").TrimEnd('x'), CultureInfo.InvariantCulture) : 0;

                    double fps = enumerable2.TryGetValue("fps", out dynamic? f) ? double.Parse(f, CultureInfo.InvariantCulture) : 0;
                    int frame = enumerable2.TryGetValue("frame", out dynamic? f2) ? int.Parse(f2, CultureInfo.InvariantCulture) : 0;
                    string bitrate = enumerable2.GetValueOrDefault("bitrate", "");

                    double remaining =
                        Math.Floor((totalDuration.TotalSeconds - currentTime.TotalSeconds) / speed);

                    string? thumbFolder = Directory.GetDirectories(meta.BaseFolder, "*thumbs_*")
                        .FirstOrDefault();

                    string thumbnail = "";
                    string thumbnailFolder = "";
                    if (Directory.Exists(thumbFolder))
                    {
                        string file  = Directory.GetFiles(thumbFolder)
                            .OrderByDescending(file => new FileInfo(file).LastWriteTimeUtc)
                            .FirstOrDefault() ?? "";

                        thumbnail = Path.GetFileName(file);
                        thumbnailFolder = Path.GetFileNameWithoutExtension(thumbnail).Split("-").FirstOrDefault() ?? "";
                    }

                    string remainingHms = TimeSpan.FromSeconds(double.IsPositiveInfinity(remaining) || double.IsNegativeInfinity(remaining) ? 0 : remaining).ToString();

                    if (e.Data.Contains("progress") || e.Data.Contains("continue"))
                    {
                        Progress progress = new()
                        {
                            Percentage = progressPercentage,
                            Status = "running",
                            CurrentTime = currentTime.TotalSeconds,
                            Duration = totalDuration.TotalSeconds,
                            Remaining = remaining,
                            RemainingHms = remainingHms,
                            Fps = fps,
                            Speed = speed,
                            Frame = frame,
                            Bitrate = bitrate,
                            HasGpu = meta.HasGpu,
                            IsHdr = meta.IsHdr,
                            VideoStreams = meta.VideoStreams,
                            AudioStreams = meta.AudioStreams,
                            SubtitleStreams = meta.SubtitleStreams,
                            Thumbnail = $"{meta.ShareBasePath}/{thumbnailFolder}/{thumbnail}",
                            Title = meta.Title,
                            Id = meta.Id,
                            Message = "Encoding video",
                            ProgressId = ffmpeg.Id
                        };

                        progress.RemainingSplit = progress.RemainingHms
                            .Split(":")
                            .Prepend("0")
                            .ToArray();

                        Networking.Networking.SendToAll("encoder-progress", "dashboardHub", progress);
                        output2 = new();
                    }
                }
            }
            catch (Exception)
            {
                //
            }
        };

        await ffmpeg.WaitForExitAsync();

        FfmpegProcess.Remove(ffmpeg.Id);
        ffmpeg.Close();

        Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
        {
            Status = "completed",
            Id = meta.Id
        });

        return output.ToString();
    }

    public static async Task<bool> Pause(int id)
    {
        if (FfmpegProcess.TryGetValue(id, out Process? process))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await SuspendProcessOnWindows(process);
            }
            else{
                Process.Start("kill", $"-STOP {process.Id}");
                await Task.Delay(0);
            }
            return true;
        }

        return false;
    }

    public static async Task<bool> Resume(int id)
    {
        if (FfmpegProcess.TryGetValue(id, out Process? process))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ResumeProcessOnWindows(process);
            }
            else
            {
                Process.Start("kill", $"-CONT {process.Id}");
                await Task.Delay(0);
            }

            return true;
        }

        return false;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenThread(int dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int ThreadSuspendResume = 0x0002;

    private static async Task SuspendProcessOnWindows(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            IntPtr threadHandle = OpenThread(ThreadSuspendResume, false, (uint)thread.Id);
            if (threadHandle != IntPtr.Zero)
            {
                SuspendThread(threadHandle);
                CloseHandle(threadHandle);
            }
        }
        await Task.CompletedTask;
    }

    private static async Task ResumeProcessOnWindows(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            IntPtr threadHandle = OpenThread(ThreadSuspendResume, false, (uint)thread.Id);
            if (threadHandle != IntPtr.Zero)
            {
                ResumeThread(threadHandle);
                CloseHandle(threadHandle);
            }
        }
        await Task.CompletedTask;
    }
}
