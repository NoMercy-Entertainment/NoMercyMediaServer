using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record Render
{
    [JsonProperty("id")] public Ulid Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("data")] public IEnumerable<object> Data { get; set; } = [];
}

public record ComponentDto<T>
{
    public ComponentDto()
    {
        Id = Ulid.NewUlid();
        Update = new(Id);
        Update.When = null;
    }

    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("component")] public string Component { get; set; } = string.Empty;
    [JsonProperty("props")] public RenderProps<T> Props { get; set; } = new();
    [JsonProperty("update")] public Update Update { get; set; }
    [JsonProperty("replacing")] public Ulid Replacing { get; set; }
}

public record Update
{
    [JsonProperty("when")]public string? When { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = default!;
    [JsonProperty("body")] public object Body { get; set; } = new();

    public Update(Ulid ulid)
    {
       Body = new
       {
           replace_id = ulid
       };
    }
}

public record RenderProps<T>
{
    [JsonProperty("id")] public Ulid Id { get; set; } = Ulid.NewUlid();
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("more_link")] public Uri? MoreLink { get; set; }
    [JsonProperty("more_link_text")] public string? MoreText => MoreLink is not null ? "See all".Localize() : null;
    [JsonProperty("items")] public IEnumerable<ComponentDto<T>>? Items { get; set; } = [];
    [JsonProperty("data")] public T? Data { get; set; }
    [JsonProperty("watch")] public bool Watch { get; set; }
    [JsonProperty("context_menu_items")] public Dictionary<string, object>[]? ContextMenuItems { get; set; } = [];
    [JsonProperty("url")] public Uri? Url { get; set; }
    [JsonProperty("displayList")] public IEnumerable<ArtistTrackDto>? DisplayList { get; set; } = [];
}

public record RenderPropsItemDto
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
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("rating")] public RatingDto? Rating { get; set; }
    [JsonProperty("videoId")] public string? VideoId { get; set; }
    [JsonProperty("videos")] public VideoDto[]? Videos { get; set; }
    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }
    [JsonProperty("content_ratings")] public IEnumerable<ContentRating> ContentRatings { get; set; } = [];
    [JsonProperty("link")] public required Uri Link { get; set; } = default!;

    public RenderPropsItemDto(UserData item, string country)
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
            Link = new($"/movie/{Id}/watch", UriKind.Relative);

            NumberOfItems = 1;
            HaveItems = item.Movie.VideoFiles.Count(v => v.Folder != null);

            Videos = item.Movie.Media
                .Select(media => new VideoDto(media))
                .ToArray();

            ContentRatings = item.Movie.CertificationMovies
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country)
                .Select(certificationMovie => new ContentRating
                {
                    Rating = certificationMovie.Certification.Rating,
                    Iso31661 = certificationMovie.Certification.Iso31661
                });
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
            Link = new($"/tv/{Id}/watch", UriKind.Relative);

            NumberOfItems = item.Tv.NumberOfEpisodes;
            HaveItems = item.Tv.Episodes
                .Count(episode => episode.VideoFiles.Any(v => v.Folder != null));

            Videos = item.Tv.Media
                .Select(media => new VideoDto(media))
                .ToArray();

            ContentRatings = item.Tv.CertificationTvs
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country)
                .Select(certificationTv => new ContentRating
                {
                    Rating = certificationTv.Certification.Rating,
                    Iso31661 = certificationTv.Certification.Iso31661
                });
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
            Link = new($"/specials/{Id}/watch", UriKind.Relative);

            NumberOfItems = item.Special.Items.Count;
            HaveItems = item.Special.Items
                    .Select(specialItem => specialItem.Episode?.VideoFiles
                        .Any(videoFile => videoFile.Folder != null)).Count()
                + item.Special.Items.Count(i => i.MovieId != null);
            Videos = [];
            ContentRatings = item.Special.Items
                .SelectMany(specialItem => specialItem.Episode?.Tv.CertificationTvs
                    .Where(certificationTv => certificationTv.Certification.Iso31661 == "US"
                        || certificationTv.Certification.Iso31661 == country)
                    .Select(certificationTv => new ContentRating
                    {
                        Rating = certificationTv.Certification.Rating,
                        Iso31661 = certificationTv.Certification.Iso31661
                    }) ?? Array.Empty<ContentRating>())
                .Concat(item.Special.Items
                    .Where(specialItem => specialItem.MovieId != null)
                    .SelectMany(specialItem => specialItem.Movie?.CertificationMovies
                        .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                            || certificationMovie.Certification.Iso31661 == country)
                        .Select(certificationMovie => new ContentRating
                        {
                            Rating = certificationMovie.Certification.Rating,
                            Iso31661 = certificationMovie.Certification.Iso31661
                        }) ?? Array.Empty<ContentRating>()));
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
            Year = item.Collection.CollectionMovies
                .MinBy(movie => movie.Movie.ReleaseDate?.ParseYear())
                ?.Movie.ReleaseDate.ParseYear() ?? 0;

            MediaType = "collection";
            Type = "collection";
            Link = new($"/collection/{Id}/watch", UriKind.Relative);

            NumberOfItems = item.Collection.CollectionMovies.Count;
            HaveItems = item.Collection.CollectionMovies
                .SelectMany(collectionMovie => collectionMovie.Movie.VideoFiles)
                .Count(videoFile => videoFile.Folder != null);

            Videos = item.Collection.CollectionMovies
                .SelectMany(collectionMovie => collectionMovie.Movie.Media)
                .Select(media => new VideoDto(media))
                .ToArray();

            ContentRatings = item.Collection.CollectionMovies
                .SelectMany(collectionMovie => collectionMovie.Movie.CertificationMovies)
                .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country)
                .Select(certificationMovie => new ContentRating
                {
                    Rating = certificationMovie.Certification.Rating,
                    Iso31661 = certificationMovie.Certification.Iso31661
                });
        }
    }
}

