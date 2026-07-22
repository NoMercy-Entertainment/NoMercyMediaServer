// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.Json;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Plugins;

public static class PluginManifestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static PluginManifest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: json);

        PluginManifest? manifest = JsonSerializer.Deserialize<PluginManifest>(json: json, options: JsonOptions);

        if (manifest is null)
        {
            throw new InvalidOperationException(message: "Failed to deserialize plugin manifest.");
        }

        Validate(manifest: manifest);
        return manifest;
    }

    public static async Task<PluginManifest> ParseFileAsync(
        string filePath,
        IStorage storage,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: filePath);
        ArgumentNullException.ThrowIfNull(argument: storage);

        if (!storage.Exists(path: filePath))
        {
            throw new FileNotFoundException(message: $"Plugin manifest not found: {filePath}", fileName: filePath);
        }

        string json = await storage.ReadAllTextAsync(path: filePath, ct: ct);
        return Parse(json: json);
    }

    public static PluginInfo ToPluginInfo(
        PluginManifest manifest,
        string assemblyPath,
        PluginStatus status,
        string? manifestPath = null,
        bool verified = false,
        bool trusted = false
    )
    {
        ArgumentNullException.ThrowIfNull(argument: manifest);

        Version version = Version.Parse(input: manifest.Version);

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
            ManifestPath = manifestPath,
            Verified = verified,
            Trusted = trusted,
            Capabilities = manifest.Capabilities,
        };
    }

    private static void Validate(PluginManifest manifest)
    {
        if (manifest.Id == Guid.Empty)
        {
            throw new InvalidOperationException(message: "Plugin manifest 'id' must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(value: manifest.Name))
        {
            throw new InvalidOperationException(message: "Plugin manifest 'name' is required.");
        }

        if (string.IsNullOrWhiteSpace(value: manifest.Version))
        {
            throw new InvalidOperationException(message: "Plugin manifest 'version' is required.");
        }

        if (!Version.TryParse(input: manifest.Version, result: out _))
        {
            throw new InvalidOperationException(
                message: $"Plugin manifest 'version' is not a valid version string: '{manifest.Version}'."
            );
        }

        if (string.IsNullOrWhiteSpace(value: manifest.Description))
        {
            throw new InvalidOperationException(message: "Plugin manifest 'description' is required.");
        }

        if (string.IsNullOrWhiteSpace(value: manifest.Assembly))
        {
            throw new InvalidOperationException(message: "Plugin manifest 'assembly' is required.");
        }
    }
}
