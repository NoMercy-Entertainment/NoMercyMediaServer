using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Server.app.Http.Controllers.Api.V1.DTO;

public class CollectionResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; }

    [JsonProperty("data")] public CollectionResponseItemDto? Data { get; set; }
}

public class CollectionResponseItemDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("mediaType")] public string MediaType { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("collection")] public CollectionMovieDto[] Collection { get; set; }
    [JsonProperty("numberOfEpisodes")] public int? NumberOfEpisodes { get; set; }
    [JsonProperty("haveEpisodes")] public int? HaveEpisodes { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    
    [JsonProperty("cast")] public PeopleDto[] Cast { get; set; }
    [JsonProperty("crew")] public PeopleDto[] Crew { get; set; }
    [JsonProperty("backdrops")] public ImageDto[] Backdrops { get; set; }
    [JsonProperty("posters")] public ImageDto[] Posters { get; set; }
    
    [JsonProperty("contentRatings")] public ContentRating[] ContentRatings { get; set; }

    public CollectionResponseItemDto(Collection collection, string country)
    {
        string title = collection.Translations
            .FirstOrDefault()?.Title ?? collection.Title;

        string overview = collection.Translations
            .FirstOrDefault()?.Overview ?? collection.Overview ?? string.Empty;

        Id = collection.Id;
        Title = title;
        Overview = overview;
        Backdrop = collection.Backdrop;
        Poster = collection.Poster;
        TitleSort = collection.TitleSort;
        Type = "collection";
        MediaType = "collection";
        ColorPalette = collection.ColorPalette;
        NumberOfEpisodes = collection.Parts;
        HaveEpisodes = collection.CollectionMovies.Count(collectionMovie => collectionMovie.Movie.VideoFiles.Count > 0);
        Favorite = collection.CollectionUser.Count != 0;

        ContentRatings = collection.CollectionMovies
            .Where(collectionMovie => collectionMovie?.Movie?.CertificationMovies.Count > 0)
            .Select(certificationCollection => certificationCollection.Movie.CertificationMovies)
            .Select(certificationCollection => new ContentRating
                {
                    Rating = certificationCollection
                        .First(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country).Certification.Rating,
                    Iso31661 = certificationCollection
                        .First(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country).Certification.Iso31661
                })
                .Where(certification => certification.Iso31661 == "US" || certification.Iso31661 == country)
            .ToArray();

        Collection = collection.CollectionMovies
            .OrderBy(movie => movie.Movie.ReleaseDate)
            .Select(movie => new CollectionMovieDto(movie.Movie, country))
            .ToArray();

        Backdrops = collection.CollectionMovies
            .SelectMany(movie => movie.Movie.Images)
            .Where(media => media.Type == "backdrop")
            .Select(media => new ImageDto(media))
            .ToArray();

        Posters = collection.CollectionMovies
            .SelectMany(movie => movie.Movie.Images)
            .Where(media => media.Type == "poster")
            .Select(media => new ImageDto(media))
            .ToArray();
        
        Cast = collection.CollectionMovies
            .SelectMany(movie => movie.Movie.Cast)
            .Select(cast => new PeopleDto(cast))
            .ToArray();
        //
        // Crew = collection.CollectionMovies
        //     .SelectMany(movie => movie.Movie.Crew)
        //     .Select(crew => new PeopleDto(crew))
        //     .ToArray();

    }

}

public class CollectionMovieDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("mediaType")] public string MediaType { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("genres")] public GenreDto[] Genres { get; set; }

    [JsonProperty("rating", NullValueHandling = NullValueHandling.Ignore)]
    public Certification? Rating { get; set; }

    [JsonProperty("videoId", NullValueHandling = NullValueHandling.Ignore)]
    public string? VideoId { get; set; }
    
    [JsonProperty("numberOfEpisodes")] public int? NumberOfEpisodes { get; set; }
    [JsonProperty("haveEpisodes")] public int? HaveEpisodes { get; set; }

    public CollectionMovieDto(Movie movie, string country)
    {
        string title = movie.Translations
            .FirstOrDefault()?.Title ?? movie.Title;
        
        string overview = movie.Translations
            .FirstOrDefault()?.Overview ?? movie.Overview ?? string.Empty;

        Id = movie.Id;
        Title = title;
        Overview = overview;
        Backdrop = movie.Backdrop;
        Favorite = movie.MovieUser.Count != 0;
        // Watched = movie.Watched;
        Logo = movie.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;

        MediaType = "movie";
        ColorPalette = movie.ColorPalette;
        Poster = movie.Poster;
        Type = "movie";
        Year = movie.ReleaseDate.ParseYear();
        Genres = movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie.Genre))
            .ToArray();

        Rating = movie.CertificationMovies
            .Select(certificationMovie => certificationMovie.Certification)
            .FirstOrDefault(certification => certification.Iso31661 == "US" || certification.Iso31661 == country);
        
        NumberOfEpisodes = 1;
        HaveEpisodes = movie.VideoFiles.Count > 0 ? 1 : 0;

        VideoId = movie.Video;
    }
}