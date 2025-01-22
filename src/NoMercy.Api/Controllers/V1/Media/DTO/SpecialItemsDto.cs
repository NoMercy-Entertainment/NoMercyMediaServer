using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SpecialItemsDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("episode_ids")] public int[] EpisodeIds { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; }
    [JsonProperty("backdrops")] public IEnumerable<ImageDto> Backdrops { get; set; }
    [JsonProperty("posters")] public IEnumerable<ImageDto> Posters { get; set; }

    [JsonProperty("cast")] public IEnumerable<PeopleDto> Cast { get; set; }
    [JsonProperty("crew")] public IEnumerable<PeopleDto> Crew { get; set; }

    [JsonProperty("rating")] public Certification Rating { get; set; }

    [JsonProperty("videoId")] public string? VideoId { get; set; }

    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int HaveItems { get; set; }
    [JsonProperty("duration")] public int Duration { get; set; }
    
    [JsonProperty("total_duration")] public int TotalDuration { get; set; }

    public SpecialItemsDto(Movie movie)
    {
        Id = movie.Id;
        EpisodeIds = [];
        Title = movie.Title;
        Overview =  movie.Overview;

        Backdrop = movie.Backdrop;
        // Watched = movie.Watched;
        Logo = movie.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;

        Backdrops = movie.Images
            .Where(media => media.Type == "backdrop")
            .Take(2)
            .Select(media => new ImageDto(media));

        Posters = movie.Images
            .Where(media => media.Type == "poster")
            .Take(2)
            .Select(media => new ImageDto(media));

        MediaType = "movie";
        ColorPalette = movie.ColorPalette;
        Poster = movie.Poster;
        Type = "movie";
        Link = new($"/movie/{Id}", UriKind.Relative);
        Year = movie.ReleaseDate.ParseYear();
        Duration = movie.Runtime * 60 ?? 0;
        
        TotalDuration = movie.Runtime * 60 ?? 0;
        
        Genres = movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie.Genre));

        Rating = movie.CertificationMovies
            .Select(certificationMovie => certificationMovie.Certification)
            .FirstOrDefault() ?? new Certification();

        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count > 0 ? 1 : 0;

        VideoId = movie.Video;

        Cast = movie.Cast
            .Take(15)
            .Select(cast => new PeopleDto(cast));

        Crew = movie.Crew
            .Take(15)
            .Select(crew => new PeopleDto(crew));
    }

    public SpecialItemsDto(Tv tv)
    {
        Id = tv.Id;
        EpisodeIds = tv.Episodes?
            .Select(episode => episode.Id)
            .ToArray() ?? [];

        Title = tv.Title;
        Overview = tv.Overview;

        Backdrop = tv.Backdrop;
        // Watched = tv.Watched;
        Logo = tv.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;

        Backdrops = tv.Images
            .Where(media => media.Type == "backdrop")
            .Take(2)
            .Select(media => new ImageDto(media));

        Posters = tv.Images
            .Where(media => media.Type == "poster")
            .Take(2)
            .Select(media => new ImageDto(media));

        MediaType = "tv";
        ColorPalette = tv.ColorPalette;
        Poster = tv.Poster;
        Type = "tv";
        Link = new($"/tv/{Id}", UriKind.Relative);
        Year = tv.FirstAirDate.ParseYear();

        Genres = tv.GenreTvs
            .Select(genreTv => new GenreDto(genreTv.Genre));

        Rating = tv.CertificationTvs
            .Select(certificationTv => certificationTv.Certification)
            .FirstOrDefault() ?? new Certification();

        NumberOfItems = tv.Episodes?.Where(e => e.SeasonNumber > 0).Count() ?? 0;
        int have = tv.Episodes?.Where(e => e.SeasonNumber > 0)
            .Count(episode => episode.VideoFiles.Any()) ?? 0;

        HaveItems = have;

        Duration = tv.Duration * have * 60 ?? 0;
        
        TotalDuration = tv.Episodes?.Sum(item => item.Tv.Duration * 60 ?? 0) ?? 0;

        // Watched = tv.Episodes
        //     .SelectMany(episode => episode!.VideoFiles
        //         .Where(videoFile => videoFile.UserData.Any(userData => userData.UserId.Equals(userId)))
        //     .Count();

        VideoId = tv.Trailer;

        Cast = tv.Cast
            .Take(15)
            .Select(cast => new PeopleDto(cast));

        Crew = tv.Crew
            .Take(15)
            .Select(crew => new PeopleDto(crew));
    }

}
