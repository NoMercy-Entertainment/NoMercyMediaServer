﻿using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FFMpegCore;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

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

    public void SetFfProbe(string ffprobe)
    {
        FfProbePath = ffprobe;
    }

    public string GetFfMpegDriver()
    {
        return FfmpegPath;
    }

    public string GetFfProbe()
    {
        return FfProbePath;
    }

    public string GetFfMpegVersion()
    {
        return FfmpegPath;
    }

    public string GetFfProbeVersion()
    {
        return FfProbePath;
    }

    public VideoAudioFile Open(string path)
    {
        GlobalFFOptions.Configure(options => options.BinaryFolder = Path.Combine(AppFiles.BinariesPath, "ffmpeg"));

        // first ffprobe the file check for streams
        MediaAnalysis = new(FFProbe.Analyse(path), path);

        if (MediaAnalysis.VideoStreams.Count(s => s.CodecName != "mjpeg") > 0)
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
    
    public class ProgressData
    {
        public double ProgressPercentage { get; set; }
        public TimeSpan CurrentTime { get; set; }
        public double Speed { get; set; }
        public double Fps { get; set; }
        public int Frame { get; set; }
        public string Bitrate { get; set; } = string.Empty;
        public double Remaining { get; set; }

    }

    public VideoAudioFile Open(FolderAndFile? videoFile)
    {
        string inputFile = $"{videoFile?.HostFolder}{videoFile?.Filename}";
        return Open(inputFile);
    }

    public static async Task<string> ExecStdErrOut(string args, string? cwd = null, string? executable = null)
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
        
        ffmpeg.Start();
        FfmpegProcess.Add(ffmpeg.Id, ffmpeg);

        string error = await ffmpeg.StandardError.ReadToEndAsync();

        FfmpegProcess.Remove(ffmpeg.Id);
        ffmpeg.Close();

        return error;
    }

    public static async Task<string> Run(string args, string cwd, ProgressMeta meta)
    {
        Process ffmpeg = new()
        {
            StartInfo = new()
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
            }
        };

        ffmpeg.Start();
        FfmpegProcess.Add(ffmpeg.Id, ffmpeg);

        ffmpeg.BeginOutputReadLine();
        ffmpeg.BeginErrorReadLine();

        StringBuilder output = new();
        StringBuilder output2 = new();
        TimeSpan totalDuration = TimeSpan.Zero;
        bool durationFound = false;
        bool hasOutput = false;
        TimeSpan currentTime;

        Regex durationRegex = new(@"Duration:\s(\d{2}):(\d{2}):(\d{2})\.(\d+)");

        StringBuilder error = new();

        ffmpeg.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null && !durationFound)
            {
                // Extract duration from the log
                Match durationMatch = durationRegex.Match(e.Data);
                if (durationMatch.Success)
                {
                    int hours = int.Parse(durationMatch.Groups[1].Value);
                    int minutes = int.Parse(durationMatch.Groups[2].Value);
                    int seconds = int.Parse(durationMatch.Groups[3].Value);
                    int milliseconds = int.Parse(durationMatch.Groups[4].Value);

                    totalDuration = new(0, hours, minutes, seconds, milliseconds * 10);
                    durationFound = true;
                }
                else
                {
                    error.AppendLine(e.Data);
                }
            }
            else
            {
                error.AppendLine(e.Data);
            }
        };

        ffmpeg.OutputDataReceived += (_, e) =>
        {
            hasOutput = true;
            try
            {
                if (e.Data == null) return;

                output.AppendLine(e.Data);
                output2.AppendLine(e.Data);

                ProgressData? parsedData = ParseOutputData(output2.ToString(), totalDuration);

                if (parsedData == null) return;

                double progress = parsedData.ProgressPercentage;
                currentTime = parsedData.CurrentTime;
                double speed = parsedData.Speed;
                double fps = parsedData.Fps;
                int frame = parsedData.Frame;
                string bitrate = parsedData.Bitrate;
                double remaining = parsedData.Remaining;

                string remainingHms = TimeSpan.FromSeconds(remaining).ToString(@"d\:hh\:mm\:ss");

                string thumbnail = GetThumbnail(meta);

                Progress progressData = new()
                {
                    Percentage = progress,
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
                    Thumbnail = thumbnail,
                    Title = meta.Title,
                    Id = meta.Id,
                    Message = $"Encoding {meta.Type}",
                    ProgressId = ffmpeg.Id
                };

                progressData.RemainingSplit = progressData.RemainingHms
                    .Split(":")
                    .ToArray();

                if (progressData.Speed == 0) return;

                Networking.Networking.SendToAll("encoder-progress", "dashboardHub", progressData);

                output2.Clear();
            }
            catch (Exception ex)
            {
                Logger.Encoder($"Error processing output: {ex.Message}");
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

        if (!hasOutput && error.Length > 0)
            throw new(error.ToString());

        return output.ToString();
    }

    static ProgressData? ParseOutputData(string output, TimeSpan totalDuration)
    {
        try
        {
            double progressPercentage = 0.0;
            TimeSpan currentTime = TimeSpan.Zero;

            string[] lines = Regex.Split(output, "[\r\n]+");
            Dictionary<string, string> parsedValues = new();

            foreach (string line in lines)
            {
                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    parsedValues[key] = value;
                }
            }

            parsedValues["totalDuration"] = totalDuration.ToString();

            Regex progressRegex = new(@"(\d{2}):(\d{2}):(\d{2})\.(\d+)");
            Match progressMatch = progressRegex.Match(parsedValues.GetValueOrDefault("out_time", string.Empty));

            if (progressMatch.Success)
            {
                int hours = int.Parse(progressMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int minutes = int.Parse(progressMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                int seconds = int.Parse(progressMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                int milliseconds = int.Parse(progressMatch.Groups[4].Value, CultureInfo.InvariantCulture) / 100;

                currentTime = new(0, hours, minutes, seconds, milliseconds);
                progressPercentage = (currentTime.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100;
            }

            double speed = parsedValues.TryGetValue("speed", out string? speedStr)
                ? double.Parse(speedStr.Replace("N/A", "0").TrimEnd('x'), CultureInfo.InvariantCulture)
                : 0.0;
            double fps = parsedValues.TryGetValue("fps", out string? fpsStr)
                ? double.Parse(fpsStr, CultureInfo.InvariantCulture)
                : 0.0;
            int frame = parsedValues.TryGetValue("frame", out string? frameStr)
                ? int.Parse(frameStr, CultureInfo.InvariantCulture)
                : 0;
            string bitrate = parsedValues.GetValueOrDefault("bitrate", string.Empty);

            double remaining = speed > 0 ? Math.Floor((totalDuration.TotalSeconds - currentTime.TotalSeconds) / speed) : 0.0;

            return new()
            {
                ProgressPercentage = progressPercentage,
                CurrentTime = currentTime,
                Speed = speed,
                Fps = fps,
                Frame = frame,
                Bitrate = bitrate,
                Remaining = remaining
            };
        }
        catch (Exception ex)
        {
            Logger.Encoder($"Error parsing output data: {ex.Message}");
        }
        
        return null;
    }

    private static string GetThumbnail(ProgressMeta meta)
    {
        string? thumbFolder = Directory.GetDirectories(meta.BaseFolder, "*thumbs_*")
            .FirstOrDefault();

        if (!Directory.Exists(thumbFolder)) return "";

        string file  = Directory.GetFiles(thumbFolder)
            .OrderByDescending(file => new FileInfo(file).LastWriteTimeUtc)
            .FirstOrDefault() ?? "";

        string thumbnail = Path.GetFileName(file);
        string thumbnailFolder = Path.GetFileNameWithoutExtension(thumbnail)
            .Split("-").FirstOrDefault() ?? "";

        return $"{meta.ShareBasePath}/{thumbnailFolder}/{thumbnail}";
    }

    public static async Task<string> GetFingerprint(string file)
    {
        Process process1 = new()
        {
            StartInfo =
            {
                FileName = AppFiles.FfmpegPath,
                Arguments = "-hide_banner -i \"" + file + "\" -map 0:a:0  -ar 11025 -f chromaprint -t 120 -",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process1.Start();

        using StreamReader outputReader = process1.StandardOutput;
        using StreamReader errorReader = process1.StandardError;
    
        // Read both streams simultaneously
        Task<string> outputTask = outputReader.ReadToEndAsync();
        Task<string> errorTask = errorReader.ReadToEndAsync();
    
        await Task.WhenAll(outputTask, errorTask);
        string fingerprint = await outputTask;
        await process1.WaitForExitAsync();

        return fingerprint;
    }
    
    public static async Task<string> GetDuration(string file)
    {
        Process process2 = new()
        {
            StartInfo =
            {
                FileName = AppFiles.FfProbePath,
                Arguments = "-i \"" + file + "\" -hide_banner -show_entries format=duration -of default=noprint_wrappers=1:nokey=1",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process2.Start();

        using StreamReader outputReader2 = process2.StandardOutput;
        using StreamReader errorReader2 = process2.StandardError;
    
        // Read both streams simultaneously
        Task<string> outputTask2 = outputReader2.ReadToEndAsync();
        Task<string> errorTask2 = errorReader2.ReadToEndAsync();
    
        await Task.WhenAll(outputTask2, errorTask2);
        string time = await outputTask2;
        await process2.WaitForExitAsync();
        
        if (string.IsNullOrEmpty(time))
        {
            throw new("Failed to get duration");
        }
        
        if (time.Contains("N/A"))
        {
            throw new("Failed to get duration");
        }
        
        if (time.Contains("Duration"))
        {
            time = time.Split("Duration: ")[1].Split(",")[0];
        }

        return time.Trim();
    }
    
    
    public static async Task<bool> Pause(int id)
    {
        if (!FfmpegProcess.TryGetValue(id, out Process? process)) return false;
        
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
