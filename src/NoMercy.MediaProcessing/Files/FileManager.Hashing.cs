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
using System.Text.RegularExpressions;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Subtitles;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager
{
    private List<IVideo> GetVideoHashList(IStorage storage, string hostFolder)
    {
        List<IVideo> videos = [];

        if (!storage.Exists(path: hostFolder))
            return videos;

        // V3 encoder creates directories like video_1920x1080/ with .m3u8 playlist inside
        foreach (
            StorageEntry dir in storage
                .List(path: hostFolder, pattern: null, recursive: false)
                .Where(predicate: e => e.IsDirectory && storage.GetName(path: e.Path).StartsWith(value: "video_"))
        )
        {
            string dirName = storage.GetName(path: dir.Path);
            Match match = VideoDirectoryRegex().Match(input: dirName);
            if (!match.Success)
                continue;

            int width = int.Parse(s: match.Groups[groupname: "width"].Value);
            int height = int.Parse(s: match.Groups[groupname: "height"].Value);

            // One directory listing serves both needs: locating the playlist and
            // summing the segment sizes. Over NFS each listing enumerates every
            // segment in the quality dir, so a second List here doubled the scan
            // cost per file for no new information.
            IReadOnlyList<StorageEntry> dirEntries = storage.List(path: dir.Path, pattern: null, recursive: false);

            StorageEntry? playlist = dirEntries.FirstOrDefault(predicate: e =>
                !e.IsDirectory
                && storage.GetName(path: e.Path).EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
            );
            if (playlist is null)
                continue;

            string playlistPath = playlist.Path;
            long dirSize = dirEntries.Where(predicate: e => !e.IsDirectory).Sum(selector: e => e.SizeBytes);

            videos.Add(
                item: new()
                {
                    Width = width,
                    Height = height,
                    FileName = $"/{storage.GetName(path: dir.Path)}/{storage.GetName(path: playlistPath)}",
                    FileHash = ComputeFileHash(storage: storage, filePath: playlistPath),
                    FileSize = dirSize,
                }
            );
        }

        return videos;
    }

    private List<IAudio> GetAudioHashList(IStorage storage, string hostFolder)
    {
        List<IAudio> audioList = [];

        if (!storage.Exists(path: hostFolder))
            return audioList;

        // Encoder creates directories like audio_eng_aac/ with .m3u8 playlist inside
        foreach (
            StorageEntry dir in storage
                .List(path: hostFolder, pattern: null, recursive: false)
                .Where(predicate: e => e.IsDirectory && storage.GetName(path: e.Path).StartsWith(value: "audio_"))
        )
        {
            string dirName = storage.GetName(path: dir.Path);
            Match match = AudioDirectoryRegex().Match(input: dirName);
            if (!match.Success)
                continue;

            string language = match.Groups[groupname: "lang"].Value;
            // Old-naming dirs (`audio_jpn`) carry no codec token — default to aac,
            // the group the master's video stream references (AUDIO="audio_aac").
            string codec = match.Groups[groupname: "codec"].Success ? match.Groups[groupname: "codec"].Value : "aac";

            // Single listing for playlist lookup and size sum — see GetVideoHashList.
            IReadOnlyList<StorageEntry> dirEntries = storage.List(path: dir.Path, pattern: null, recursive: false);

            StorageEntry? playlist = dirEntries.FirstOrDefault(predicate: e =>
                !e.IsDirectory
                && storage.GetName(path: e.Path).EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
            );
            if (playlist is null)
                continue;

            string playlistPath = playlist.Path;
            long dirSize = dirEntries.Where(predicate: e => !e.IsDirectory).Sum(selector: e => e.SizeBytes);

            audioList.Add(
                item: new()
                {
                    Language = language,
                    Codec = codec,
                    FileName = $"/{storage.GetName(path: dir.Path)}/{storage.GetName(path: playlistPath)}",
                    FileHash = ComputeFileHash(storage: storage, filePath: playlistPath),
                    FileSize = dirSize,
                }
            );
        }

        return audioList;
    }

    private List<ISubtitle> GetSubtitleHashList(IStorage storage, string hostFolder)
    {
        List<ISubtitle> subtitles = [];

        string subtitleFolder = storage.CombinePath(parent: hostFolder, child: "subtitles");

        if (!storage.Exists(path: subtitleFolder))
            return subtitles;

        IReadOnlyList<StorageEntry> subtitleFiles = storage.List(
            path: subtitleFolder,
            pattern: null,
            recursive: false
        );
        foreach (StorageEntry subtitleEntry in subtitleFiles.Where(predicate: e => !e.IsDirectory))
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(input: subtitleEntry.Path);

            if (!match.Success)
                continue;

            string path = subtitleEntry.Path;

            // Reject binary subtitle formats we can't stream as HLS sidecars; accept
            // every text format (vtt, ass, srt, ssa, sub, webvtt). The bitmap
            // track's OCR sidecar carries the same {lang}.{type} and is hashed in
            // its place, so the track is still represented here.
            string ext = match.Groups[groupname: "ext"].Value;
            if (SubtitleClassifier.IsBitmapSidecarExtension(extension: ext))
                continue;

            subtitles.Add(
                item: new()
                {
                    Language = match.Groups[groupname: "lang"].Value,
                    Type = match.Groups[groupname: "type"].Value,
                    // A client-facing sidecar URL, not a local disk path — must
                    // always join with '/' regardless of host OS. storage.CombinePath
                    // deliberately returns the driver-NATIVE separator (backslash for
                    // LocalStorageDriver on Windows, per IStorageDriver.DirectorySeparator),
                    // which is correct for on-disk I/O but produced a mixed "/subtitles\file"
                    // FileName on a Windows-hosted install — a malformed URL the player
                    // would fail to resolve.
                    FileName = $"/subtitles/{storage.GetName(path: path)}",
                    FileHash = ComputeFileHash(storage: storage, filePath: path),
                    FileSize = subtitleEntry.SizeBytes,
                    Codec = ext,
                }
            );
        }

        return subtitles;
    }

    private List<IPreview> GetPreviewHashList(
        IStorage storage,
        string hostFolder,
        List<VideoTrack> extraFiles
    )
    {
        IEnumerable<IPreview> sprites = extraFiles
            .Where(predicate: file => file.Kind == "sprite")
            .Select(selector: file =>
            {
                string spritePath = storage.CombinePath(
                    parent: hostFolder,
                    child: Path.GetFileName(path: file.File).OrEmpty()
                );
                return new IPreview
                {
                    ImageFileName =
                        "/" + (Path.GetFileName(path: file.File).OrEmpty()).Replace(oldValue: "\\", newValue: "/"),
                    ImageFileSize = storage.SizeOrZero(path: spritePath),
                    ImageFileHash = ComputeFileHash(storage: storage, filePath: spritePath),
                };
            });

        IEnumerable<IPreview> times = extraFiles
            .Where(predicate: file => file.Kind == "thumbnails")
            .Select(selector: file =>
            {
                string vttPath = storage.CombinePath(
                    parent: hostFolder,
                    child: Path.GetFileName(path: file.File).OrEmpty()
                );
                // Read + parse the VTT once — it was previously read twice, once
                // per dimension, doubling the NFS round-trips for the thumbnail track.
                (int Width, int Height) dimensions = GetImageDimensionsFromVtt(storage: storage, filePath: vttPath);
                return new IPreview
                {
                    Width = dimensions.Width,
                    Height = dimensions.Height,
                    TimeFileName = "/" + (Path.GetFileName(path: file.File).OrEmpty()).Replace(oldValue: "\\", newValue: "/"),
                    TimeFileSize = storage.SizeOrZero(path: vttPath),
                    TimeFileHash = ComputeFileHash(storage: storage, filePath: vttPath),
                };
            });

        List<IPreview> previews = sprites
            .Zip(
                second: times,
                resultSelector: (sprite, time) =>
                    new IPreview
                    {
                        Width = time.Width,
                        Height = time.Height,
                        ImageFileName = sprite.ImageFileName,
                        ImageFileSize = sprite.ImageFileSize,
                        ImageFileHash = sprite.ImageFileHash,
                        TimeFileName = time.TimeFileName,
                        TimeFileSize = time.TimeFileSize,
                        TimeFileHash = time.TimeFileHash,
                    }
            )
            .ToList();
        return previews;
    }

    private List<IFont> GetFontHashList(IStorage storage, string hostFolder)
    {
        string fontFolder = storage.CombinePath(parent: hostFolder, child: "fonts");

        List<IFont> fonts = [];

        if (!storage.Exists(path: fontFolder))
            return fonts;

        IReadOnlyList<StorageEntry> fontFiles = storage.List(path: fontFolder, pattern: null, recursive: false);
        foreach (StorageEntry fontEntry in fontFiles.Where(predicate: e => !e.IsDirectory))
        {
            string path = fontEntry.Path;
            fonts.Add(
                item: new()
                {
                    // See the matching comment in GetSubtitleHashList: a client-facing
                    // sidecar URL must join with '/' regardless of host OS, never the
                    // driver-native separator storage.CombinePath deliberately returns.
                    FileName = $"/fonts/{storage.GetName(path: path)}",
                    FileHash = ComputeFileHash(storage: storage, filePath: path),
                    FileSize = fontEntry.SizeBytes,
                }
            );
        }

        return fonts;
    }

    private async Task<List<IChapter>> GetChapterHashListAsync(
        IStorage storage,
        string hostFolder,
        string file
    )
    {
        string path = storage.CombinePath(parent: hostFolder, child: file);

        List<IChapter> chapters = [];

        List<IChapter>? parsedChapters = await ParseChaptersAsync(storage: storage, chapterFile: path);

        foreach (IChapter parsedChapter in parsedChapters ?? [])
        {
            chapters.Add(
                item: new()
                {
                    EndTime = parsedChapter.EndTime,
                    StartTime = parsedChapter.StartTime,
                    Title = parsedChapter.Title,
                    Id = parsedChapter.Id,
                }
            );
        }

        return chapters;
    }

    private static async Task MoveFolderAsync(
        string sourceFolder,
        string destinationFolder,
        IStorage sourceStorage,
        IStorage destinationStorage
    )
    {
        if (!sourceStorage.Exists(path: sourceFolder))
            throw new DirectoryNotFoundException(message: $"Source folder not found: {sourceFolder}");

        bool sameBackend =
            ReferenceEquals(objA: sourceStorage, objB: destinationStorage)
            || sourceStorage.Driver.GetType() == destinationStorage.Driver.GetType();

        if (sameBackend)
        {
            sourceStorage.MoveDirectory(from: sourceFolder, to: destinationFolder);
            Logger.App(message: $"Moved {sourceFolder} to {destinationFolder}");
            return;
        }

        IReadOnlyList<StorageEntry> entries = sourceStorage.List(
            path: sourceFolder,
            pattern: null,
            recursive: true
        );

        foreach (StorageEntry entry in entries)
        {
            if (entry.IsDirectory)
                continue;

            string relativePath = entry.Path.StartsWith(value: sourceFolder, comparisonType: StringComparison.Ordinal)
                ? entry.Path[sourceFolder.Length..].TrimStart(trimChars: ['/', '\\'])
                : entry.Path;

            string destPath = string.Join(
                separator: '/', value: [destinationFolder.TrimEnd(trimChar: '/'), relativePath.Replace(oldChar: '\\', newChar: '/')]
            );

            string? parentDir = destinationStorage.GetParent(path: destPath);
            if (!string.IsNullOrEmpty(value: parentDir))
                destinationStorage.CreateDirectory(path: parentDir);

            await using Stream readStream = sourceStorage.OpenRead(path: entry.Path);
            await using Stream writeStream = destinationStorage.OpenWrite(
                path: destPath,
                overwrite: true
            );
            await readStream.CopyToAsync(destination: writeStream);
        }

        sourceStorage.DeleteDirectory(path: sourceFolder, recursive: true);

        Logger.App(message: $"Cross-backend move: {sourceFolder} to {destinationFolder}");
    }

    private static (int Width, int Height) GetImageDimensions(string filePath)
    {
        ImageInfo info = Image.Identify(path: filePath);

        return (info.Width, info.Height);
    }

    private static (int Width, int Height) GetImageDimensionsFromVtt(
        IStorage storage,
        string filePath
    )
    {
        string vttContents = storage
            .ReadAllTextAsync(path: filePath, ct: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Regex regex = ImageDimensions();
        Match match = regex.Match(input: vttContents);

        if (match.Success)
        {
            int width = int.Parse(s: match.Groups[groupname: "width"].Value);
            int height = int.Parse(s: match.Groups[groupname: "height"].Value);
            return (width, height);
        }

        return (0, 0);
    }

    private static async Task<List<IChapter>?> ParseChaptersAsync(
        IStorage storage,
        string chapterFile
    )
    {
        await using Stream fileStream = storage.OpenRead(path: chapterFile);
        using StreamReader reader = new(stream: fileStream);
        string text = await reader.ReadToEndAsync();

        List<IChapter> chapters = ParseChaptersVtt(text: text);
        return chapters.Count == 0 ? null : chapters;
    }

    // Chapter files are WEBVTT (see ChapterWriter): a WEBVTT header, then
    // blank-line-separated cue blocks of an optional id line, a
    // `HH:MM:SS.mmm --> HH:MM:SS.mmm` timing line, and a title payload. The
    // general subtitle parser cannot read this shape and degrades to one
    // garbage entry per line, so chapters are parsed here directly.
    internal static List<IChapter> ParseChaptersVtt(string text)
    {
        List<IChapter> chapters = [];
        if (string.IsNullOrWhiteSpace(value: text))
            return chapters;

        string normalized = text.Replace(oldValue: "\r\n", newValue: "\n").Replace(oldValue: "\r", newValue: "\n");
        string[] blocks = normalized.Split(separator: ["\n\n"], options: StringSplitOptions.None);

        int index = 0;
        foreach (string rawBlock in blocks)
        {
            string block = rawBlock.Trim();
            if (block.Length == 0 || block == "WEBVTT" || block.StartsWith(value: "WEBVTT"))
                continue;
            if (block.StartsWith(value: "NOTE") || block.StartsWith(value: "STYLE") || block.StartsWith(value: "REGION"))
                continue;

            string[] lines = block.Split(separator: '\n');

            int timingLineIndex = -1;
            Match? timing = null;
            for (int lineIndex = 0; lineIndex < Math.Min(val1: 2, val2: lines.Length); lineIndex++)
            {
                Match candidate = ChapterTimingRegex().Match(input: lines[lineIndex].Trim());
                if (candidate.Success)
                {
                    timingLineIndex = lineIndex;
                    timing = candidate;
                    break;
                }
            }

            if (timing is null)
                continue;

            int start = ParseVttTimestampMs(timestamp: timing.Groups[groupnum: 1].Value);
            int end = ParseVttTimestampMs(timestamp: timing.Groups[groupnum: 2].Value);
            if (start < 0 || end < 0)
                continue;

            string title =
                lines.Length > timingLineIndex + 1
                    ? lines[timingLineIndex + 1].Trim()
                    : string.Empty;

            chapters.Add(
                item: new()
                {
                    Id = index++,
                    StartTime = start,
                    EndTime = end,
                    Title = title,
                }
            );
        }

        return NormalizeChapters(chapters: chapters);
    }

    // A chapter list should span the whole item from 0. Some sources mark only
    // a late chapter (e.g. a lone "Credits" cue starting at 29:58), which leaves
    // the player with a single marker floating near the end and no way to seek
    // the opening via chapters. When the first parsed chapter starts after 0,
    // prepend an opening chapter covering 0 -> firstStart so the timeline is
    // complete and every chapter set is usable. Ids are re-sequenced from 0.
    private static List<IChapter> NormalizeChapters(List<IChapter> chapters)
    {
        if (chapters.Count == 0)
            return chapters;

        if (chapters[index: 0].StartTime > 0)
            chapters.Insert(
                index: 0,
                item: new()
                {
                    StartTime = 0,
                    EndTime = chapters[index: 0].StartTime,
                    Title = "Start",
                }
            );

        for (int i = 0; i < chapters.Count; i++)
            chapters[index: i].Id = i;

        return chapters;
    }

    // Parse a `HH:MM:SS.mmm` (or `MM:SS.mmm`) WEBVTT timestamp into whole
    // milliseconds. Returns -1 for unparseable input so the caller can skip it.
    private static int ParseVttTimestampMs(string timestamp)
    {
        string[] parts = timestamp.Split(separator: ':');
        double hours = 0;
        double minutes;
        double seconds;

        if (parts.Length == 3)
        {
            if (
                !double.TryParse(
                    s: parts[0],
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out hours
                )
                || !double.TryParse(
                    s: parts[1],
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out minutes
                )
                || !double.TryParse(
                    s: parts[2],
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out seconds
                )
            )
                return -1;
        }
        else if (parts.Length == 2)
        {
            if (
                !double.TryParse(
                    s: parts[0],
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out minutes
                )
                || !double.TryParse(
                    s: parts[1],
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out seconds
                )
            )
                return -1;
        }
        else
        {
            return -1;
        }

        double totalSeconds = hours * 3600 + minutes * 60 + seconds;
        return (int)Math.Round(a: totalSeconds * 1000);
    }

    [GeneratedRegex(
        pattern: @"^((?:\d{1,3}:)?\d{2}:\d{2}(?:\.\d{1,3})?)\s+-->\s+((?:\d{1,3}:)?\d{2}:\d{2}(?:\.\d{1,3})?)"
    )]
    private static partial Regex ChapterTimingRegex();

    private static List<VideoTrack> GetExtraFiles(IStorage storage, string hostFolder)
    {
        List<VideoTrack> tracks = [];

        IReadOnlyList<StorageEntry> files = storage.List(path: hostFolder, pattern: null, recursive: false);

        // Index every thumb/sprite candidate first so we can pair a VTT with
        // a same-stem WEBP. Stale VTT files from a previous re-encode (e.g.
        // thumbs_320x178.vtt) used to be registered alongside the live sprite
        // (thumbs_320x180.webp), and the player followed the VTT's cues to a
        // non-existent webp — 404 every hover.
        Dictionary<string, string> spriteByStem = new(comparer: StringComparer.OrdinalIgnoreCase);
        List<(string Name, string Stem)> vttCandidates = [];

        foreach (StorageEntry entry in files.Where(predicate: e => !e.IsDirectory))
        {
            string name = storage.GetName(path: entry.Path);
            string stem = storage.GetNameWithoutExtension(path: entry.Path);

            if (name.StartsWith(value: "chapter"))
                tracks.Add(item: new() { File = "/" + name, Kind = "chapters" });
            else if (name.StartsWith(value: "skipper"))
                tracks.Add(item: new() { File = "/" + name, Kind = "skippers" });
            else if (
                (
                    name.StartsWith(value: "sprite")
                    || name.StartsWith(value: "preview")
                    || name.StartsWith(value: "thumb")
                ) && entry.Path.EndsWith(value: "vtt")
            )
                vttCandidates.Add(item: (name, stem));
            else if (
                (name.StartsWith(value: "sprite") || name.StartsWith(value: "thumb"))
                && entry.Path.EndsWith(value: "webp")
            )
            {
                spriteByStem[key: stem] = name;
                tracks.Add(item: new() { File = "/" + name, Kind = "sprite" });
            }
            else if (name.StartsWith(value: "fonts"))
                tracks.Add(item: new() { File = "/" + name, Kind = "fonts" });
        }

        // Only register VTTs whose basename has a matching sprite WEBP on
        // disk. Drops stale VTTs left behind when the sprite was re-rendered
        // at a different dimension and the old VTT wasn't cleaned up.
        foreach ((string name, string stem) in vttCandidates)
        {
            if (spriteByStem.ContainsKey(key: stem))
                tracks.Add(item: new() { File = "/" + name, Kind = "thumbnails" });
        }

        return tracks;
    }

    private static List<Subtitle> GetSubtitles(IStorage storage, string hostFolder)
    {
        string subtitleFolder = storage.CombinePath(parent: hostFolder, child: "subtitles");

        List<Subtitle> subtitles = [];

        if (!storage.Exists(path: subtitleFolder))
            return subtitles;

        IReadOnlyList<StorageEntry> subtitleFiles = storage.List(
            path: subtitleFolder,
            pattern: null,
            recursive: false
        );

        // First pass: index every .vtt by {lang}|{type} so we can spot bitmap subs
        // (.sup / .vob) whose OCR pass left no companion .vtt behind. Without this
        // an orphaned bitmap silently disappears from the API track list and the
        // operator has no signal the OCR failed.
        HashSet<string> vttKeys = new(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (StorageEntry subtitleEntry in subtitleFiles.Where(predicate: e => !e.IsDirectory))
        {
            Match vttMatch = SubtitleFileRegex().Match(input: subtitleEntry.Path);
            if (vttMatch.Success && vttMatch.Groups[groupname: "ext"].Value == "vtt")
                vttKeys.Add(item: $"{vttMatch.Groups[groupname: "lang"].Value}|{vttMatch.Groups[groupname: "type"].Value}");
        }

        foreach (StorageEntry subtitleEntry in subtitleFiles.Where(predicate: e => !e.IsDirectory))
        {
            Regex regex = SubtitleFileRegex();
            Match match = regex.Match(input: subtitleEntry.Path);

            if (!match.Success)
                continue;

            // Reject binary subtitle formats; accept every text format.
            string ext = match.Groups[groupname: "ext"].Value;
            if (SubtitleClassifier.IsBitmapSidecarExtension(extension: ext))
            {
                string siblingKey = $"{match.Groups[groupname: "lang"].Value}|{match.Groups[groupname: "type"].Value}";
                if (!vttKeys.Contains(item: siblingKey))
                    Logger.App(
                        message: $"Orphaned bitmap subtitle (no sibling .vtt): {Path.GetFileName(path: subtitleEntry.Path)} — OCR likely failed or never ran"
                    );
                continue;
            }

            subtitles.Add(
                item: new()
                {
                    Language = match.Groups[groupname: "lang"].Value,
                    Type = match.Groups[groupname: "type"].Value,
                    Ext = ext,
                }
            );
        }

        return subtitles;
    }
}
