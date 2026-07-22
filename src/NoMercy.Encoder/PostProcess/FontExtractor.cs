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
    private static readonly HashSet<string> FontExtensions = new(comparer: StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".ttc",
        ".otf",
        ".woff",
        ".woff2",
    };

    private static readonly HashSet<string> LutExtensions = new(comparer: StringComparer.OrdinalIgnoreCase)
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
        string fontDir = storage.CombinePath(parent: outputDirectory, child: "fonts");

        List<string> args = ["-y", "-hide_banner"];

        HashSet<string> usedNames = new(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (AttachmentInfo attachment in attachments)
        {
            args.Add(item: $"-dump_attachment:{attachment.Index}");
            args.Add(item: ResolveSafeAttachmentName(attachment: attachment, usedNames: usedNames));
        }

        args.Add(item: "-i");
        args.Add(item: inputPath);
        args.Add(item: "-f");
        args.Add(item: "null");
        args.Add(item: "-");

        return new(Executable: ffmpegPath, Arguments: args.ToArray(), WorkingDirectory: fontDir);
    }

    public int CountFontAttachments(IReadOnlyList<AttachmentInfo> attachments)
    {
        int count = 0;
        foreach (AttachmentInfo attachment in attachments)
            if (attachment.Filename is { } name && IsFontExtension(fileName: name))
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
        HashSet<string> usedNames = new(comparer: StringComparer.OrdinalIgnoreCase);
        List<AttachmentDumpTarget> targets = [];
        foreach (AttachmentInfo attachment in attachments)
        {
            string safeName = ResolveSafeAttachmentName(attachment: attachment, usedNames: usedNames);
            targets.Add(item: new(Index: attachment.Index, RelativePath: $"fonts/{safeName}"));
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
        string fontDir = storage.CombinePath(parent: outputDirectory, child: "fonts");

        if (!storage.Exists(path: fontDir))
            return 0;

        IReadOnlyList<StorageEntry> allDumped = storage
            .List(path: fontDir, pattern: "*", recursive: false)
            .Where(predicate: e => !e.IsDirectory)
            .ToList();

        if (allDumped.Count == 0)
        {
            storage.DeleteDirectory(path: fontDir, recursive: false);
            return 0;
        }

        List<StorageEntry> fontFiles = allDumped
            .Where(predicate: f => IsFontExtension(fileName: storage.GetName(path: f.Path)))
            .ToList();

        List<StorageEntry> lutFiles = allDumped
            .Where(predicate: f => IsLutExtension(fileName: storage.GetName(path: f.Path)))
            .ToList();

        await MoveLutsAndWriteManifestAsync(outputDirectory: outputDirectory, lutFiles: lutFiles, ct: ct);

        if (fontFiles.Count == 0)
        {
            if (
                !storage.Exists(path: fontDir)
                || storage.List(path: fontDir, pattern: "*", recursive: false).All(predicate: e => e.IsDirectory)
            )
                storage.DeleteDirectory(path: fontDir, recursive: false);
            return 0;
        }

        List<AssetEntry> entries = fontFiles
            .Select(selector: f => new AssetEntry(
                File: $"fonts/{storage.GetName(path: f.Path)}",
                MimeType: GetFontMimeType(fileName: storage.GetName(path: f.Path))
            ))
            .ToList();

        string json = JsonConvert.SerializeObject(value: entries, formatting: Formatting.Indented);
        await storage.WriteAsync(
            path: storage.CombinePath(parent: outputDirectory, child: "fonts.json"),
            bytes: Encoding.UTF8.GetBytes(s: json),
            ct: ct
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
        int dot = original.LastIndexOf(value: '.');
        string stem = dot < 0 ? original : original[..dot];
        string ext = dot < 0 ? string.Empty : original[dot..];

        string safeStem = Sanitize(value: stem);
        if (safeStem.Length == 0)
            safeStem = $"attachment_{attachment.Index}";

        string candidate = safeStem + Sanitize(value: ext);
        if (usedNames.Add(item: candidate))
            return candidate;

        // Two attachments sanitized to the same name — disambiguate by index.
        string deduped = $"{safeStem}_{attachment.Index}{Sanitize(value: ext)}";
        usedNames.Add(item: deduped);
        return deduped;
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new(capacity: value.Length);
        foreach (char character in value)
            builder.Append(
                value: char.IsLetterOrDigit(c: character) || character is '.' or '-' or '_' ? character : '_'
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

        string lutDir = storage.CombinePath(parent: outputDirectory, child: "luts");
        storage.CreateDirectory(path: lutDir);

        List<AssetEntry> lutEntries = [];

        foreach (StorageEntry lutFile in lutFiles)
        {
            string fileName = storage.GetName(path: lutFile.Path);
            string destination = storage.CombinePath(parent: lutDir, child: fileName);

            byte[] data = await storage.ReadAsync(path: lutFile.Path, ct: ct);
            await storage.WriteAsync(path: destination, bytes: data, ct: ct);
            storage.Delete(path: lutFile.Path);

            lutEntries.Add(item: new(File: $"luts/{fileName}", MimeType: "application/octet-stream"));
        }

        string lutsJson = JsonConvert.SerializeObject(value: lutEntries, formatting: Formatting.Indented);
        await storage.WriteAsync(
            path: storage.CombinePath(parent: outputDirectory, child: "luts.json"),
            bytes: Encoding.UTF8.GetBytes(s: lutsJson),
            ct: ct
        );
    }

    private static bool IsFontExtension(string fileName)
    {
        int dot = fileName.LastIndexOf(value: '.');
        string ext = dot < 0 ? string.Empty : fileName[dot..];
        return FontExtensions.Contains(item: ext);
    }

    private static bool IsLutExtension(string fileName)
    {
        int dot = fileName.LastIndexOf(value: '.');
        string ext = dot < 0 ? string.Empty : fileName[dot..];
        return LutExtensions.Contains(item: ext);
    }

    private static string GetFontMimeType(string fileName)
    {
        int dot = fileName.LastIndexOf(value: '.');
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
        [property: JsonProperty(propertyName: "file")] string File,
        [property: JsonProperty(propertyName: "mime_type")] string MimeType
    );
}
