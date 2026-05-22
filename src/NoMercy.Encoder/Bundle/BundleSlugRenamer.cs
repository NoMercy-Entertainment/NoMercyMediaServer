using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Startup-time pass that renames on-disk bundle directories when a built-in
/// preset's display name (and therefore its computed slug) changes between
/// releases.
///
/// For each <c>oldSlug → newSlug</c> pair supplied, the renamer:
/// <list type="bullet">
///   <item>Walks every library folder for <c>encodes/{oldSlug}/</c> directories.</item>
///   <item>Renames the directory to <c>encodes/{newSlug}/</c> via the storage facade.</item>
///   <item>Reads the relocated <c>manifest.json</c> and patches its <c>preset_slug</c> field.</item>
/// </list>
///
/// The pass is idempotent: if no <c>encodes/{oldSlug}/</c> directory exists the
/// pair is silently skipped. If the destination <c>encodes/{newSlug}/</c> already
/// exists a warning is logged and the old directory is left untouched.
/// </summary>
public class BundleSlugRenamer(
    IReadOnlyDictionary<string, string> slugMap,
    IStorageFactory storageFactory,
    MediaContext context
)
{
    /// <summary>
    /// Runs the rename pass. Reads all library folders from the database,
    /// then for each folder checks each slug pair and renames when applicable.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (slugMap.Count == 0)
            return;

        List<Folder> folders = await context
            .Folders.Include(f => f.Driver)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (Folder folder in folders)
        {
            IStorage storage = storageFactory.For(folder.Id, folder.DriverId, folder.Path);
            await RenameInFolderAsync(storage, folder.Path, ct);
        }
    }

    private async Task RenameInFolderAsync(
        IStorage storage,
        string folderPath,
        CancellationToken ct
    )
    {
        foreach (KeyValuePair<string, string> pair in slugMap)
        {
            string oldDir = $"encodes/{pair.Key}";
            string newDir = $"encodes/{pair.Value}";

            bool oldExists = await storage.ExistsAsync(oldDir, ct);
            if (!oldExists)
                continue;

            bool newExists = await storage.ExistsAsync(newDir, ct);
            if (newExists)
            {
                Logger.Setup(
                    $"BundleSlugRenamer: collision — '{newDir}' already exists in '{folderPath}'. "
                        + $"Leaving '{oldDir}' untouched.",
                    LogEventLevel.Warning
                );
                continue;
            }

            try
            {
                await storage.MoveDirectoryAsync(oldDir, newDir, ct);
                Logger.Setup(
                    $"BundleSlugRenamer: renamed '{oldDir}' → '{newDir}' in '{folderPath}'",
                    LogEventLevel.Information
                );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    $"BundleSlugRenamer: failed to rename '{oldDir}' → '{newDir}' in '{folderPath}': {ex.Message}",
                    LogEventLevel.Warning
                );
                continue;
            }

            await PatchManifestAsync(storage, newDir, pair.Value, ct);
        }
    }

    private static async Task PatchManifestAsync(
        IStorage storage,
        string bundleDir,
        string newSlug,
        CancellationToken ct
    )
    {
        string manifestPath = $"{bundleDir}/manifest.json";

        bool manifestExists = await storage.ExistsAsync(manifestPath, ct);
        if (!manifestExists)
            return;

        try
        {
            string json = await storage.ReadAllTextAsync(manifestPath, ct);
            JObject? obj = JsonConvert.DeserializeObject<JObject>(json);
            if (obj is null)
                return;

            obj["preset_slug"] = newSlug;

            string updated = JsonConvert.SerializeObject(obj, Formatting.Indented);
            await storage.WriteAsync(manifestPath, Encoding.UTF8.GetBytes(updated), ct);

            Logger.Setup(
                $"BundleSlugRenamer: patched manifest.json preset_slug → '{newSlug}' in '{bundleDir}'",
                LogEventLevel.Verbose
            );
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"BundleSlugRenamer: failed to patch manifest.json in '{bundleDir}': {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }
}
