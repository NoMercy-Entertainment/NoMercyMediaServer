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
using System.Text.RegularExpressions;
using NoMercy.Storage;

namespace NoMercy.NmSystem.Extensions;

public static class FileNameSanitizer
{
    private static string _cleanFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(value: name))
            return "";

        // Replace invalid file system characters with dots
        string invalidChars = $"{string.Join(separator: "", values: Path.GetInvalidFileNameChars())}:?*<>|\"";
        string pattern = $"[{Regex.Escape(str: invalidChars)}]";
        name = Regex.Replace(input: name, pattern: pattern, replacement: ".");

        // Replace whitespace with dots
        name = Regex.Replace(input: name, pattern: @"\s+", replacement: ".");

        // Replace special characters and symbols in a single pass
        StringBuilder sb = new(capacity: name.Length + 16);
        foreach (char c in name)
        {
            switch (c)
            {
                case '\u2010': // Hyphen
                case '\u2013': // En dash
                case '\u2014': // Em dash
                case '\u2212': // Minus sign
                    sb.Append(value: '-');
                    break;
                case '\u00B0': // Degree sign
                    sb.Append(value: ".Degrees");
                    break;
                case '&':
                    sb.Append(value: "and");
                    break;
                case '!':
                case '?':
                case '~':
                case '`':
                    sb.Append(value: '.');
                    break;
                default:
                    sb.Append(value: c);
                    break;
            }
        }

        name = sb.ToString();

        // Replace any remaining non-ASCII characters with dots
        name = Regex.Replace(input: name, pattern: @"[^\u0000-\u007F\u00C0-\u017F\u0100-\u024F]+", replacement: ".");

        // Collapse multiple dots
        name = Regex.Replace(input: name, pattern: @"\.+", replacement: ".");

        // Remove leading/trailing dots
        name = name.Trim(trimChar: '.');

        return name;
    }

    public static string SanitizeFileName(this string filePath)
    {
        string directory = Path.GetDirectoryName(path: filePath).OrEmpty();
        string fileName = Path.GetFileName(path: filePath);

        // Replace problematic Unicode characters with ASCII equivalents
        fileName = fileName
            .Replace(oldChar: '\u2019', newChar: '\'') // Right single quote
            .Replace(oldChar: '\u2018', newChar: '\'') // Left single quote
            .Replace(oldChar: '\u201C', newChar: '"') // Left double quote
            .Replace(oldChar: '\u201D', newChar: '"') // Right double quote
            .Replace(oldChar: '\u2013', newChar: '-') // En dash
            .Replace(oldChar: '\u2014', newChar: '-'); // Em dash

        // Normalize to decomposed form (separates combined characters)
        fileName = fileName.Normalize(normalizationForm: NormalizationForm.FormKD);

        return Path.Combine(path1: directory, path2: fileName);
    }

    public static string DirectorySafeName(this string? self)
    {
        if (string.IsNullOrEmpty(value: self))
            return string.Empty;
        string name = Regex.Replace(input: self, pattern: @"[/\\|:*?\""<>{}]", replacement: " ");
        return name.Trim().SanitizeFileName();
    }

    public static string MusicBrainzSafeName(this string? self)
    {
        if (string.IsNullOrEmpty(value: self))
            return string.Empty;
        string name = Regex.Replace(input: self, pattern: @"[/\\|:*?\""<>{}]", replacement: "_");
        return name.Trim().SanitizeFileName();
    }

    public static string CleanFileName(this string? self)
    {
        return _cleanFileName(name: self);
    }

    /// <summary>
    /// Default cap for a single title component inside a generated path segment.
    /// The show title is embedded in the show folder, the episode/season folder
    /// AND the filename, and the V3 encoder nests an HLS bundle (~90 chars) under
    /// that directory — so an unbounded anime-length title blows past Windows'
    /// 260-char path limit and playback fails. 50 leaves the overwhelming majority
    /// of real titles untouched (so existing on-disk names never move) while
    /// bounding the pathological ones.
    /// </summary>
    public const int MaxTitleComponentLength = 50;

    private static readonly char[] TitleTokenBoundaries = [' ', '.', '-', '_'];

    /// <summary>
    /// Bounds a single title component to <paramref name="maxLength"/> characters
    /// without mingling it with the season/episode markers or the episode title it
    /// gets concatenated with. A value already within the limit is returned
    /// unchanged, so short titles keep their exact existing paths. An over-long
    /// value is cut on a token boundary, which keeps the name readable — it is
    /// still the start of the real title, just fewer words of it.
    ///
    /// Nothing is appended to disambiguate. The name this lands in already
    /// carries what separates two media items: an episode has its SxxEyy marker,
    /// a movie its release year. A digest here bought nothing they don't already
    /// provide, cost nine of the fifty characters, and turned a title into
    /// something unreadable.
    ///
    /// Deterministic — the same title always produces the same shortened form, so
    /// a later rescan reconstructs the identical path.
    /// </summary>
    public static string Shorten(this string? self, int maxLength = MaxTitleComponentLength)
    {
        if (string.IsNullOrEmpty(value: self) || self.Length <= maxLength)
            return self ?? string.Empty;

        string head = self[..maxLength];
        int boundary = head.LastIndexOfAny(anyOf: TitleTokenBoundaries);
        if (boundary >= maxLength / 2)
            head = head[..boundary];

        return head.Trim(trimChars: ['.', ' ', '-', '_']);
    }

    public static string NormalizeForComparison(this string name)
    {
        name = name.Replace(oldValue: "&", newValue: "and");
        return Regex.Replace(input: name, pattern: @"[^a-zA-Z0-9]", replacement: "").ToLowerInvariant();
    }

    public static string? FindMatchingDirectory(
        IStorageDriver driver,
        string rootPath,
        string expectedFolderName
    )
    {
        if (!driver.DirectoryExists(path: rootPath))
            return null;

        string normalizedExpected = expectedFolderName.NormalizeForComparison();

        foreach (
            string dir in driver.EnumerateFileSystemEntries(
                directory: rootPath,
                searchPattern: "*",
                option: SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!driver.DirectoryExists(path: dir))
                continue;
            string dirName = Path.GetFileName(path: dir).OrEmpty();
            if (dirName.NormalizeForComparison() == normalizedExpected)
                return dir;
        }

        return null;
    }
}
