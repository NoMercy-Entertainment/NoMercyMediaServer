using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public class BundleManifestWriter(IStorage storage) : IBundleManifestWriter
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    public async Task WriteAsync(string path, BundleManifest manifest, CancellationToken ct)
    {
        string json = JsonConvert.SerializeObject(manifest, Settings);
        await storage.WriteAsync(path, Encoding.UTF8.GetBytes(json), ct);
    }

    public async Task<BundleManifest?> ReadAsync(string path, CancellationToken ct)
    {
        if (!storage.Exists(path))
            return null;
        byte[] bytes = await storage.ReadAsync(path, ct);
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<BundleManifest>(json, Settings);
    }

    public Task<ReconcileReport> ReconcileAsync(
        string bundleDirectory,
        BundleManifest manifest,
        CancellationToken ct
    )
    {
        IReadOnlyList<StorageEntry> onDisk = storage.List(bundleDirectory, "*", recursive: true);

        string dirPrefix = bundleDirectory.TrimEnd('/') + "/";

        HashSet<string> diskRel = new(StringComparer.OrdinalIgnoreCase);
        foreach (StorageEntry entry in onDisk)
        {
            if (entry.IsDirectory)
                continue;
            string rel = entry.Path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase)
                ? entry.Path[dirPrefix.Length..]
                : entry.Path;
            diskRel.Add(rel);
        }

        HashSet<string> manifestSet = new(manifest.Files, StringComparer.OrdinalIgnoreCase);

        List<string> extra = [.. diskRel.Except(manifestSet)];
        List<string> missing = [.. manifestSet.Except(diskRel)];

        return Task.FromResult(new ReconcileReport(extra, missing));
    }
}
