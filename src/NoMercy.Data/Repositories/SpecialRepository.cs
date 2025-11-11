using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class SpecialRepository(MediaContext context)
{
    public async Task<List<Special>> GetSpecialsAsync(Guid userId, string language, int take, int page)
    {
        IOrderedQueryable<Special> query = context.Specials
            .AsNoTracking()
            .Include(special => special.Items)
            .ThenInclude(item => item.Episode)
            .ThenInclude(episode => episode!.Tv)
            .Include(special => special.Items)
            .ThenInclude(item => item.Episode)
            .ThenInclude(episode => episode!.VideoFiles)
            .Include(special => special.Items)
            .ThenInclude(item => item.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .OrderBy(special => special.TitleSort);

        List<Special> collections = await query
            .Skip(page * take)
            .Take(take)
            .ToListAsync();

        return collections;
    }

    public Task<Special?> GetSpecialAsync(Guid userId, Ulid id)
    {
        return Task.FromResult(context.Specials
            .AsNoTracking()
            .Where(special => special.Id == id)
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Episode)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.SpecialUser
                .Where(specialUser => specialUser.UserId.Equals(userId))
            )
            .FirstOrDefault());
    }

    public IQueryable<Special> GetSpecialItems(Guid userId, string? language, int take = 1, int page = 1,
        Expression<Func<Special, object>>? orderByExpression = null, string? direction = null)
    {
        IIncludableQueryable<Special, IEnumerable<SpecialUser>> x = context.Specials
            .AsNoTracking()
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.Items
                .OrderBy(specialItem => specialItem.Order)
            )
            .ThenInclude(specialItem => specialItem.Episode)
            .ThenInclude(movie => movie!.VideoFiles)
            .ThenInclude(file => file.UserData
                .Where(userData => userData.UserId.Equals(userId))
            )
            .Include(special => special.SpecialUser
                .Where(specialUser => specialUser.UserId.Equals(userId))
            );

        if (orderByExpression is not null && direction == "desc")
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        if (orderByExpression is not null)
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);

        return x.OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take);
    }

    public readonly Func<MediaContext, Guid, Ulid, string, string, Task<Special?>> GetSpecialPlaylist =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Ulid id, string language, string country) =>
            mediaContext.Specials.AsNoTracking()
                .Where(special => special.Id == id)
                .Include(special => special.Items
                    .OrderBy(specialItem => specialItem.Order)
                )
                
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.VideoFiles)
                .ThenInclude(file => file.Metadata)
                
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie!.VideoFiles)
                .ThenInclude(file => file.UserData
                    .Where(userData => userData.UserId.Equals(userId) && userData.Type == "specials")
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
                .ThenInclude(specialItem => specialItem.Movie)
                .ThenInclude(movie => movie.CertificationMovies
                    .Where(certification => certification.Certification.Iso31661 == country ||
                                            certification.Certification.Iso31661 == "US"))
                .ThenInclude(certificationMovie => certificationMovie.Certification)
                
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.VideoFiles)
                .ThenInclude(videoFile => videoFile.Metadata)
                
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
                
                .Include(special => special.Items)
                .ThenInclude(specialItem => specialItem.Episode)
                .ThenInclude(episode => episode!.Tv)
                .ThenInclude(tv => tv.CertificationTvs
                    .Where(certification => certification.Certification.Iso31661 == country ||
                                            certification.Certification.Iso31661 == "US"))
                .ThenInclude(certificationTv => certificationTv.Certification)
                .FirstOrDefault());
}