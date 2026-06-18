using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Information;

namespace NoMercy.MediaProcessing.Images.Palettes.Sources;

public class TrackPaletteSource : IPaletteSource
{
    public string EntityType => "track";

    public async Task<string?> CurrentPaletteAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        return await db
            .Tracks.Where(t => t.Id == id)
            .Select(t => t._colorPalette)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaletteResult> GenerateAsync(
        MediaContext db,
        string entityId,
        CancellationToken ct
    )
    {
        Guid id = Guid.Parse(entityId);
        Track? track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (track is null)
            return PaletteResult.NoImage();

        AlbumTrack? link = await db
            .AlbumTrack.Include(at => at.Album)
            .Where(at => at.TrackId == id)
            .FirstOrDefaultAsync(ct);
        Album? album = link?.Album;

        // A track almost always shares its album's stored cover file — both are
        // written from the same release artwork on import. When the covers match,
        // or the track carries no cover of its own, reuse the album palette rather
        // than re-decoding tens of thousands of identical images.
        bool sharesAlbumArt =
            album is not null && (track.Cover is null || track.Cover == album.Cover);
        if (
            sharesAlbumArt
            && !string.IsNullOrEmpty(album!._colorPalette)
            && album._colorPalette != "{}"
        )
            return PaletteResult.Success(album._colorPalette);

        if (track.Cover is null)
            return PaletteResult.NoImage();

        string filePath = AppFiles.MusicImagesPath + track.Cover;
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
            .Tracks.Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t._colorPalette, json), ct);
    }
}
