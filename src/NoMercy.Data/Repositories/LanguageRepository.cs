using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class LanguageRepository(MediaContext context) : ILanguageRepository
{
    public async Task<List<Language>> GetLanguagesAsync()
    {
        return await context.Languages
            .ToListAsync();
    }

    public Task<List<LanguageLibrary>> GetLanguagesAsync(string[] list)
    {
        return context.LanguageLibrary
            .Where(language => list.Contains(language.Language.Iso6391))
            .ToListAsync();
    }
}