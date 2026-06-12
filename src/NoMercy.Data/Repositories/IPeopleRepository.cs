using NoMercy.Database.Models.People;

namespace NoMercy.Data.Repositories;

public interface IPeopleRepository
{
    Task<List<Person>> GetPeopleAsync(
        Guid userId,
        string language,
        int take,
        int page = 0,
        CancellationToken ct = default
    );

    Task<Person?> GetPersonWithCreditsAsync(int id, CancellationToken ct = default);
}
