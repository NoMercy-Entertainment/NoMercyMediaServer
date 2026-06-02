using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.Repositories;

public interface ILanguageRepository
{
    Task<List<Language>> GetLanguagesAsync();

    Task<List<LanguageLibrary>> GetLanguagesAsync(string[] list);
}
