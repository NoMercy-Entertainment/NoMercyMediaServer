using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Server.app.Http.Controllers.Api.V1.DTO;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Server.app.Http.Controllers.Api.V1.Media.DTO;

public record ContinueWatchingDto
{
    [JsonProperty("data")] public IEnumerable<ContinueWatchingItemDto> Data { get; set; }
}

public record ContinueWatchingItemDto
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("media_type")] public string? MediaType { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
    [JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("year")] public long? Year { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("rating")] public RatingDto? Rating { get; set; }
    [JsonProperty("videoId")] public string? VideoId { get; set; }
    [JsonProperty("videos")] public VideoDto[]? Videos { get; set; }
    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }

    public ContinueWatchingItemDto(UserData item)
    {
        Id = item.MovieId?.ToString() ?? item.TvId?.ToString() ??
            item.SpecialId?.ToString() ?? item.CollectionId.ToString() ?? string.Empty;
        Type = item.Type;
        UpdatedAt = item.UpdatedAt;
        CreatedAt = item.CreatedAt;

        if (item.Movie is not null)
        {
            ColorPalette = item.Movie.ColorPalette;
            Year = item.Movie.ReleaseDate.ParseYear();
            Poster = item.Movie.Poster;
            Backdrop = item.Movie.Backdrop;
            Title = item.Movie.Title;
            TitleSort = item.Movie.Title.TitleSort(item.Movie.ReleaseDate);
            Overview = item.Movie.Overview;
            MediaType = "movie";
            Type = "movie";

            NumberOfItems = 1;
            HaveItems = item.Movie.VideoFiles.Count(v => v.Folder != null);

            Videos = item.Movie.Media
                .Select(media => new VideoDto(media))
                .ToArray();
        }
        else if (item.Tv is not null)
        {
            ColorPalette = item.Tv.ColorPalette;
            Year = item.Tv.FirstAirDate.ParseYear();
            Poster = item.Tv.Poster;
            Backdrop = item.Tv.Backdrop;
            Title = item.Tv.Title;
            TitleSort = item.Tv.Title.TitleSort(item.Tv.FirstAirDate);
            HaveItems = item.Tv.HaveEpisodes;
            Overview = item.Tv.Overview;
            Type = item.Tv.Type;

            MediaType = "tv";
            Type = "tv";

            NumberOfItems = item.Tv.NumberOfEpisodes;
            HaveItems = item.Tv.Episodes
                .Count(episode => episode.VideoFiles.Any(v => v.Folder != null));

            Videos = item.Tv.Media
                .Select(media => new VideoDto(media))
                .ToArray();
        }
        else if (item.Special is not null)
        {
            ColorPalette = item.Special.ColorPalette;
            Poster = item.Special.Poster;
            Backdrop = item.Special.Backdrop;
            Title = item.Special.Title;
            TitleSort = item.Special.Title.TitleSort();
            Overview = item.Special.Overview;

            MediaType = "specials";
            Type = "specials";

            NumberOfItems = item.Special.Items.Count;
            HaveItems = item.Special.Items
                            .Select(specialItem => specialItem.Episode?.VideoFiles
                                .Any(videoFile => videoFile.Folder != null)).Count()
                        + item.Special.Items.Count(i => i.MovieId != null);
            // Videos = item.SpecialItemsDto.SpecialItems
            //     .SelectMany(specialDto => specialDto.Tv.Media)
            //     .Select(media => new VideoDto(media))
            //     .ToArray();
        }
        else if (item.Collection is not null)
        {
            ColorPalette = item.Collection.ColorPalette;
            Poster = item.Collection.Poster;
            Backdrop = item.Collection.Backdrop;
            Title = item.Collection.Title;
            TitleSort = item.Collection.Title.TitleSort();
            Overview = item.Collection.Overview;

            MediaType = "collection";
            Type = "collection";

            NumberOfItems = item.Collection.CollectionMovies.Count;
            HaveItems = item.Collection.CollectionMovies
                .SelectMany(collectionMovie => collectionMovie.Movie.VideoFiles)
                .Count(videoFile => videoFile.Folder != null);

            Videos = item.Collection.CollectionMovies
                .SelectMany(collectionMovie => collectionMovie.Movie.Media)
                .Select(media => new VideoDto(media))
                .ToArray();
        }
    }
}