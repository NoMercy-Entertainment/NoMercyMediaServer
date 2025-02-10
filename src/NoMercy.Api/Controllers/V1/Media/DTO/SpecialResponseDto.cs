
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using Special = NoMercy.Database.Models.Special;

namespace NoMercy.Api.Controllers.V1.Media.DTO;

public record SpecialResponseDto
{
    [JsonProperty("nextId")] public object NextId { get; set; } = null!;

    [JsonProperty("data")] public SpecialResponseItemDto? Data { get; set; }

    #region GetSpecial

    public static readonly Func<MediaContext, Guid, Ulid, string, string, Task<Special?>> GetSpecial =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid id, string language, string country) =>
            mediaContext.Specials
                .AsNoTracking()
                .Where(special => special.Id == id)
                .Include(special => special.Items
                    .OrderBy(specialItem => specialItem.Order)
                )
                .Include(special => special.Items
                    .OrderBy(specialItem => specialItem.Order)
                )
                .ThenInclude(specialItem => specialItem.Episode)
                    .ThenInclude(ep => ep!.Tv)
                .Include(special => special.SpecialUser
                    .Where(specialUser => specialUser.UserId.Equals(userId))
                )
                .FirstOrDefault());

    #endregion

    #region GetSpecialMovies

    public static readonly Func<MediaContext, Guid, IEnumerable<int>, string, string, IAsyncEnumerable<Movie>>
        GetSpecialMovies =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, IEnumerable<int> ids, string language,
                    string country) =>
                mediaContext.Movies.AsNoTracking()
                    .Where(movie => ids.Contains(movie.Id))
                    .Include(movie => movie.CertificationMovies
                        .Where(certification => certification.Certification.Iso31661 == country ||
                                                certification.Certification.Iso31661 == "US")
                    )
                    .ThenInclude(certificationMovie => certificationMovie.Certification)
                    .Include(movie => movie.VideoFiles)
                    .ThenInclude(file => file.UserData
                        .Where(userData => userData.UserId.Equals(userId))
                    )
                    .Include(movie => movie.GenreMovies)
                    .ThenInclude(genreMovie => genreMovie.Genre)
                    .Include(movie => movie.Cast
                        .OrderBy(castTv => castTv.Role.Order)
                    )
                    .ThenInclude(castTv => castTv.Person)
                    .Include(movie => movie.Cast
                        .OrderBy(castTv => castTv.Role.Order)
                    )
                    .ThenInclude(castTv => castTv.Role)
                    .Include(movie => movie.Crew)
                    .ThenInclude(crew => crew.Person)
                    .Include(movie => movie.Crew)
                    .ThenInclude(crew => crew.Job)
                    .Include(movie => movie.Images
                        .Where(image =>
                            (image.Type == "logo" && image.Iso6391 == "en")
                            || ((image.Type == "backdrop" || image.Type == "poster") &&
                                (image.Iso6391 == "en" || image.Iso6391 == null))
                        )
                        .OrderByDescending(image => image.VoteAverage)
                        .Take(2)
                    )
            );

    #endregion

    #region GetSpecialTvs

    public static readonly Func<MediaContext, Guid, IEnumerable<int>, string, string, IAsyncEnumerable<Tv>>
        GetSpecialTvs =
            EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, IEnumerable<int> ids, string language,
                    string country) =>
                mediaContext.Tvs.AsNoTracking()
                    .Where(tv => ids.Contains(tv.Id))
                    .Include(tv => tv.TvUser
                        .Where(tvUser => tvUser.UserId.Equals(userId))
                    )
                    .Include(tv => tv.GenreTvs)
                    .ThenInclude(genreTv => genreTv.Genre)
                    .Include(tv => tv.Cast
                        .OrderBy(castTv => castTv.Role.Order)
                    )
                    .ThenInclude(castTv => castTv.Person)
                    .Include(tv => tv.Cast
                        .OrderBy(castTv => castTv.Role.Order)
                    )
                    .ThenInclude(castTv => castTv.Role)
                    .Include(tv => tv.Crew)
                    .ThenInclude(crew => crew.Person)
                    .Include(tv => tv.Crew)
                    .ThenInclude(crew => crew.Job)
                    .Include(tv => tv.CertificationTvs
                        .Where(certification => certification.Certification.Iso31661 == country ||
                                                certification.Certification.Iso31661 == "US")
                    )
                    .ThenInclude(certificationTv => certificationTv.Certification)
                    .Include(tv => tv.Images
                        .Where(image =>
                            (image.Type == "logo" && image.Iso6391 == "en")
                            || ((image.Type == "backdrop" || image.Type == "poster") &&
                                (image.Iso6391 == "en" || image.Iso6391 == null))
                        )
                        .OrderByDescending(image => image.VoteAverage)
                        .Take(2)
                    )
                    .Include(tv => tv.Episodes)
                    .ThenInclude(episode => episode.VideoFiles)
            );

    #endregion

    #region GetSpecialAvailable

    public static readonly Func<MediaContext, Guid, Ulid, Task<Special?>> GetSpecialAvailable =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid id) =>
            mediaContext.Specials.AsNoTracking()
                .Where(special => special.Id == id)
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.VideoFiles)
                .ThenInclude(file => file.UserData)
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(movie => movie!.VideoFiles)
                .ThenInclude(file => file.UserData)
                .FirstOrDefault());

    #endregion

    #region GetSpecialPlaylist

    public static readonly Func<MediaContext, Guid, Ulid, string, Task<Special?>> GetSpecialPlaylist =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid id, string language) =>
            mediaContext.Specials.AsNoTracking()
                .Where(special => special.Id == id)
                .Include(special => special.Items
                    .OrderBy(specialItem => specialItem.Order)
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.VideoFiles)
                .ThenInclude(file => file.UserData
                    .Where(userData => userData.UserId.Equals(userId))
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.CertificationMovies)
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.MovieUser
                    .Where(movieUser => movieUser.UserId.Equals(userId))
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.VideoFiles)
                .ThenInclude(file => file.UserData
                    .Where(userData => userData.UserId.Equals(userId))
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Season)
                .ThenInclude(episode => episode.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Tv)
                .ThenInclude(episode => episode.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Tv)
                .ThenInclude(episode => episode.CertificationTvs)
                .ThenInclude(certificationTv => certificationTv.Certification)
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Tv)
                .ThenInclude(episode => episode.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                    .OrderByDescending(image => image.VoteAverage)
                )
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Tv)
                .ThenInclude(episode => episode.TvUser
                    .Where(tvUser => tvUser.UserId.Equals(userId))
                )
                .FirstOrDefault());

    public SpecialResponseDto(Special special)
    {
        //
    }

    public SpecialResponseDto()
    {
        //
    }

    #endregion
}
