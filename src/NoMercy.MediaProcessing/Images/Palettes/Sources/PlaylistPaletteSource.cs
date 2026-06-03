using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class PlaylistPaletteSource : IPaletteSource
{
    public string EntityType => "playlist";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        return await db
            .Playlists.Where(p => p.Id == id)
            .Select(p => p._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        Playlist? playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist is null)
            return PaletteResult.NoImage();
        if (playlist.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + playlist.Cover;
        if (!File.Exists(filePath))
            return PaletteResult.NoImage();

        using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(filePath, ct);
        string json = BaseImageManager.GenerateColorPalette([
            new() { Key = "cover", ImageData = image },
        ]);
        return PaletteResult.Success(json);
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
            .Playlists.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p._colorPalette, json), ct);
    }
}
