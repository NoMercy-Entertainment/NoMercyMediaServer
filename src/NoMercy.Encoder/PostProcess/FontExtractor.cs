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

using System.Text;
using Newtonsoft.Json;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

public class FontExtractor(IStorage storage) : IFontExtractor
{
    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".woff",
        ".woff2",
    };

    private static readonly HashSet<string> LutExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cube",
        ".3dl",
        ".look",
    };

    // FFmpeg dumps attachments via -dump_attachment:t "" which is a pre-input flag.
    // The standard builder does not model pre-input attachment flags, so we build
    // the argument list directly to keep the contract explicit and simple.
    public FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory
    )
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

        string[] args =
        [
            "-y",
            "-hide_banner",
            "-dump_attachment:t",
            "",
            "-i",
            inputPath,
            "-f",
            "null",
            "-",
        ];

        return new(ffmpegPath, args, fontDir);
    }

    public async Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct)
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

        if (!storage.Exists(fontDir))
            return;

        IReadOnlyList<StorageEntry> allDumped = storage
            .List(fontDir, "*", recursive: false)
            .Where(e => !e.IsDirectory)
            .ToList();

        if (allDumped.Count == 0)
        {
            storage.DeleteDirectory(fontDir, recursive: false);
            return;
        }

        List<StorageEntry> fontFiles = allDumped
            .Where(f => IsFontExtension(storage.GetName(f.Path)))
            .ToList();

        List<StorageEntry> lutFiles = allDumped
            .Where(f => IsLutExtension(storage.GetName(f.Path)))
            .ToList();

        await MoveLutsAndWriteManifestAsync(outputDirectory, lutFiles, ct);

        if (fontFiles.Count == 0)
        {
            if (
                !storage.Exists(fontDir)
                || storage.List(fontDir, "*", recursive: false).All(e => e.IsDirectory)
            )
                storage.DeleteDirectory(fontDir, recursive: false);
            return;
        }

        List<AssetEntry> entries = fontFiles
            .Select(f => new AssetEntry(
                File: $"fonts/{storage.GetName(f.Path)}",
                MimeType: GetFontMimeType(storage.GetName(f.Path))
            ))
            .ToList();

        string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
        await storage.WriteAsync(
            storage.CombinePath(outputDirectory, "fonts.json"),
            Encoding.UTF8.GetBytes(json),
            ct
        );
    }

    private async Task MoveLutsAndWriteManifestAsync(
        string outputDirectory,
        List<StorageEntry> lutFiles,
        CancellationToken ct
    )
    {
        if (lutFiles.Count == 0)
            return;

        string lutDir = storage.CombinePath(outputDirectory, "luts");
        storage.CreateDirectory(lutDir);

        List<AssetEntry> lutEntries = [];

        foreach (StorageEntry lutFile in lutFiles)
        {
            string fileName = storage.GetName(lutFile.Path);
            string destination = storage.CombinePath(lutDir, fileName);

            byte[] data = await storage.ReadAsync(lutFile.Path, ct);
            await storage.WriteAsync(destination, data, ct);
            storage.Delete(lutFile.Path);

            lutEntries.Add(
                new(File: $"luts/{fileName}", MimeType: "application/octet-stream")
            );
        }

        string lutsJson = JsonConvert.SerializeObject(lutEntries, Formatting.Indented);
        await storage.WriteAsync(
            storage.CombinePath(outputDirectory, "luts.json"),
            Encoding.UTF8.GetBytes(lutsJson),
            ct
        );
    }

    private static bool IsFontExtension(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        string ext = dot < 0 ? string.Empty : fileName[dot..];
        return FontExtensions.Contains(ext);
    }

    private static bool IsLutExtension(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        string ext = dot < 0 ? string.Empty : fileName[dot..];
        return LutExtensions.Contains(ext);
    }

    private static string GetFontMimeType(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        string ext = dot < 0 ? string.Empty : fileName[dot..].ToLowerInvariant();
        return ext switch
        {
            ".ttf" => "application/x-font-truetype",
            ".otf" => "application/x-font-opentype",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
    }

    // FontEntry + LutEntry shared the same shape (file path + mime type) so collapsing
    // them avoids two definitions drifting apart over time.
    private record AssetEntry(
        [property: JsonProperty("file")] string File,
        [property: JsonProperty("mime_type")] string MimeType
    );
}
