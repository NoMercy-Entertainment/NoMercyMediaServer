using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using Collection = NoMercy.Database.Models.Collection;


namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record CollectionResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; } = null!;

    [JsonProperty("data")] public CollectionResponseItemDto? Data { get; set; }

    public static readonly Func<MediaContext, Guid, int, string, string, Task<Collection?>> GetCollection =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, int id, string language, string country) =>
            mediaContext.Collections.AsNoTracking()
                .Where(collection => collection.Id == id)
                .Where(collection => collection.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(collection => collection.CollectionUser
                    .Where(x => x.UserId.Equals(userId))
                )
                .Include(collection => collection.Library)
                .ThenInclude(library => library.LibraryUsers)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.VideoFiles)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.MovieUser
                    .Where(x => x.UserId.Equals(userId))
                )
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.CertificationMovies
                    .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US" ||
                                                 certificationMovie.Certification.Iso31661 == country)
                )
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.GenreMovies)
                .ThenInclude(genreMovie => genreMovie.Genre)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Cast)
                .ThenInclude(genreMovie => genreMovie.Person)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Cast)
                .ThenInclude(genreMovie => genreMovie.Role)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Crew)
                .ThenInclude(genreMovie => genreMovie.Job)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Crew)
                .ThenInclude(genreMovie => genreMovie.Person)
                .Include(collection => collection.CollectionMovies)
                .ThenInclude(movie => movie.Movie)
                .ThenInclude(movie => movie.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                    .OrderByDescending(image => image.VoteAverage)
                    .Take(30)
                )
                .Include(collection => collection.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(collection => collection.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                    .OrderByDescending(image => image.VoteAverage)
                )
                .FirstOrDefault());
}
