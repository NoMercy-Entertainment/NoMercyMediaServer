using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class AlbumPaletteSource : IPaletteSource
{
    public string EntityType => "album";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        return await db
            .Albums.Where(a => a.Id == id)
            .Select(a => a._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        Album? album = await db.Albums.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (album is null)
            return PaletteResult.NoImage();
        if (album.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + album.Cover;
        if (!File.Exists(filePath))
            return PaletteResult.NoImage();

        string json = await CoverArtImageManagerManager.ColorPalette("cover", new(filePath));
        return string.IsNullOrWhiteSpace(json)
            ? PaletteResult.NoImage()
            : PaletteResult.Success(json);
    }

    public async Task PersistAsync(
        MediaContext db,
        string entityId,
        string json,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        await db
            .Albums.Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a._colorPalette, json), ct);
    }
}
