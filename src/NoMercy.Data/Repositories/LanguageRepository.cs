using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public class LanguageRepository(MediaContext context)
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