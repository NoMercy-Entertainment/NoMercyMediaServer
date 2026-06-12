using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.Repositories;

public class LanguageRepository(MediaContext context) : ILanguageRepository
{
    public async Task<List<Language>> GetLanguagesAsync()
    {
        return await context.Languages.ToListAsync();
    }

    public Task<List<Country>> GetCountriesAsync(CancellationToken ct = default)
    {
        return context.Countries.AsNoTracking().ToListAsync(ct);
    }

    public Task<List<LanguageLibrary>> GetLanguagesAsync(string[] list)
    {
        return context
            .LanguageLibrary.Where(language => list.Contains(language.Language.Iso6391))
            .ToListAsync();
    }
}
