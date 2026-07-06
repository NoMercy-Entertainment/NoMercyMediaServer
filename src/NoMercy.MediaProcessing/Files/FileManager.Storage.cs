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

using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.MediaProcessing.Files;

public partial class FileManager
{
    private async Task StoreMusic()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        if (items.Count == 0)
            return;

        foreach (MediaFile item in items)
            await StoreAudioItem(item);

        Logger.App($"Found {items.Count} music files");
    }

    private async Task StoreMovie()
    {
        MediaFile? item = Files
            .SelectMany(file => file.Files ?? [])
            .FirstOrDefault(file => file.Parsed is not null);

        if (item == null)
            return;

        await StoreVideoItem(item);

        Logger.App($"Found {item.Path} for {Movie?.Title}");
    }

    private async Task StoreTvShow()
    {
        List<MediaFile> items = Files
            .SelectMany(file => file.Files ?? [])
            .Where(mediaFolder => mediaFolder.Parsed is not null)
            .ToList();

        Logger.App(
            $"[StoreTvShow] {Show?.Title} ({Show?.Id}): {items.Count} candidate files across {Folders.Count} folder(s)",
            LogEventLevel.Information
        );

        if (items.Count == 0)
            return;

        int idx = 0;
        foreach (MediaFile item in items)
        {
            idx++;
            Logger.App(
                $"[StoreVideoItem] {idx}/{items.Count} {Path.GetFileName(item.Path)}",
                LogEventLevel.Information
            );
            await StoreVideoItem(item);
        }

        Logger.App($"Found {items.Count} files for {Show?.Title}");
    }

    private async Task StoreAudioItem(MediaFile? item)
    {
        if (item?.Parsed is null)
            return;

        Folder? folder = Folders.FirstOrDefault(folder => item.Path.Contains(folder.Path));
        if (folder == null)
            return;

        await Task.CompletedTask;
    }

    private async Task StoreVideoItem(MediaFile item)
    {
        string itemPath = item.Path.Replace('\\', '/');
        Folder? folder = Folders.FirstOrDefault(f =>
            itemPath.Contains(f.Path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
        );
        if (folder == null)
        {
            Logger.App(
                $"[StoreVideoItem] no Folders match for {itemPath} — skipping (Folders={string.Join(", ", Folders.Select(f => f.Path))})",
                LogEventLevel.Warning
            );
            return;
        }

        // MediaScan resolves every path through the driver, so item.Path is
        // driver-absolute (e.g. "/mnt/vault/Media/Marvels/TV.Shows/..."). Every
        // storage call below is on the IStorage facade, which rejects absolute
        // keys on remote backends (StoragePathGuard). Rebase the path onto its
        // scope-relative root — the folder's stored Path — so the facade, the
        // stored HostFolder/Folder, and the served URLs are all backend-neutral.
        itemPath = StoragePathHelpers.RebaseToFolderRoot(itemPath, folder.Path);

        IStorage storage = StorageFor(folder);
        string fileName = "/" + storage.GetName(itemPath);
        string hostFolder = itemPath.Replace(fileName, "");
        string showName = (Movie?.Folder ?? Show?.Folder).OrEmpty().Trim('/', '\\');
        int showIdx = string.IsNullOrEmpty(showName)
            ? -1
            : itemPath.IndexOf(showName, StringComparison.OrdinalIgnoreCase);
        string baseFolder =
            showIdx >= 0 ? ("/" + itemPath[showIdx..]).Replace(fileName, "") : hostFolder;

        List<Subtitle> subtitles = GetSubtitles(storage, hostFolder);

        List<VideoTrack> tracks = GetExtraFiles(storage, hostFolder);

        Episode? episode = await fileRepository.GetEpisode(Show?.Id, item);

        Metadata metadata = await MakeMetadata(
            storage,
            item,
            fileName,
            baseFolder,
            hostFolder,
            tracks
        );

        Ulid metadataId = await fileRepository.StoreMetadata(metadata);

        Logger.App($"Storing video file: {episode?.Id}, {Movie?.Id}", LogEventLevel.Verbose);

        // Joined multi-episode file (e.g. S01E01-E04): record the last covered
        // episode so every episode in the range counts as present, while a single
        // VideoFile keeps it from being listed twice in playlists.
        int? lastEpisodeNumber = null;
        if (episode is not null)
        {
            IReadOnlyList<int> covered = Parsing.EpisodeRangeParser.Expand(
                fileName,
                episode.SeasonNumber,
                episode.EpisodeNumber
            );
            if (covered.Count > 1)
                lastEpisodeNumber = covered[^1];
        }
        VideoFile videoFile = new()
        {
            EpisodeId = episode?.Id,
            LastEpisodeNumber = lastEpisodeNumber,
            MovieId = Movie?.Id,
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            Filename = fileName.Replace("\\", "/"),

            Share = folder.Id.ToString(),
            Duration = Regex.Replace(
                Regex.Replace((item.FFprobe?.Duration.ToString()).OrEmpty(), @"\.\d+", ""),
                "^00:",
                ""
            ),
            // Chapters = JsonConvert.SerializeObject(item.FFprobe?.Chapters ?? []),
            Chapters = "",
            Languages = JsonConvert.SerializeObject(
                item.FFprobe?.AudioStreams.Select(stream => stream.Language)
                    .Where(stream => stream != null && stream != "und")
            ),
            Quality = (item.FFprobe?.VideoStreams.FirstOrDefault()?.Width.ToString()).OrEmpty(),
            Subtitles = JsonConvert.SerializeObject(subtitles),
            Tracks = tracks.ToArray(),
            MetadataId = metadataId,
        };

        await fileRepository.StoreVideoFile(videoFile);
    }

    private async Task<Metadata> MakeMetadata(
        IStorage storage,
        MediaFile item,
        string fileName,
        string baseFolder,
        string hostFolder,
        List<VideoTrack> extraFiles
    )
    {
        List<IVideo> video = GetVideoHashList(storage, hostFolder);
        List<IAudio> audio = GetAudioHashList(storage, hostFolder);
        List<ISubtitle> subtitles = GetSubtitleHashList(storage, hostFolder);
        List<IFont> fonts = GetFontHashList(storage, hostFolder);
        List<IPreview> previews = GetPreviewHashList(storage, hostFolder, extraFiles);

        VideoTrack? chaptersFile = extraFiles.FirstOrDefault(file => file.Kind == "chapters");

        List<IChapter> chapters = chaptersFile?.File is not null
            ? await GetChapterHashListAsync(
                storage,
                hostFolder,
                Path.GetFileName(chaptersFile.File)
            )
            : [];

        IChapterFile? chaptersFileHashMap = chaptersFile?.File is not null
            ? new()
            {
                FileName = "/" + Path.GetFileName(chaptersFile.File).Replace("\\", "/"),
                FileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(chaptersFile.File))
                ),
                FileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(chaptersFile.File))
                ),
            }
            : null;

        IFontsFile? fontsFileHashMap = extraFiles
            .Where(file => file.Kind == "fonts")
            .Select(file => new IFontsFile
            {
                FileName = "/" + Path.GetFileName(file.File).Replace("\\", "/"),
                FileSize = storage.SizeOrZero(
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
                FileHash = ComputeFileHash(
                    storage,
                    storage.CombinePath(hostFolder, Path.GetFileName(file.File).OrEmpty())
                ),
            })
            .FirstOrDefault();

        Metadata metadata = new()
        {
            Filename = fileName.Replace("\\", "/"),
            Duration = (item.FFprobe?.Duration.ToString()).OrEmpty(),
            Folder = baseFolder.Replace("\\", "/"),
            HostFolder = hostFolder.Replace("\\", "/"),
            FolderSize = GetDirectorySize(storage, hostFolder),

            Type = Movie?.Id is not null
                ? Database.Models.Media.MediaType.Movie
                : Database.Models.Media.MediaType.Tv,

            Audio = audio,
            Previews = previews,
            Subtitles = subtitles,
            Video = video,
            Chapters = chapters,
            ChapterFile = chaptersFileHashMap,
            Fonts = fonts,
            FontsFile = fontsFileHashMap,
        };

        return metadata;
    }
}
