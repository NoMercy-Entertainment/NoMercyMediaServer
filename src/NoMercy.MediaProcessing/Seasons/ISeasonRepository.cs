using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Seasons;

public interface ISeasonRepository
{
    public Task StoreAsync(IEnumerable<Season> seasons);
    public Task StoreTranslationsAsync(IEnumerable<Translation> translations);
    public Task StoreImagesAsync(IEnumerable<Image> images);

    public Task<bool> RemoveSeasonAsync(int seasonId);
}