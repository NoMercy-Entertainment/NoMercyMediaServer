using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Encoder.Core;
using NoMercy.EncoderV2.Adapters.Ffmpeg;
using NoMercy.EncoderV2.Tasks;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using VideoFile = NoMercy.Database.Models.VideoFile;

namespace NoMercy.Server;

public static class Dev
{
    public static async Task Run()
    {
        // GenerateFingerprint gd = new("M:\\Music\\E\\Ed Sheeran\\[2023] Autumn Variations\\02 England.flac");
        // await gd.Run(new());
        //
        // Logger.Encoder(gd.Get());
        //
        // string? dur = await GenerateFingerprint.GetStatic("H:\\Marvels\\Download\\Legion.(2017)\\Legion.S01E01.2160p.WEB.H265-GGWP.mkv", new());
        //
        // Logger.Encoder(dur);
        
        
        // // Setup
        // FfmpegAdapterCommandBuilder builder = new(); // your builder that produces ffmpeg args
        //
        // Dictionary<string, dynamic?> preArgs = new()
        // {
        //     ["-hide_banner"] = true,
        //     ["-probesize"] = "4092M",
        //     ["-analyzeduration"] = "9999M",
        //     ["-threads"] = 0,
        //     ["-extra_hw_frames"] = 8, // last value wins (3 then 8)
        //     ["-init_hw_device"] = new[] { "opencl=ocl", "cuda=cu:0" },
        //     ["-filter_hw_device"] = "cu",
        //     ["-hwaccel_output_format"] = "cuda",
        //     ["-progress"] = "-", // ffmpeg progress target
        // };
        //
        // Dictionary<string, dynamic?> ffArgs = new()
        // {
        //     ["-gpu"] = "any",
        //
        //     ["-map_metadata"] = -1,
        //
        //     // filter_complex: audio and sprite items removed (only keep video variants)
        //     ["-filter_complex"] = "[v:0]crop=3840:2160:0:0,scale=1920:-2,format=yuv420p[v1_hls_0];",
        //
        //     ["-map"] = "[v1_hls_0]",
        //
        //     // video codec and metadata
        //     ["-c:v"] = "h264_nvenc",
        //     ["-metadata"] = "title=\"Legion S01E01 Chapter 1 NoMercy\"",
        //
        //     // color / range
        //     ["-color_primaries"] = "bt709",
        //     ["-color_trc"] = "bt709",
        //     ["-colorspace"] = "bt709",
        //     ["-color_range"] = "tv",
        //
        //     // profile / bitrate / level / preset / tune
        //     ["-profile:v"] = "high",
        //     ["-b:v"] = "8695k",
        //     ["-level:v"] = "4.0",
        //     ["-preset"] = "fast",
        //     ["-tune:v"] = "hq",
        //
        //     // format and HLS options
        //     ["-f"] = "hls",
        //     ["-hls_allow_cache"] = 1,
        //     ["-hls_flags"] = "independent_segments",
        //     ["-hls_segment_type"] = "mpegts",
        //     ["-segment_list_type"] = "m3u8",
        //     ["-segment_time_delta"] = 1,
        //     ["-start_number"] = 0,
        //     ["-use_wallclock_as_timestamps"] = 1,
        //     ["-hls_playlist_type"] = "vod",
        //     ["-hls_init_time"] = 4,
        //     ["-hls_time"] = 4,
        //     ["-hls_list_size"] = 0,
        //
        //     ["-hls_segment_filename"] = "./video_1920x1080_SDR/video_1920x1080_SDR_%05d.ts",
        // };
        //
        // // Build args (replace with real builder call)
        // string args = builder.Build(
        //     inputPath: "H:\\Marvels\\Download\\Legion.(2017)\\Legion.S01E01.2160p.WEB.H265-GGWP.mkv",
        //     outputPath: "./video_1920x1080_SDR/video_1920x1080_SDR.m3u8",
        //     preInputOptions: preArgs,
        //     options: ffArgs 
        // );
        //
        // string cwd = Path.Combine(AppFiles.TranscodePath, "test");
        // if (!Directory.Exists(cwd))
        //     Directory.CreateDirectory(cwd);
        //
        // string videodir = Path.Combine(cwd, "video_1920x1080_SDR");
        // if (!Directory.Exists(videodir))
        //     Directory.CreateDirectory(videodir);
        //
        // ExecOptions options = new()
        // {
        //     WorkingDirectory = cwd,
        //     CaptureStdOut = true,
        //     CaptureStdErr = true,
        //     MergeStdErrToOut = false,
        //     CreateNoWindow = true
        // };
        //
        // // Progress parsing state
        // StringBuilder progressBuffer = new();
        // TimeSpan totalDuration = TimeSpan.Zero;
        // bool durationFound = false;
        // Regex durationRegex = new(@"Duration:\s(\d{2}):(\d{2}):(\d{2})\.(\d+)", RegexOptions.Compiled);
        //
        // // stderr callback: extract total duration from ffmpeg stderr lines
        // void StderrCallback(string line)
        // {
        //     if (!durationFound)
        //     {
        //         Match m = durationRegex.Match(line);
        //         if (m.Success)
        //         {
        //             int h = int.Parse(m.Groups[1].Value);
        //             int mm = int.Parse(m.Groups[2].Value);
        //             int s = int.Parse(m.Groups[3].Value);
        //             int ms = int.Parse(m.Groups[4].Value);
        //             totalDuration = new TimeSpan(0, h, mm, s, ms * 10);
        //             durationFound = true;
        //         }
        //     }
        //
        //     // optional: log stderr
        //     // Console.WriteLine($"[ffmpeg:err] {line}");
        // }
        //
        // // stdout callback: accumulate progress blocks and parse when "progress=" appears
        // void StdoutCallback(string line)
        // {
        //     progressBuffer.AppendLine(line);
        //
        //     if (line.StartsWith("progress="))
        //     {
        //         string block = progressBuffer.ToString();
        //         progressBuffer.Clear();
        //     
        //         ProgressData? parsed = ParseProgressBlock(block, totalDuration);
        //         if (parsed != null)
        //         {
        //             Console.WriteLine($"Progress: {parsed.ProgressPercentage:F1}% | time {parsed.CurrentTime}");
        //             // publish to websocket, DB, etc.
        //         }
        //     }
        //     else
        //     {
        //         // optional: log other stdout lines
        //         // Console.WriteLine($"[ffmpeg:out] {line}");
        //     }
        // }
        //
        // // Cancellation token for the run
        // using CancellationTokenSource cts = new();
        //
        // try
        // {
        //     // Start and register the execution so we can control it externally
        //     Task<ExecResult> running;
        //     ExecutorHandle handle = Shell.StartAndRegister(
        //         executable: AppFiles.FfmpegPath,
        //         arguments: args,
        //         out running,
        //         cancellationToken: cts.Token,
        //         stdoutCallback: StdoutCallback,
        //         stderrCallback: StderrCallback,
        //         options: options,
        //         jobId: "job-123");
        //
        //     Console.WriteLine($"Started executor id: {handle.Id} pid: {handle.Pid}");
        //
        //     await Task.Delay(TimeSpan.FromMinutes(1), cts.Token);
        //     // Example: pause and resume after a short delay (platform-dependent)
        //     bool paused = Shell.Pause(handle.Id);
        //     Console.WriteLine(paused ? "Paused executor" : "Pause not supported");
        //
        //     await Task.Delay(TimeSpan.FromMinutes(1), cts.Token);
        //
        //     bool resumed = Shell.Resume(handle.Id);
        //     Console.WriteLine(resumed ? "Resumed executor" : "Resume not supported");
        //     
        //     ExecResult result = await running.ConfigureAwait(false);
        //
        //     // Wait for completion (ExecuteAndRegisterAsync already awaited completion)
        //     Console.WriteLine($"Exit code: {result.ExitCode}");
        //     // Console.WriteLine($"StdErr (trimmed): {result.StandardError}");
        // }
        // catch (AggregateException aex)
        // {
        //     Console.WriteLine("Callback errors occurred:");
        //     foreach (Exception ex in aex.InnerExceptions) Console.WriteLine(ex);
        // }
        // catch (OperationCanceledException)
        // {
        //     Console.WriteLine("Execution cancelled.");
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"Execution failed: {ex}");
        // }

        
        // await using MediaContext context = new();
        //
        // List<Tv> shows = await context.Tvs
        //     // .Where(tv => tv.Library.Type == "tv")
        //     .Include(tv => tv.Episodes)
        //     .ThenInclude(episode => episode.VideoFiles)
        //     .ThenInclude(videoFile => videoFile.Metadata)
        //     // .Where(tv => tv.Id == 67195)
        //     .ToListAsync();
        //     
        // shows.Reverse();
        //     
        // foreach (Tv show in shows)
        // foreach (Episode episode in show.Episodes)
        // {
        //     foreach (VideoFile videoFile in episode.VideoFiles)
        //     {
        //         if (videoFile.Metadata == null) continue;
        //
        //         string hostFolder = videoFile.Metadata.HostFolder;
        //         if (string.IsNullOrEmpty(hostFolder)) continue;
        //
        //         // Logger.App($"Processing Episode: {episode.Title} (S{episode.SeasonNumber}E{episode.EpisodeNumber})");
        //         // Logger.App($"Host Folder: {hostFolder}");
        //         
        //         // DiagnoseMasterFolder(hostFolder);
        //
        //         // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
        //     }
        // }
        //
        // List<Movie> movies = await context.Movies
        //     .Where(tv => tv.Library.Type == "movie")
        //     .Include(episode => episode.VideoFiles)
        //     .ThenInclude(videoFile => videoFile.Metadata)
        //     // .Where(tv => tv.Id == 60808)
        //     .ToListAsync();
        //
        // foreach (Movie movie in movies)
        // {
        //     foreach (VideoFile videoFile in movie.VideoFiles)
        //     {
        //         if (videoFile.Metadata == null) continue;
        //
        //         string hostFolder = videoFile.Metadata.HostFolder;
        //         if (string.IsNullOrEmpty(hostFolder)) continue;
        //
        //         // Logger.App($"Processing Movie: {movie.Title}");
        //         // Logger.App($"Host Folder: {hostFolder}");
        //         
        //         //DiagnoseMasterFolder(hostFolder);
        //
        //         // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
        //     }
        // }
        //
        // await Task.CompletedTask;
    }
    
    
    // Minimal progress block parser used in the example
    static ProgressData? ParseProgressBlock(string block, TimeSpan totalDuration)
    {
        string[] lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Dictionary<string, string> dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string l in lines)
        {
            int idx = l.IndexOf('=');
            if (idx <= 0) continue;
            string k = l[..idx].Trim();
            string v = l[(idx + 1)..].Trim();
            dict[k] = v;
        }

        if (!dict.TryGetValue("out_time", out string? outTime)) return null;
        if (!TimeSpan.TryParse(outTime, out TimeSpan current)) current = TimeSpan.Zero;

        double pct = totalDuration.TotalSeconds > 0
            ? Math.Min(100.0, (current.TotalSeconds / totalDuration.TotalSeconds) * 100.0)
            : 0.0;

        dict.TryGetValue("speed", out string? speedStr);
        double.TryParse(speedStr?.TrimEnd('x'), out double speed);

        dict.TryGetValue("frame", out string? frameStr);
        int.TryParse(frameStr, out int frame);

        dict.TryGetValue("fps", out string? fpsStr);
        double.TryParse(fpsStr, out double fps);

        return new ProgressData
        {
            CurrentTime = current,
            TotalDuration = totalDuration,
            ProgressPercentage = pct,
            Speed = speed,
            Frame = frame,
            Fps = fps
        };
    }

    // Local DTO for parsed progress
    sealed class ProgressData
    {
        public TimeSpan CurrentTime { get; init; }
        public TimeSpan TotalDuration { get; init; }
        public double ProgressPercentage { get; init; }
        public double Speed { get; init; }
        public int Frame { get; init; }
        public double Fps { get; init; }
    }


    private static Dictionary<string, long> CalculateBitratesFromMaster(string episodeFolder)
    {
        Dictionary<string, long> results = new(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(episodeFolder))
            return results;

        IEnumerable<string> m3U8Files = Directory.GetFiles(episodeFolder, "*.m3u8", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                try { return File.ReadAllText(f).Contains("#EXT-X-STREAM-INF"); }
                catch { return false; }
            });

        foreach (string masterPath in m3U8Files)
        {
            string masterDir = Path.GetDirectoryName(masterPath) ?? episodeFolder;
            string[] lines;
            try { lines = File.ReadAllLines(masterPath); }
            catch { continue; }

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Trim().StartsWith("#EXT-X-STREAM-INF", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                // next non-comment line is the variant URI
                string? variantUri = null;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string nxt = lines[j].Trim();
                    if (string.IsNullOrEmpty(nxt) || nxt.StartsWith("#")) continue;
                    variantUri = nxt;
                    break;
                }

                if (variantUri == null) continue;
                if (Uri.IsWellFormedUriString(variantUri, UriKind.Absolute)) continue;

                string variantPath = Path.GetFullPath(Path.Combine(masterDir, variantUri));
                if (!File.Exists(variantPath)) continue;

                long totalBytes = 0L;
                double totalSeconds = 0.0;

                string variantDir = Path.GetDirectoryName(variantPath) ?? masterDir;
                string[] vlines;
                try { vlines = File.ReadAllLines(variantPath); }
                catch { continue; }

                foreach (string raw in vlines)
                {
                    string vline = raw.Trim();
                    if (vline.StartsWith("#EXTINF:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string payload = vline.Substring("#EXTINF:".Length);
                        int comma = payload.IndexOf(',');
                        string durStr = comma >= 0 ? payload.Substring(0, comma) : payload;
                        if (double.TryParse(durStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                            totalSeconds += d;

                        continue;
                    }

                    if (vline.StartsWith("#")) continue;
                    string segRef = vline.Split(new[] { '?', '#' }, 2)[0];
                    if (string.IsNullOrWhiteSpace(segRef)) continue;
                    if (Uri.IsWellFormedUriString(segRef, UriKind.Absolute)) continue;

                    string segPath = Path.GetFullPath(Path.Combine(variantDir, segRef));
                    if (!File.Exists(segPath)) continue;

                    try
                    {
                        FileInfo fi = new(segPath);
                        totalBytes = checked(totalBytes + fi.Length);
                    }
                    catch (OverflowException)
                    {
                        // extremely unlikely; abort this variant
                        results[variantUri] = 0;
                        break;
                    }
                    catch
                    {
                        // ignore IO errors per-segment
                    }
                }

                if (totalBytes > 0 && totalSeconds > 0.0)
                {
                    double bits = totalBytes * 8.0;
                    long bitrate = (long)Math.Round(bits / totalSeconds);
                    results[variantUri] = bitrate;
                    Logger.App($"Computed bitrate for {variantUri}: {bitrate} bps (bytes={totalBytes}, seconds={totalSeconds:F2})");
                }
                else
                {
                    results[variantUri] = 0;
                    Logger.App($"Could not compute bitrate for {variantUri} (bytes={totalBytes}, seconds={totalSeconds:F2})");
                }
            }
        }

        return results;
    }

    // Diagnostic helper you can call locally to print a short report for an episode folder
    private static void DiagnoseMasterFolder(string hostFolder)
    {
        Logger.App($"Diagnosing folder: {hostFolder}");
        if (!Directory.Exists(hostFolder))
        {
            Logger.App("Folder does not exist");
            return;
        }

        Dictionary<string, long> bitrates = CalculateBitratesFromMaster(hostFolder);
        if (bitrates.Count == 0)
        {
            Logger.App("No computed bitrates (no master playlists found or all remote/failed).\n");
            return;
        }

        foreach (KeyValuePair<string, long> kv in bitrates)
        {
            Logger.App($"Variant: {kv.Key} -> Bitrate: {kv.Value} bps");
        }

        // Optionally write a diagnostic file next to the master playlist(s)
        try
        {
            // Instead of writing a diagnostic JSON file, update the master playlists in-place
            IEnumerable<string> masters = Directory.GetFiles(hostFolder, "*.m3u8", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    try { return File.ReadAllText(f).Contains("#EXT-X-STREAM-INF"); }
                    catch { return false; }
                });

            Regex bwRegex = new(@"BANDWIDTH\s*=\s*\d+", RegexOptions.IgnoreCase);

            foreach (string masterPath in masters)
            {
                string original = File.ReadAllText(masterPath);
                string[] lines = original.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                bool changed = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Trim().StartsWith("#EXT-X-STREAM-INF", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    // Find next non-empty, non-comment line for the variant URI
                    string variantUri = null;
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string nxt = lines[j].Trim();
                        if (string.IsNullOrEmpty(nxt) || nxt.StartsWith("#")) continue;
                        variantUri = nxt;
                        break;
                    }

                    if (variantUri == null) continue;
                    if (Uri.IsWellFormedUriString(variantUri, UriKind.Absolute)) continue;

                    if (!bitrates.TryGetValue(variantUri, out long computed)) continue;
                    if (computed <= 0) continue;

                    string tag = lines[i];

                    // Replace or add BANDWIDTH attribute
                    if (bwRegex.IsMatch(tag))
                    {
                        tag = bwRegex.Replace(tag, $"BANDWIDTH={computed}");
                    }
                    else
                    {
                        // Ensure we append after the colon-separated attributes
                        tag += $",BANDWIDTH={computed}";
                    }

                    if (tag != lines[i])
                    {
                        lines[i] = tag;
                        changed = true;
                        Logger.App($"Updated playlist tag in {masterPath}: {variantUri} -> BANDWIDTH={computed}");
                    }
                }

                if (changed)
                {
                    try
                    {
                        File.WriteAllText(masterPath, string.Join(Environment.NewLine, lines));
                        Logger.App($"Wrote updated master playlist: {masterPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.App($"Failed updating master playlist {masterPath}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Failed updating playlists: {ex.Message}");
        }
    }


    private static async Task RecreateMasterPlaylist(string hostFolder, string filename)
    {
        if (string.IsNullOrEmpty(hostFolder)) return;
        if (!Directory.Exists(hostFolder))
        {
            Logger.App($"Host folder does not exist: {hostFolder}");
            return;
        }

        string targetName = Path.GetFileNameWithoutExtension(filename) ?? "master";

        try
        {
            // Find master playlists in the folder (those containing EXT-X-STREAM-INF)
            List<string> masters = Directory.GetFiles(hostFolder, "*.m3u8", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    try { return File.ReadAllText(f).Contains("#EXT-X-STREAM-INF"); }
                    catch { return false; }
                })
                .ToList();

            if (masters.Any())
            {
                string backupDir = Path.Combine(hostFolder, "_m3u8_backup_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(backupDir);
                foreach (string m in masters)
                {
                    try
                    {
                        string dest = Path.Combine(backupDir, Path.GetFileName(m));
                        File.Move(m, dest);
                        Logger.App($"Backed up master playlist {m} -> {dest}");
                    }
                    catch (Exception ex)
                    {
                        Logger.App($"Failed to backup {m}: {ex.Message}");
                    }
                }
            }

            // Build new master using the HLS playlist generator
            await HlsPlaylistGenerator.Build(hostFolder, targetName);

            string newMaster = Path.Combine(hostFolder, targetName + ".m3u8");
            Logger.App($"Recreated master playlist: {newMaster}");
        }
        catch (Exception ex)
        {
            Logger.App($"Failed recreating master playlist in {hostFolder}: {ex.Message}");
        }
    }
}