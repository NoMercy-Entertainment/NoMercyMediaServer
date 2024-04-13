using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Server.app.Http.Controllers.Api.V1.DTO;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Server.app.Http.Controllers.Api.V1.Media.DTO;

public class HomeResponseDto<T>
{
    [JsonProperty("data")] public IEnumerable<GenreRowDto<T>> Data { get; set; } = [];
}

public class GenreRowDto<T>
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("moreLink")] public string? MoreLink { get; set; }
    [JsonProperty("items")] public List<T?> Items { get; set; } = [];
    
    [NotMapped, JsonIgnore]
    [JsonProperty("source")] public IEnumerable<HomeSourceDto?> Source { get; set; }
}

public class HomeSourceDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("mediaType")] public string MediaType { get; set; }
    
    public HomeSourceDto(int id, string type)
    {
        Id = id;
        MediaType = type;
    }
}

public class GenreRowItemDto
{
    [JsonProperty("id")] public int? Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("mediaType")] public string? MediaType { get; set; }
    [JsonProperty("genres")] public GenreDto[]? Genres { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("rating")] public RatingClass? Rating { get; set; }
    
    [JsonProperty("videos")] public VideoDto[]? Videos { get; set; }

    public GenreRowItemDto()
    {
        
    }
    
    public GenreRowItemDto(Movie movie)
    {
        string? title = movie.Translations?.FirstOrDefault()?.Title;
        string? overview = movie.Translations?.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = !string.IsNullOrEmpty(title) 
            ? title 
            : movie.Title;
        Overview = !string.IsNullOrEmpty(overview) 
            ? overview 
            : movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Images.FirstOrDefault(image => image.Type == "logo")?.FilePath;
        TitleSort = movie.Title.TitleSort(movie.ReleaseDate);
        Year = movie.ReleaseDate.ParseYear();
        MediaType = "movie";
        ColorPalette = movie.ColorPalette;
        Videos = movie.Media
            .Where(media => media.Site == "YouTube")
            .Select(media => new VideoDto(media))
            .ToArray();
    }

    public GenreRowItemDto(Tv tv)
    {
        string? title = tv.Translations?.FirstOrDefault()?.Title;
        string? overview = tv.Translations?.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(title) 
            ? title 
            : tv.Title;
        Overview = !string.IsNullOrEmpty(overview) 
            ? overview 
            : tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Images.FirstOrDefault(image => image.Type == "logo")?.FilePath;
        TitleSort = tv.Title.TitleSort(tv.FirstAirDate);
        Type = tv.Type;
        Year = tv.FirstAirDate.ParseYear();
        MediaType = "tv";
        ColorPalette = tv.ColorPalette;
        Videos = tv.Media
            .Where(media => media.Site == "YouTube")
            .Select(media => new VideoDto(media))
            .ToArray();
        
    }
}

public class RatingClass
{
    [JsonProperty("rating")] public string Rating { get; set; }

    [JsonProperty("meaning")] public string Meaning { get; set; }

    [JsonProperty("order")] public long Order { get; set; }

    [JsonProperty("iso_3166_1")] public string Iso31661 { get; set; }

    [JsonProperty("image")] public string Image { get; set; }
}

public abstract class HomeResponseDto
{
    public static readonly Func<MediaContext, Guid, string?, IAsyncEnumerable<Genre>> GetHome =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string? language) =>
            mediaContext.Genres.AsNoTracking()

                .OrderBy(genre => genre.Name)

                .Where(genre =>
                    genre.GenreMovies
                        .Any(g => g.Movie.Library.LibraryUsers
                            .FirstOrDefault(u => u.UserId == userId) != null) ||
                    genre.GenreTvShows
                        .Any(g => g.Tv.Library.LibraryUsers
                            .FirstOrDefault(u => u.UserId == userId) != null))
                
                .Include(genre => genre.GenreMovies
                    .Where(genreTv => genreTv.Movie.VideoFiles
                        .Any(videoFile => videoFile.Folder != null) == true
                    )
                )
                
                .Include(genre => genre.GenreTvShows
                    .Where(genreTv => genreTv.Tv.Episodes
                        .Any(episode => episode.VideoFiles
                            .Any(videoFile => videoFile.Folder != null)
                        ) == true
                    )
                )
            );
    
    public static readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Tv>> GetHomeTvs =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> tvIds, string? language) =>
            mediaContext.Tvs.AsNoTracking()
        
            .Where(tv => tvIds.Contains(tv.Id))
            
            .Include(movie => movie.Translations
                .Where(translation => translation.Iso6391 == language))
            
            .Include(tv => tv.Images
                .Where(image =>image.Type == "logo" && image.Iso6391 == "en")
            )
            
            .Include(movie => movie.Media
                .Where(media => media.Site == "YouTube")
            ));
    
    public static readonly Func<MediaContext, List<int>, string?, IAsyncEnumerable<Movie>> GetHomeMovies =
        EF.CompileAsyncQuery((MediaContext mediaContext, List<int> movieIds, string? language) =>
            mediaContext.Movies.AsNoTracking()
                
            .Where(movie => movieIds.Contains(movie.Id))
        
            .Include(movie => movie.Translations
                .Where(translation => translation.Iso6391 == language))
        
            .Include(movie => movie.Media
                .Where(media => media.Site == "YouTube"))
                    
            .Include(movie => movie.Images
                .Where(image =>image.Type == "logo" && image.Iso6391 == "en")
            ));
    
}
