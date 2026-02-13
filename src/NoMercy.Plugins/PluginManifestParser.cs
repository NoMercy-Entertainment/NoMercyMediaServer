using System.Text.Json;
using NoMercy.Plugins.Abstractions;

namespace NoMercy.Plugins;

public static class PluginManifestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PluginManifest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        PluginManifest? manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

        if (manifest is null)
        {
            throw new InvalidOperationException("Failed to deserialize plugin manifest.");
        }

        Validate(manifest);
        return manifest;
    }

    public static async Task<PluginManifest> ParseFileAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Plugin manifest not found: {filePath}", filePath);
        }

        string json = await File.ReadAllTextAsync(filePath, ct);
        return Parse(json);
    }

    public static PluginInfo ToPluginInfo(PluginManifest manifest, string assemblyPath, PluginStatus status, string? manifestPath = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Version version = Version.Parse(manifest.Version);

        return new()
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            Version = version,
            Status = status,
            Author = manifest.Author,
            ProjectUrl = manifest.ProjectUrl,
            AssemblyPath = assemblyPath,
            TargetAbi = manifest.TargetAbi,
            ManifestPath = manifestPath
        };
    }

    private static void Validate(PluginManifest manifest)
    {
        if (manifest.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Plugin manifest 'id' must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidOperationException("Plugin manifest 'name' is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Plugin manifest 'version' is required.");
        }

        if (!Version.TryParse(manifest.Version, out _))
        {
            throw new InvalidOperationException($"Plugin manifest 'version' is not a valid version string: '{manifest.Version}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            throw new InvalidOperationException("Plugin manifest 'assembly' is required.");
        }
    }
}
