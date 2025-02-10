using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record PlaylistResponseDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("show")] public string? Show { get; set; }
    [JsonProperty("origin")] public Guid Origin { get; set; }
    [JsonProperty("uuid")] public int Uuid { get; set; }
    [JsonProperty("video_id")] public Ulid VideoId { get; set; }
    [JsonProperty("duration")] public string Duration { get; set; } = string.Empty;
    [JsonProperty("tmdb_id")] public int TmdbId { get; set; }
    [JsonProperty("video_type")] public string VideoType { get; set; } = string.Empty;
    [JsonProperty("playlist_type")] public string PlaylistType { get; set; } = string.Empty;
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("progress")] public ProgressDto? Progress { get; set; }
    [JsonProperty("image")] public string? Image { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("sources")] public SourceDto[] Sources { get; set; } = [];
    [JsonProperty("fonts")] public List<FontDto?>? Fonts { get; set; } = [];
    [JsonProperty("fontsFile")] public string FontsFile { get; set; } = string.Empty;
    [JsonProperty("tracks")] public List<IVideoTrack> Tracks { get; set; } = [];

    [JsonProperty("season")] public int? Season { get; set; }
    [JsonProperty("episode")] public int? Episode { get; set; }
    [JsonProperty("seasonName")] public string? SeasonName { get; set; }
    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }

    public PlaylistResponseDto(Episode episode, int? index = null)
    {
        VideoFile? videoFile = episode.VideoFiles.FirstOrDefault();
        if (videoFile is null) return;

        UserData? userData = videoFile.UserData.FirstOrDefault();
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string? logo = episode.Tv.Images
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(image => image.Type == "logo")?.FilePath;

        string tvTitle = episode.Tv.Translations.FirstOrDefault()?.Title ?? episode.Tv.Title;

        string? title = episode.Translations.FirstOrDefault()?.Title ?? episode.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview ?? episode.Overview;

        string? specialTitle = index is not null
            ? $"{tvTitle} %S{episode.SeasonNumber} %E{episode.EpisodeNumber} - {title}"
            : title;

        Subs subs = Subtitles(videoFile);

        Id = episode.Id;
        Title = specialTitle;
        Description = overview;
        Show = index is not null
            ? null
            : tvTitle;
        Origin = Info.DeviceId;
        Uuid = episode.Tv.Id + episode.Id;
        VideoId = videoFile.Id;
        Duration = videoFile.Duration ?? "0";
        TmdbId = episode.Tv.Id;
        VideoType = "tv";
        PlaylistType = "tv";
        Year = episode.Tv.FirstAirDate.ParseYear();
        Progress = userData?.UpdatedAt is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = userData.UpdatedAt
            }
            : null;
        Image = episode.Still is not null ? "https://image.tmdb.org/t/p/original" + episode.Still : null;
        Logo = logo is not null ? "https://image.tmdb.org/t/p/original" + logo : null;
        File = $"{baseFolder}{videoFile.Filename}";
        Sources =
        [
            new()
            {
                Src = $"{baseFolder}{videoFile.Filename}",
                Type = videoFile.Filename.Contains(".mp4")
                    ? "video/mp4"
                    : "application/x-mpegURL",
                Languages = JsonConvert.DeserializeObject<string?[]>(videoFile.Languages)
                    ?.Where(lang => lang != null).ToArray()
            }
        ];

        Tracks = videoFile.Tracks.Select(t => new IVideoTrack
        {
            Label = t.Label,
            File = $"{baseFolder}{t.File}",
            Language = t.Language,
            Kind = t.Kind
        })
            .Concat(subs.TextTracks)
            .OrderBy(track => track.Language)
            .ToList();

        Season = index is not null ? 0 : episode.SeasonNumber;
        Episode = index ?? episode.EpisodeNumber;
        SeasonName = episode.Season.Title;
        EpisodeId = episode.Id;
    }

    public PlaylistResponseDto(Movie movie, int? index = null, Collection? collection = null)
    {
        VideoFile? videoFile = movie.VideoFiles.FirstOrDefault();
        if (videoFile is null) return;

        string? logo = movie.Images
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(image => image.Type == "logo")?.FilePath;
        UserData? userData = videoFile.UserData.FirstOrDefault();
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string title = movie.Translations.FirstOrDefault()?.Title ?? movie.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview ?? movie.Overview;

        Subs subs = Subtitles(videoFile);

        Id = movie.Id;
        Title = title;
        Description = overview;
        Origin = Info.DeviceId;
        Uuid = movie.Id;
        VideoId = videoFile.Id;
        Duration = videoFile.Duration ?? "0";
        TmdbId = collection?.Id ?? movie.Id;
        VideoType = "movie";
        PlaylistType = "movie";
        Year = movie.ReleaseDate.ParseYear();
        Progress = userData?.UpdatedAt is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = userData.UpdatedAt
            }
            : null;
        Image = movie.Backdrop is not null ? "https://image.tmdb.org/t/p/original" + movie.Backdrop : null;
        Logo = logo is not null ? "https://image.tmdb.org/t/p/original" + logo : null;
        File = $"{baseFolder}{videoFile.Filename}";
        Sources =
        [
            new()
            {
                Src = $"{baseFolder}{videoFile.Filename}",
                Type = videoFile.Filename.Contains(".mp4")
                    ? "video/mp4"
                    : "application/x-mpegURL",
                Languages = JsonConvert.DeserializeObject<string?[]>(videoFile.Languages)
                    ?.Where(lang => lang != null).ToArray()
            }
        ];

        Tracks = videoFile.Tracks.Select(t => new IVideoTrack
        {
            Label = t.Label,
            File = $"{baseFolder}{t.File}",
            Language = t.Language,
            Kind = t.Kind
        })
            .Concat(subs.TextTracks)
            .OrderBy(track => track.Language)
            .ToList();

        if (index is null) return;
        SeasonName = "Collection";
        Season = 0;
        Episode = index;
        EpisodeId = movie.Id;
    }

    private record Subs
    {
        public List<IVideoTrack> TextTracks { get; set; } = [];
        public List<FontDto?>? Fonts { get; set; } = [];
        public string FontsFile { get; set; } = string.Empty;
    }

    public class Subtitle
    {
        [JsonProperty("language")] public string Language { get; set; } = "eng";
        [JsonProperty("type")] public string Type { get; set; } = "full";
        [JsonProperty("ext")] public string Ext { get; set; } = "vtt";
    }

    private static Subs Subtitles(VideoFile videoFile)
    {
        string baseFolder = $"/{videoFile.Share}{videoFile.Folder}";

        string subtitles = videoFile.Subtitles ?? "[]";
        List<Subtitle>? subtitleList = JsonConvert.DeserializeObject<List<Subtitle>>(subtitles);

        List<IVideoTrack> textTracks = [];
        bool search = false;

        foreach (Subtitle sub in subtitleList ?? [])
        {
            string language = sub.Language;
            string type = sub.Type;
            string ext = sub.Ext;

            if (ext == "ass") search = true;

            textTracks.Add(new()
            {
                Label = type,
                File = $"{baseFolder}/subtitles{videoFile?.Filename
                    .Replace(".mp4", "")
                    .Replace(".m3u8", "")}.{language}.{type}.{ext}",
                Language = language,
                Kind = "subtitles"
            });
        }

        List<FontDto?>? fonts = [];
        string fontsFile = "";

        if (!search || !System.IO.File.Exists($"{videoFile?.HostFolder}fonts.json"))
            return new()
            {
                TextTracks = textTracks,
                Fonts = fonts,
                FontsFile = fontsFile
            };

        fontsFile = $"/{videoFile?.Share}/{videoFile?.Folder}fonts.json";
        fonts = JsonConvert.DeserializeObject<List<FontDto?>?>(
            System.IO.File.ReadAllText($"{videoFile?.HostFolder}fonts.json"));

        return new()
        {
            TextTracks = textTracks,
            Fonts = fonts,
            FontsFile = fontsFile
        };
    }
}
