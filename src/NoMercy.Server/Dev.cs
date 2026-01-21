using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Encoder.Core;
using NoMercy.NmSystem.Information;
using VideoFile = NoMercy.Database.Models.VideoFile;

namespace NoMercy.Server;

public static class Dev
{
    public static async Task Run()
    {
        // string path = "M:\\Anime\\Anime";
        // string[] showFolders = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
        //
        // foreach (string showFolder in showFolders)
        // {
        //     Logger.App($"Processing show folder: {showFolder}");
        //     string[] episodeFolders = Directory.GetDirectories(showFolder, "*", SearchOption.TopDirectoryOnly);
        //     foreach (string folder in episodeFolders)
        //     {
        //         await DeleteEmptyPlaylists(folder);
        //     }
        // }
        
        
        await using MediaContext context = new();

        List<Tv> shows = await context.Tvs
            // .Where(tv => tv.Library.Type == "tv")
            .Include(tv => tv.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .ThenInclude(videoFile => videoFile.Metadata)
            // .Where(tv => tv.Id == 218613)
            .ToListAsync();
            
        foreach (Tv show in shows)
        foreach (Episode episode in show.Episodes)
        {
            foreach (VideoFile videoFile in episode.VideoFiles)
            {
                if (videoFile.Metadata == null) continue;
        
                string hostFolder = videoFile.Metadata.HostFolder;
                if (string.IsNullOrEmpty(hostFolder)) continue;
        
                // Logger.App($"Processing Episode: {episode.Title} (S{episode.SeasonNumber}E{episode.EpisodeNumber})");
                // Logger.App($"Host Folder: {hostFolder}");
                
                // DiagnoseMasterFolder(hostFolder);

                // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
            }
        }
        
        List<Movie> movies = await context.Movies
            .Where(tv => tv.Library.Type == Config.MovieMediaType)
            .Include(episode => episode.VideoFiles)
            .ThenInclude(videoFile => videoFile.Metadata)
            // .Where(tv => tv.Id == 60808)
            .ToListAsync();
        
        foreach (Movie movie in movies)
        {
            foreach (VideoFile videoFile in movie.VideoFiles)
            {
                if (videoFile.Metadata == null) continue;

                string hostFolder = videoFile.Metadata.HostFolder;
                if (string.IsNullOrEmpty(hostFolder)) continue;

                // Logger.App($"Processing Movie: {movie.Title}");
                // Logger.App($"Host Folder: {hostFolder}");
                
                //DiagnoseMasterFolder(hostFolder);

                // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
            }
        }

        await Task.CompletedTask;
    }
    
    
    private static Task DeleteEmptyPlaylists(string episodeFolder)
    {
        if (!Directory.Exists(episodeFolder))
            return Task.CompletedTask;

        IEnumerable<string> m3U8Files = Directory.GetFiles(episodeFolder, "*.m3u8", SearchOption.TopDirectoryOnly);

        foreach (string playlistPath in m3U8Files)
        {
            string[] lines;
            try { lines = File.ReadAllLines(playlistPath); }
            catch { continue; }

            bool hasSegments = lines.Any(line => !line.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(line));
            if (!hasSegments)
            {
                try
                {
                    File.Delete(playlistPath);
                    Logger.App($"Deleted empty playlist: {playlistPath}");
                }
                catch (Exception ex)
                {
                    Logger.App($"Failed to delete empty playlist {playlistPath}: {ex.Message}");
                }
            }
        }

        return Task.CompletedTask;
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