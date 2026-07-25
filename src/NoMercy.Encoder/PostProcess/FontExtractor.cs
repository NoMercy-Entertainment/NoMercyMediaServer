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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Commands;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

public class FontExtractor(IStorage storage) : IFontExtractor
{
    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".ttc",
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

    // FFmpeg dumps attachments via -dump_attachment, a pre-input flag. Passing an
    // explicit, sanitized output filename per attachment stream — rather than the
    // bulk -dump_attachment:t "" form, which reuses each attachment's embedded
    // filename — stops a single attachment whose embedded name ffmpeg rejects as
    // "unsafe" (e.g. one containing spaces) from aborting the whole dump and
    // dropping every remaining font. Subtitle rendering matches fonts by their
    // internal family name, not the on-disk filename, so the rename is safe.
    // The standard builder does not model pre-input attachment flags, so we build
    // the argument list directly to keep the contract explicit and simple.
    public FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        IReadOnlyList<AttachmentInfo> attachments
    )
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

        List<string> args = ["-y", "-hide_banner"];

        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentInfo attachment in attachments)
        {
            args.Add($"-dump_attachment:{attachment.Index}");
            args.Add(ResolveSafeAttachmentName(attachment, usedNames));
        }

        args.Add("-i");
        args.Add(inputPath);
        args.Add("-f");
        args.Add("null");
        args.Add("-");

        return new(ffmpegPath, args.ToArray(), fontDir);
    }

    public int CountFontAttachments(IReadOnlyList<AttachmentInfo> attachments)
    {
        int count = 0;
        foreach (AttachmentInfo attachment in attachments)
            if (attachment.Filename is { } name && IsFontExtension(name))
                count++;
        return count;
    }

    // Exposes the same sanitized, collision-free naming ResolveSafeAttachmentName
    // uses internally, but pre-fixed with "fonts/" — ExtractionCommandBuilder wires
    // these straight into -dump_attachment args when it merges attachment dumping
    // with bitmap-subtitle extraction into a single ffmpeg command. Keeping this on
    // IFontExtractor (rather than duplicating the sanitizer as a static call) means
    // a plugin's replacement IFontExtractor controls attachment naming too.
    public IReadOnlyList<AttachmentDumpTarget> ResolveAttachmentDumpTargets(
        IReadOnlyList<AttachmentInfo> attachments
    )
    {
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        List<AttachmentDumpTarget> targets = [];
        foreach (AttachmentInfo attachment in attachments)
        {
            string safeName = ResolveSafeAttachmentName(attachment, usedNames);
            targets.Add(new(attachment.Index, $"fonts/{safeName}"));
        }
        return targets;
    }

    /// <summary>
    /// Writes fonts.json (and moves any LUT attachments to luts/). Returns the
    /// number of font files written to the manifest so the finalize stage can
    /// verify every embedded font was extracted before publishing.
    /// </summary>
    public async Task<int> WriteFontManifestAsync(string outputDirectory, CancellationToken ct)
    {
        string fontDir = storage.CombinePath(outputDirectory, "fonts");

        if (!storage.Exists(fontDir))
            return 0;

        IReadOnlyList<StorageEntry> allDumped = storage
            .List(fontDir, "*", false)
            .Where(e => !e.IsDirectory)
            .ToList();

        if (allDumped.Count == 0)
        {
            storage.DeleteDirectory(fontDir, false);
            return 0;
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
                || storage.List(fontDir, "*", false).All(e => e.IsDirectory)
            )
                storage.DeleteDirectory(fontDir, false);
            return 0;
        }

        List<AssetEntry> entries = fontFiles
            .Select(f => new AssetEntry(
                $"fonts/{storage.GetName(f.Path)}",
                GetFontMimeType(storage.GetName(f.Path))
            ))
            .ToList();

        string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
        await storage.WriteAsync(
            storage.CombinePath(outputDirectory, "fonts.json"),
            Encoding.UTF8.GetBytes(json),
            ct
        );

        return fontFiles.Count;
    }

    // FFmpeg writes each attachment relative to the command working directory
    // (fonts/). Build a sanitized, collision-free filename that preserves the
    // original extension so WriteFontManifestAsync can still classify it as a
    // font or LUT after extraction.
    private static string ResolveSafeAttachmentName(
        AttachmentInfo attachment,
        HashSet<string> usedNames
    )
    {
        string original = attachment.Filename ?? $"attachment_{attachment.Index}";
        int dot = original.LastIndexOf('.');
        string stem = dot < 0 ? original : original[..dot];
        string ext = dot < 0 ? string.Empty : original[dot..];

        string safeStem = Sanitize(stem);
        if (safeStem.Length == 0)
            safeStem = $"attachment_{attachment.Index}";

        string candidate = safeStem + Sanitize(ext);
        if (usedNames.Add(candidate))
            return candidate;

        // Two attachments sanitized to the same name — disambiguate by index.
        string deduped = $"{safeStem}_{attachment.Index}{Sanitize(ext)}";
        usedNames.Add(deduped);
        return deduped;
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
            builder.Append(
                char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_'
            );
        return builder.ToString();
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

            lutEntries.Add(new($"luts/{fileName}", "application/octet-stream"));
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
            ".ttc" => "font/collection",
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
