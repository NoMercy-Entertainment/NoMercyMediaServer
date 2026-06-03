using NoMercy.Database;

namespace NoMercy.MediaProcessing.Images.Palettes;

public interface IPaletteSource
{
    string EntityType { get; }

    Task<string?> CurrentPaletteAsync(MediaContext db, string entityId, CancellationToken ct);

    Task<PaletteResult> GenerateAsync(MediaContext db, string entityId, CancellationToken ct);

    Task PersistAsync(MediaContext db, string entityId, string json, CancellationToken ct);
}
