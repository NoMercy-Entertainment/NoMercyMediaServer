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
        IIncludableQueryable<Special, IEnumerable<SpecialUser>> x =  context.Specials
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
        {
            return x.OrderByDescending(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }
        if (orderByExpression is not null)
        {
            return x.OrderBy(orderByExpression)
                .Skip(page * take)
                .Take(take);
        }

        return x.OrderBy(special => special.TitleSort)
            .Skip(page * take)
            .Take(take);
    }
}
