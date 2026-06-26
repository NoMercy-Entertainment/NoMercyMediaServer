using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Information;

namespace NoMercy.Data.Repositories;

public class UserDataRepository(IDbContextFactory<MediaContext> contextFactory)
    : IUserDataRepository
{
    public async Task<List<UserData>> GetUserDataAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        IQueryable<UserData>? query = BuildQuery(context, userId, type, intId, ulidId);
        return query is null ? [] : await query.ToListAsync(ct);
    }

    public async Task<UserData?> GetUserDataSingleAsync(
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        IQueryable<UserData>? query = BuildQuery(context, userId, type, intId, ulidId);
        return query is null ? null : await query.FirstOrDefaultAsync(ct);
    }

    public async Task<int> DeleteUserDataAsync(
        List<UserData> userData,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        context.UserData.RemoveRange(userData);
        return await context.SaveChangesAsync(ct);
    }

    private IQueryable<UserData>? BuildQuery(
        MediaContext context,
        Guid userId,
        string type,
        int? intId,
        Ulid? ulidId
    )
    {
        IQueryable<UserData> query = context
            .UserData.AsNoTracking()
            .Where(data => data.UserId.Equals(userId));

        return type switch
        {
            Config.MovieMediaType => query.Where(data => data.MovieId == intId),
            Config.TvMediaType => query.Where(data => data.TvId == intId),
            Config.SpecialMediaType => query.Where(data => data.SpecialId == ulidId),
            Config.CollectionMediaType => query.Where(data => data.CollectionId == intId),
            _ => null,
        };
    }
}
