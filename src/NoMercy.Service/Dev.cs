// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Service;

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

        // await using MediaContext context = new();

        // List<Tv> shows = await context
        //     .Tvs
        //     // .Where(tv => tv.Library.Type == "tv")
        //     .Include(tv => tv.Episodes)
        //         .ThenInclude(episode => episode.VideoFiles)
        //             .ThenInclude(videoFile => videoFile.Metadata)
        //     // .Where(tv => tv.Id == 218613)
        //     .ToListAsync();

        // foreach (Tv show in shows)
        // foreach (Episode episode in show.Episodes)
        // {
        //     foreach (VideoFile videoFile in episode.VideoFiles)
        //     {
        //         if (videoFile.Metadata == null)
        //             continue;

        //         string hostFolder = videoFile.Metadata.HostFolder;
        //         if (string.IsNullOrEmpty(hostFolder))
        //             continue;

        // Logger.App($"Processing Episode: {episode.Title} (S{episode.SeasonNumber}E{episode.EpisodeNumber})");
        // Logger.App($"Host Folder: {hostFolder}");

        // DiagnoseMasterFolder(hostFolder);

        // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
        //     }
        // }

        // List<Movie> movies = await context
        //     .Movies.Where(tv => tv.Library.Type == MediaTypes.MovieMediaType)
        //     .Include(episode => episode.VideoFiles)
        //         .ThenInclude(videoFile => videoFile.Metadata)
        //     // .Where(tv => tv.Id == 60808)
        //     .ToListAsync();

        // foreach (Movie movie in movies)
        // {
        //     foreach (VideoFile videoFile in movie.VideoFiles)
        //     {
        //         if (videoFile.Metadata == null)
        //             continue;

        //         string hostFolder = videoFile.Metadata.HostFolder;
        //         if (string.IsNullOrEmpty(hostFolder))
        //             continue;

        // Logger.App($"Processing Movie: {movie.Title}");
        // Logger.App($"Host Folder: {hostFolder}");

        //DiagnoseMasterFolder(hostFolder);

        // await RecreateMasterPlaylist(hostFolder, videoFile.Filename);
        //     }
        // }

        await Task.CompletedTask;
    }

    private static IStorage CreateStorage()
    {
        IStorageDriver driver = new LocalStorageDriver();
        return new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));
    }

    private static Task DeleteEmptyPlaylists(string episodeFolder)
    {
        IStorage storage = CreateStorage();

        if (!storage.Exists(path: episodeFolder))
            return Task.CompletedTask;

        IEnumerable<StorageEntry> entries = storage.List(path: episodeFolder, pattern: "*.m3u8", recursive: false);
        IEnumerable<string> m3U8Files = entries.Select(selector: e => e.Path);

        foreach (string playlistPath in m3U8Files)
        {
            string[] lines;
            try
            {
                lines = storage
                    .ReadAllTextAsync(path: playlistPath, ct: CancellationToken.None)
                    .Result.Split(separator: ["\r\n", "\n"], options: StringSplitOptions.None);
            }
            catch
            {
                continue;
            }

            bool hasSegments = lines.Any(predicate: line =>
                !line.Trim().StartsWith(value: "#") && !string.IsNullOrWhiteSpace(value: line)
            );
            if (!hasSegments)
            {
                try
                {
                    storage.Delete(path: playlistPath);
                    Logger.App(message: $"Deleted empty playlist: {playlistPath}");
                }
                catch (Exception ex)
                {
                    Logger.App(message: $"Failed to delete empty playlist {playlistPath}: {ex.Message}");
                }
            }
        }

        return Task.CompletedTask;
    }

    private static Dictionary<string, long> CalculateBitratesFromMaster(string episodeFolder)
    {
        Dictionary<string, long> results = new(comparer: StringComparer.OrdinalIgnoreCase);
        IStorage storage = CreateStorage();

        if (!storage.Exists(path: episodeFolder))
            return results;

        IEnumerable<string> m3U8Files = storage
            .List(path: episodeFolder, pattern: "*.m3u8", recursive: false)
            .Select(selector: e => e.Path)
            .Where(predicate: f =>
            {
                try
                {
                    return storage
                        .ReadAllTextAsync(path: f, ct: CancellationToken.None)
                        .Result.Contains(value: "#EXT-X-STREAM-INF");
                }
                catch
                {
                    return false;
                }
            });

        foreach (string masterPath in m3U8Files)
        {
            string masterDir = Path.GetDirectoryName(path: masterPath) ?? episodeFolder;
            string[] lines;
            try
            {
                lines = storage
                    .ReadAllTextAsync(path: masterPath, ct: CancellationToken.None)
                    .Result.Split(separator: ["\r\n", "\n"], options: StringSplitOptions.None);
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (
                    !lines[i]
                        .Trim()
                        .StartsWith(
                            value: "#EXT-X-STREAM-INF",
                            comparisonType: StringComparison.InvariantCultureIgnoreCase
                        )
                )
                    continue;

                // next non-comment line is the variant URI
                string? variantUri = null;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string nxt = lines[j].Trim();
                    if (string.IsNullOrEmpty(value: nxt) || nxt.StartsWith(value: "#"))
                        continue;
                    variantUri = nxt;
                    break;
                }

                if (variantUri == null)
                    continue;
                if (Uri.IsWellFormedUriString(uriString: variantUri, uriKind: UriKind.Absolute))
                    continue;

                string variantPath = Path.GetFullPath(path: Path.Combine(path1: masterDir, path2: variantUri));
                if (!storage.Exists(path: variantPath))
                    continue;

                long totalBytes = 0L;
                double totalSeconds = 0.0;

                string variantDir = Path.GetDirectoryName(path: variantPath) ?? masterDir;
                string[] vlines;
                try
                {
                    vlines = storage
                        .ReadAllTextAsync(path: variantPath, ct: CancellationToken.None)
                        .Result.Split(separator: ["\r\n", "\n"], options: StringSplitOptions.None);
                }
                catch
                {
                    continue;
                }

                foreach (string raw in vlines)
                {
                    string vline = raw.Trim();
                    if (vline.StartsWith(value: "#EXTINF:", comparisonType: StringComparison.InvariantCultureIgnoreCase))
                    {
                        string payload = vline.Substring(startIndex: "#EXTINF:".Length);
                        int comma = payload.IndexOf(value: ',');
                        string durStr = comma >= 0 ? payload.Substring(startIndex: 0, length: comma) : payload;
                        if (
                            double.TryParse(
                                s: durStr,
                                style: NumberStyles.Any,
                                provider: CultureInfo.InvariantCulture,
                                result: out double d
                            )
                        )
                            totalSeconds += d;

                        continue;
                    }

                    if (vline.StartsWith(value: "#"))
                        continue;
                    string segRef = vline.Split(separator: new[] { '?', '#' }, count: 2)[0];
                    if (string.IsNullOrWhiteSpace(value: segRef))
                        continue;
                    if (Uri.IsWellFormedUriString(uriString: segRef, uriKind: UriKind.Absolute))
                        continue;

                    string segPath = Path.GetFullPath(path: Path.Combine(path1: variantDir, path2: segRef));
                    if (!storage.Exists(path: segPath))
                        continue;

                    try
                    {
                        long segSize = storage.Size(path: segPath);
                        totalBytes = checked(totalBytes + segSize);
                    }
                    catch (OverflowException)
                    {
                        // extremely unlikely; abort this variant
                        results[key: variantUri] = 0;
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
                    long bitrate = (long)Math.Round(a: bits / totalSeconds);
                    results[key: variantUri] = bitrate;
                    Logger.App(
                        message: $"Computed bitrate for {variantUri}: {bitrate} bps (bytes={totalBytes}, seconds={totalSeconds:F2})"
                    );
                }
                else
                {
                    results[key: variantUri] = 0;
                    Logger.App(
                        message: $"Could not compute bitrate for {variantUri} (bytes={totalBytes}, seconds={totalSeconds:F2})"
                    );
                }
            }
        }

        return results;
    }

    // Diagnostic helper you can call locally to print a short report for an episode folder
    private static void DiagnoseMasterFolder(string hostFolder)
    {
        IStorage storage = CreateStorage();

        Logger.App(message: $"Diagnosing folder: {hostFolder}");
        if (!storage.Exists(path: hostFolder))
        {
            Logger.App(message: "Folder does not exist");
            return;
        }

        Dictionary<string, long> bitrates = CalculateBitratesFromMaster(episodeFolder: hostFolder);
        if (bitrates.Count == 0)
        {
            Logger.App(message: "No computed bitrates (no master playlists found or all remote/failed).\n");
            return;
        }

        foreach (KeyValuePair<string, long> kv in bitrates)
        {
            Logger.App(message: $"Variant: {kv.Key} -> Bitrate: {kv.Value} bps");
        }

        // Optionally write a diagnostic file next to the master playlist(s)
        try
        {
            // Instead of writing a diagnostic JSON file, update the master playlists in-place
            IEnumerable<string> masters = storage
                .List(path: hostFolder, pattern: "*.m3u8", recursive: false)
                .Select(selector: e => e.Path)
                .Where(predicate: f =>
                {
                    try
                    {
                        return storage
                            .ReadAllTextAsync(path: f, ct: CancellationToken.None)
                            .Result.Contains(value: "#EXT-X-STREAM-INF");
                    }
                    catch
                    {
                        return false;
                    }
                });

            Regex bwRegex = new(pattern: @"BANDWIDTH\s*=\s*\d+", options: RegexOptions.IgnoreCase);

            foreach (string masterPath in masters)
            {
                string original = storage
                    .ReadAllTextAsync(path: masterPath, ct: CancellationToken.None)
                    .Result;
                string[] lines = original.Split(separator: ["\r\n", "\n"], options: StringSplitOptions.None);
                bool changed = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (
                        !lines[i]
                            .Trim()
                            .StartsWith(
                                value: "#EXT-X-STREAM-INF",
                                comparisonType: StringComparison.InvariantCultureIgnoreCase
                            )
                    )
                        continue;

                    // Find next non-empty, non-comment line for the variant URI
                    string? variantUri = null;
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string nxt = lines[j].Trim();
                        if (string.IsNullOrEmpty(value: nxt) || nxt.StartsWith(value: "#"))
                            continue;
                        variantUri = nxt;
                        break;
                    }

                    if (variantUri == null)
                        continue;
                    if (Uri.IsWellFormedUriString(uriString: variantUri, uriKind: UriKind.Absolute))
                        continue;

                    if (!bitrates.TryGetValue(key: variantUri, value: out long computed))
                        continue;
                    if (computed <= 0)
                        continue;

                    string tag = lines[i];

                    // Replace or add BANDWIDTH attribute
                    if (bwRegex.IsMatch(input: tag))
                    {
                        tag = bwRegex.Replace(input: tag, replacement: $"BANDWIDTH={computed}");
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
                        Logger.App(
                            message: $"Updated playlist tag in {masterPath}: {variantUri} -> BANDWIDTH={computed}"
                        );
                    }
                }

                if (changed)
                {
                    try
                    {
                        storage
                            .WriteAllTextAsync(
                                path: masterPath,
                                contents: string.Join(separator: Environment.NewLine, value: lines),
                                ct: CancellationToken.None
                            )
                            .GetAwaiter()
                            .GetResult();
                        Logger.App(message: $"Wrote updated master playlist: {masterPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.App(message: $"Failed updating master playlist {masterPath}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed updating playlists: {ex.Message}");
        }
    }

    private static async Task RecreateMasterPlaylist(string hostFolder, string filename)
    {
        if (string.IsNullOrEmpty(value: hostFolder))
            return;

        IStorage storage = CreateStorage();

        if (!storage.Exists(path: hostFolder))
        {
            Logger.App(message: $"Host folder does not exist: {hostFolder}");
            return;
        }

        string targetName = Path.GetFileNameWithoutExtension(path: filename) ?? "master";

        try
        {
            // Find master playlists in the folder (those containing EXT-X-STREAM-INF)
            List<string> masters = storage
                .List(path: hostFolder, pattern: "*.m3u8", recursive: false)
                .Select(selector: e => e.Path)
                .Where(predicate: f =>
                {
                    try
                    {
                        return storage
                            .ReadAllTextAsync(path: f, ct: CancellationToken.None)
                            .Result.Contains(value: "#EXT-X-STREAM-INF");
                    }
                    catch
                    {
                        return false;
                    }
                })
                .ToList();

            if (masters.Any())
            {
                string backupDir = Path.Combine(
                    path1: hostFolder,
                    path2: "_m3u8_backup_" + DateTime.UtcNow.ToString(format: "yyyyMMddHHmmss")
                );
                storage.CreateDirectory(path: backupDir);
                foreach (string m in masters)
                {
                    try
                    {
                        string dest = Path.Combine(path1: backupDir, path2: Path.GetFileName(path: m));
                        storage.Move(from: m, to: dest);
                        Logger.App(message: $"Backed up master playlist {m} -> {dest}");
                    }
                    catch (Exception ex)
                    {
                        Logger.App(message: $"Failed to backup {m}: {ex.Message}");
                    }
                }
            }

            // Build master playlist by scanning variant playlists in subdirectories
            StringBuilder masterBuilder = new();
            masterBuilder.AppendLine(value: "#EXTM3U");

            // Video variants
            foreach (
                string dir in storage
                    .List(path: hostFolder, pattern: "video_*", recursive: false)
                    .Where(predicate: e => e.IsDirectory)
                    .Select(selector: e => e.Path)
                    .OrderByDescending(keySelector: d => d)
            )
            {
                IReadOnlyList<StorageEntry> dirPlaylists = storage.List(path: dir, pattern: "*.m3u8", recursive: false);
                if (dirPlaylists.Count == 0)
                    continue;

                string dirName = Path.GetFileName(path: dir);
                string relativePath = Path.Combine(path1: dirName, path2: Path.GetFileName(path: dirPlaylists[index: 0].Path))
                    .Replace(oldValue: "\\", newValue: "/");

                // Parse resolution from directory name (video_1920x1080)
                Match resMatch = Regex.Match(input: dirName, pattern: @"video_(\d+)x(\d+)");
                if (resMatch.Success)
                {
                    string width = resMatch.Groups[groupnum: 1].Value;
                    string height = resMatch.Groups[groupnum: 2].Value;
                    masterBuilder.AppendLine(
                        handler: $"#EXT-X-STREAM-INF:BANDWIDTH=8000000,RESOLUTION={width}x{height}"
                    );
                    masterBuilder.AppendLine(value: relativePath);
                }
            }

            // Audio variants
            foreach (
                string dir in storage
                    .List(path: hostFolder, pattern: "audio_*", recursive: false)
                    .Where(predicate: e => e.IsDirectory)
                    .Select(selector: e => e.Path)
            )
            {
                IReadOnlyList<StorageEntry> dirPlaylists = storage.List(path: dir, pattern: "*.m3u8", recursive: false);
                if (dirPlaylists.Count == 0)
                    continue;

                string dirName = Path.GetFileName(path: dir);
                string relativePath = Path.Combine(path1: dirName, path2: Path.GetFileName(path: dirPlaylists[index: 0].Path))
                    .Replace(oldValue: "\\", newValue: "/");
                masterBuilder.AppendLine(
                    handler: $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio\",NAME=\"{dirName}\",URI=\"{relativePath}\""
                );
            }

            string newMaster = Path.Combine(path1: hostFolder, path2: targetName + ".m3u8");
            await storage.WriteAllTextAsync(
                path: newMaster,
                contents: masterBuilder.ToString(),
                ct: CancellationToken.None
            );
            Logger.App(message: $"Recreated master playlist: {newMaster}");
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Failed recreating master playlist in {hostFolder}: {ex.Message}");
        }
    }
}
