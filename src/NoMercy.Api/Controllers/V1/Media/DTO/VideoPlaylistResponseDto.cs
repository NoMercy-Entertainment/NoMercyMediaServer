using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public class VideoPlaylistResponseDto
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
    [JsonProperty("library_type")] public string LibraryType { get; set; } = string.Empty;
    [JsonProperty("playlist_type")] public string PlaylistType { get; set; } = string.Empty;
    [JsonProperty("playlist_id")] public dynamic PlaylistId { get; set; } = null!;
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("progress")] public ProgressDto? Progress { get; set; }
    [JsonProperty("image")] public string? Image { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("sources")] public SourceDto[] Sources { get; set; } = [];
    [JsonProperty("fonts")] public List<IFont> Fonts { get; set; } = [];
    [JsonProperty("chapters")] public List<IChapter> Chapters { get; set; } = [];
    [JsonProperty("tracks")] public List<IVideoTrack> Tracks { get; set; } = [];
    [JsonProperty("rating")] public RatingClass? ContentRating { get; set; }
    
    [JsonProperty("audio")] public List<IAudio> Audio { get; set; } = [];
    [JsonProperty("captions")] public List<ISubtitle> Captions { get; set; } = [];
    [JsonProperty("qualities")] public List<IVideo> Qualities { get; set; } = [];

    [JsonProperty("season")] public int? Season { get; set; }
    [JsonProperty("episode")] public int? Episode { get; set; }
    [JsonProperty("seasonName")] public string? SeasonName { get; set; }
    [JsonProperty("episode_id")] public int? EpisodeId { get; set; }

    public VideoPlaylistResponseDto()
    {
        
    }

    public VideoPlaylistResponseDto(Episode episode, string playlistType, dynamic playlistId, string country, int? index = null)
    {
        VideoFile? videoFile = episode.VideoFiles.FirstOrDefault();
        if (videoFile is null) return;

        if (episode.Tv is null) return;

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
        Duration = videoFile.Duration ?? "0";
        TmdbId = episode.Tv.Id;
        VideoType = Config.TvMediaType;
        VideoId = videoFile.Id;
        LibraryType = episode.Tv.MediaType ?? Config.TvMediaType;
        PlaylistType = playlistType;
        PlaylistId = playlistId;
        Year = episode.Tv.FirstAirDate.ParseYear();
        Progress = userData?.LastPlayedDate is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = DateTime.Parse(userData.LastPlayedDate)
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
                Type = videoFile.Filename.Contains(".mp4")
                    ? "video/mp4"
                    : "application/x-mpegURL",
                Languages = JsonConvert.DeserializeObject<string?[]>(videoFile.Languages)
                    ?.Where(lang => lang != null).ToArray() ?? []
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
            .Concat([new()
            {
                File = $"{baseFolder}/fonts.json",
                Kind = "fonts",
            }])
            .OrderBy(track => track.Language)
            .ToList();

        Season = index is not null ? 0 : episode.SeasonNumber;
        Episode = index ?? episode.EpisodeNumber;
        SeasonName = episode.Season?.Title;
        EpisodeId = episode.Id;
        Chapters = videoFile.Metadata?.Chapters ?? [];
        Fonts = videoFile.Metadata?.Fonts?.Select(font => new IFont
        {
            FileName = $"{baseFolder}{font.FileName}",
            FileHash = font.FileHash,
            FileSize = font.FileSize
        }).ToList() ?? [];

        Audio = videoFile.Metadata?.Audio ?? [];
        Captions = videoFile.Metadata?.Subtitles ?? [];
        Qualities = videoFile.Metadata?.Video ?? [];
        
        ContentRating = episode.Tv.CertificationTvs
            .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                                         || certificationMovie.Certification.Iso31661 == country)
            .Select(certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image = new($"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg")
            })
            .FirstOrDefault();
    }

    public VideoPlaylistResponseDto(Movie movie, string playlistType, dynamic playlistId, string country, int? index = null, Collection? collection = null)
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
        Duration = videoFile.Duration ?? "0";
        TmdbId = collection?.Id ?? movie.Id;
        VideoType = Config.MovieMediaType;
        VideoId = videoFile.Id;
        LibraryType = Config.MovieMediaType;
        PlaylistType = playlistType;
        PlaylistId = playlistId;
        Year = movie.ReleaseDate.ParseYear();
        Progress = userData?.LastPlayedDate is not null
            ? new ProgressDto
            {
                Time = userData.Time ?? 0,
                Date = DateTime.Parse(userData.LastPlayedDate)
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
                Type = videoFile.Filename.Contains(".mp4")
                    ? "video/mp4"
                    : "application/x-mpegURL",
                Languages = JsonConvert.DeserializeObject<string?[]>(videoFile.Languages)
                    ?.Where(lang => lang != null).ToArray() ?? []
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
            .Concat([new()
            {
                File = $"{baseFolder}/fonts.json",
                Kind = "fonts",
            }])
            .OrderBy(track => track.Language)
            .ToList();
        
        Chapters = videoFile.Metadata?.Chapters ?? [];
        Fonts = videoFile.Metadata?.Fonts?.Select(font => new IFont
        {
            FileName = $"{baseFolder}{font.FileName}",
            FileHash = font.FileHash,
            FileSize = font.FileSize
        }).ToList() ?? [];
        
        Audio = videoFile.Metadata?.Audio ?? [];
        Captions = videoFile.Metadata?.Subtitles ?? [];
        Qualities = videoFile.Metadata?.Video ?? [];

        if (index is null) return;
        SeasonName = "Collection";
        Season = 0;
        Episode = index;
        EpisodeId = movie.Id;
        
        ContentRating = movie.CertificationMovies
            .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                                         || certificationMovie.Certification.Iso31661 == country)
            .Select(certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image = new($"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg")
            })
            .FirstOrDefault();
    }

    private record Subs
    {
        public List<IVideoTrack> TextTracks { get; set; } = [];
        public List<FontDto>? Fonts { get; set; } = [];
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
                File = $"{baseFolder}/subtitles{(videoFile?.Filename ?? string.Empty)
                    .Replace(".mp4", "")
                    .Replace(".m3u8", "")}.{language}.{type}.{ext}",
                Language = language,
                Kind = "subtitles"
            });
        }

        List<FontDto?>? fonts = [];
        string fontsFile = "";

        if (!search || videoFile?.HostFolder == null || !System.IO.File.Exists($"{videoFile.HostFolder}fonts.json"))
            return new()
            {
                TextTracks = textTracks,
                Fonts = fonts,
                FontsFile = fontsFile
            };

        fontsFile = $"/{videoFile.Share}/{videoFile.Folder}fonts.json";
        fonts = JsonConvert.DeserializeObject<List<FontDto?>?>(
            System.IO.File.ReadAllText($"{videoFile.HostFolder}fonts.json"));

        return new()
        {
            TextTracks = textTracks,
            Fonts = fonts,
            FontsFile = fontsFile
        };
    }
}