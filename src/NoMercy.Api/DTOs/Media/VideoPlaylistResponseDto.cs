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

using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Media;

public class VideoPlaylistResponseDto
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "show")]
    public string? Show { get; set; }

    [JsonProperty(propertyName: "origin")]
    public Guid Origin { get; set; }

    [JsonProperty(propertyName: "uuid")]
    public int Uuid { get; set; }

    [JsonProperty(propertyName: "video_id")]
    public Ulid VideoId { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tmdb_id")]
    public int TmdbId { get; set; }

    [JsonProperty(propertyName: "video_type")]
    public string VideoType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "library_type")]
    public string LibraryType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "playlist_type")]
    public string PlaylistType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "playlist_id")]
    public dynamic PlaylistId { get; set; } = null!;

    [JsonProperty(propertyName: "year")]
    public long Year { get; set; }

    [JsonProperty(propertyName: "file")]
    public string File { get; set; } = string.Empty;

    [JsonProperty(propertyName: "progress")]
    public ProgressDto? Progress { get; set; }

    [JsonProperty(propertyName: "image")]
    public string? Image { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "sources")]
    public SourceDto[] Sources { get; set; } = [];

    [JsonProperty(propertyName: "fonts")]
    public List<IFont> Fonts { get; set; } = [];

    [JsonProperty(propertyName: "chapters")]
    public List<IChapter> Chapters { get; set; } = [];

    [JsonProperty(propertyName: "tracks")]
    public List<VideoTrack> Tracks { get; set; } = [];

    [JsonProperty(propertyName: "rating")]
    public RatingClass? ContentRating { get; set; }

    [JsonProperty(propertyName: "audio")]
    public List<IAudio> Audio { get; set; } = [];

    [JsonProperty(propertyName: "captions")]
    public List<ISubtitle> Captions { get; set; } = [];

    [JsonProperty(propertyName: "qualities")]
    public List<IVideo> Qualities { get; set; } = [];

    [JsonProperty(propertyName: "season")]
    public int? Season { get; set; }

    [JsonProperty(propertyName: "episode")]
    public int? Episode { get; set; }

    [JsonProperty(propertyName: "seasonName")]
    public string? SeasonName { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }

    public VideoPlaylistResponseDto() { }

    public VideoPlaylistResponseDto(
        Episode episode,
        string playlistType,
        dynamic playlistId,
        string country,
        int? index = null
    )
    {
        VideoFile? videoFile = episode.VideoFiles.FirstOrDefault();
        if (videoFile is null)
            return;

        if (episode.Tv is null)
            return;

        UserData? userData = videoFile.UserData.FirstOrDefault();
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string? logo = episode
            .Tv.Images.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: image => image.Type == "logo")
            ?.FilePath;

        string tvTitle = episode.Tv.Translations.FirstOrDefault()?.Title ?? episode.Tv.Title;

        string? title = episode.Translations.FirstOrDefault()?.Title ?? episode.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview ?? episode.Overview;

        string? specialTitle = index is not null
            ? $"{tvTitle} %S{episode.SeasonNumber} %E{episode.EpisodeNumber} - {title}"
            : title;

        Subs subs = Subtitles(videoFile: videoFile);
        Id = episode.Id;
        Title = specialTitle;
        Description = overview;
        Show = index is not null ? null : tvTitle;
        Origin = Info.DeviceId;
        Uuid = episode.Tv.Id + episode.Id;
        Duration = videoFile.Duration ?? "0";
        TmdbId = episode.Tv.Id;
        VideoType = MediaTypes.TvMediaType;
        VideoId = videoFile.Id;
        LibraryType = episode.Tv.MediaType ?? MediaTypes.TvMediaType;
        PlaylistType = playlistType;
        PlaylistId = playlistId;
        Year = episode.Tv.FirstAirDate.ParseYear();
        Progress = userData?.LastPlayedDate is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = DateTime.Parse(s: userData.LastPlayedDate),
            }
            : null;
        Image = episode.Still;
        Logo = logo;
        File = $"{baseFolder}{videoFile.Filename}";
        Sources =
        [
            new()
            {
                Src = $"{baseFolder}{videoFile.Filename}",
                Type = videoFile.Filename.Contains(value: ".mp4") ? "video/mp4" : "application/x-mpegURL",
                Languages =
                    JsonConvert
                        .DeserializeObject<string?[]>(value: videoFile.Languages)
                        ?.Where(predicate: lang => lang != null)
                        .ToArray()
                    ?? [],
            },
        ];

        List<VideoTrack> fontsTrack = videoFile.Metadata?.Fonts is { Count: > 0 }
            ? [new() { File = $"{baseFolder}/fonts.json", Kind = "fonts" }]
            : [];

        List<VideoTrack> chaptersTrack = videoFile.Metadata?.ChapterFile
            is { FileSize: > 0 } chaptersFile
            ? [new() { File = $"{baseFolder}{chaptersFile.FileName}", Kind = "chapters" }]
            : [];

        Tracks = videoFile
            .Tracks.Select(selector: t => new VideoTrack
            {
                Label = t.Label,
                File = $"{baseFolder}{t.File}",
                Language = t.Language,
                Kind = t.Kind,
            })
            .Concat(second: subs.TextTracks)
            .Concat(second: fontsTrack)
            .Concat(second: chaptersTrack)
            .OrderBy(keySelector: track => track.Language)
            .ToList();

        Season = index is not null ? 0 : episode.SeasonNumber;
        Episode = index ?? episode.EpisodeNumber;
        SeasonName = episode.Season.Title;
        EpisodeId = episode.Id;
        Chapters = videoFile.Metadata?.Chapters ?? [];
        Fonts =
            videoFile
                .Metadata?.Fonts?.Select(selector: font => new IFont
                {
                    FileName = $"{baseFolder}{font.FileName}",
                    FileHash = font.FileHash,
                    FileSize = font.FileSize,
                })
                .ToList()
            ?? [];

        Audio = videoFile.Metadata?.Audio ?? [];
        Captions = videoFile.Metadata?.Subtitles ?? [];
        Qualities = videoFile.Metadata?.Video ?? [];

        ContentRating = episode
            .Tv.CertificationTvs.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image = new(
                    value: $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg"
                ),
            })
            .FirstOrDefault();
    }

    public VideoPlaylistResponseDto(
        Movie movie,
        string playlistType,
        dynamic playlistId,
        string country,
        int? index = null,
        Collection? collection = null
    )
    {
        VideoFile? videoFile = movie.VideoFiles.FirstOrDefault();
        if (videoFile is null)
            return;

        string? logo = movie
            .Images.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: image => image.Type == "logo")
            ?.FilePath;
        UserData? userData = videoFile.UserData.FirstOrDefault();
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string title = movie.Translations.FirstOrDefault()?.Title ?? movie.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview ?? movie.Overview;

        Subs subs = Subtitles(videoFile: videoFile);
        Id = movie.Id;
        Title = title;
        Description = overview;
        Origin = Info.DeviceId;
        Uuid = movie.Id;
        Duration = videoFile.Duration ?? "0";
        TmdbId = collection?.Id ?? movie.Id;
        VideoType = MediaTypes.MovieMediaType;
        VideoId = videoFile.Id;
        LibraryType = MediaTypes.MovieMediaType;
        PlaylistType = playlistType;
        PlaylistId = playlistId;
        Year = movie.ReleaseDate.ParseYear();
        Progress = userData?.LastPlayedDate is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = DateTime.Parse(s: userData.LastPlayedDate),
            }
            : null;
        Image = movie.Backdrop;
        Logo = logo;
        File = $"{baseFolder}{videoFile.Filename}";
        Sources =
        [
            new()
            {
                Src = $"{baseFolder}{videoFile.Filename}",
                Type = videoFile.Filename.Contains(value: ".mp4") ? "video/mp4" : "application/x-mpegURL",
                Languages =
                    JsonConvert
                        .DeserializeObject<string?[]>(value: videoFile.Languages)
                        ?.Where(predicate: lang => lang != null)
                        .ToArray()
                    ?? [],
            },
        ];

        List<VideoTrack> fontsTrack = videoFile.Metadata?.Fonts is { Count: > 0 }
            ? [new() { File = $"{baseFolder}/fonts.json", Kind = "fonts" }]
            : [];

        List<VideoTrack> chaptersTrack = videoFile.Metadata?.ChapterFile
            is { FileSize: > 0 } chaptersFile
            ? [new() { File = $"{baseFolder}{chaptersFile.FileName}", Kind = "chapters" }]
            : [];

        Tracks = videoFile
            .Tracks.Select(selector: t => new VideoTrack
            {
                Label = t.Label,
                File = $"{baseFolder}{t.File}",
                Language = t.Language,
                Kind = t.Kind,
            })
            .Concat(second: subs.TextTracks)
            .Concat(second: fontsTrack)
            .Concat(second: chaptersTrack)
            .OrderBy(keySelector: track => track.Language)
            .ToList();

        Chapters = videoFile.Metadata?.Chapters ?? [];
        Fonts =
            videoFile
                .Metadata?.Fonts?.Select(selector: font => new IFont
                {
                    FileName = $"{baseFolder}{font.FileName}",
                    FileHash = font.FileHash,
                    FileSize = font.FileSize,
                })
                .ToList()
            ?? [];

        Audio = videoFile.Metadata?.Audio ?? [];
        Captions = videoFile.Metadata?.Subtitles ?? [];
        Qualities = videoFile.Metadata?.Video ?? [];

        ContentRating = movie
            .CertificationMovies.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image = new(
                    value: $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg"
                ),
            })
            .FirstOrDefault();

        if (index is null)
            return;
        SeasonName = "Collection";
        Season = 0;
        Episode = index;
        EpisodeId = movie.Id;
    }

    private record Subs
    {
        public List<VideoTrack> TextTracks { get; set; } = [];
    }

    public class Subtitle
    {
        [JsonProperty(propertyName: "language")]
        public string Language { get; set; } = "eng";

        [JsonProperty(propertyName: "type")]
        public string Type { get; set; } = "full";

        [JsonProperty(propertyName: "ext")]
        public string Ext { get; set; } = "vtt";
    }

    private static Subs Subtitles(VideoFile videoFile)
    {
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string subtitles = videoFile.Subtitles ?? "[]";
        // Subtitles is a string column on VideoFile that can drift to
        // malformed JSON across schema migrations or partial writes. Treat
        // a parse failure as 'no subtitle tracks' rather than crashing the
        // playlist response.
        List<Subtitle>? subtitleList;
        try
        {
            subtitleList = JsonConvert.DeserializeObject<List<Subtitle>>(value: subtitles);
        }
        catch (JsonException)
        {
            subtitleList = null;
        }

        List<VideoTrack> textTracks = [];

        foreach (Subtitle sub in subtitleList ?? [])
        {
            string language = sub.Language;
            string type = sub.Type;
            string ext = sub.Ext;

            textTracks.Add(
                item: new()
                {
                    Label = type,
                    File =
                        $"{baseFolder}/subtitles{(videoFile?.Filename).OrEmpty()
                    .Replace(oldValue: ".mp4", newValue: "")
                    .Replace(oldValue: ".m3u8", newValue: "")}.{language}.{type}.{ext}",
                    Language = language,
                    Kind = "subtitles",
                }
            );
        }

        return new() { TextTracks = textTracks };
    }
}
