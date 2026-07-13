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
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // Replace invalid file system characters with dots
        string invalidChars = $"{string.Join("", Path.GetInvalidFileNameChars())}:?*<>|\"";
        string pattern = $"[{Regex.Escape(invalidChars)}]";
        name = Regex.Replace(name, pattern, ".");

        // Replace whitespace with dots
        name = Regex.Replace(name, @"\s+", ".");

        // Replace special characters and symbols in a single pass
        StringBuilder sb = new(name.Length + 16);
        foreach (char c in name)
        {
            switch (c)
            {
                case '\u2010': // Hyphen
                case '\u2013': // En dash
                case '\u2014': // Em dash
                case '\u2212': // Minus sign
                    sb.Append('-');
                    break;
                case '\u00B0': // Degree sign
                    sb.Append(".Degrees");
                    break;
                case '&':
                    sb.Append("and");
                    break;
                case '!':
                case '?':
                case '~':
                case '`':
                    sb.Append('.');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        name = sb.ToString();

        // Replace any remaining non-ASCII characters with dots
        name = Regex.Replace(name, @"[^\u0000-\u007F\u00C0-\u017F\u0100-\u024F]+", ".");

        // Collapse multiple dots
        name = Regex.Replace(name, @"\.+", ".");

        // Remove leading/trailing dots
        name = name.Trim('.');

        return name;
    }

    public static string SanitizeFileName(this string filePath)
    {
        string directory = Path.GetDirectoryName(filePath).OrEmpty();
        string fileName = Path.GetFileName(filePath);

        // Replace problematic Unicode characters with ASCII equivalents
        fileName = fileName
            .Replace('\u2019', '\'') // Right single quote
            .Replace('\u2018', '\'') // Left single quote
            .Replace('\u201C', '"') // Left double quote
            .Replace('\u201D', '"') // Right double quote
            .Replace('\u2013', '-') // En dash
            .Replace('\u2014', '-'); // Em dash

        // Normalize to decomposed form (separates combined characters)
        fileName = fileName.Normalize(NormalizationForm.FormKD);

        return Path.Combine(directory, fileName);
    }

    public static string DirectorySafeName(this string? self)
    {
        if (string.IsNullOrEmpty(self))
            return string.Empty;
        string name = Regex.Replace(self, @"[/\\|:*?\""<>{}]", " ");
        return name.Trim().SanitizeFileName();
    }

    public static string MusicBrainzSafeName(this string? self)
    {
        if (string.IsNullOrEmpty(self))
            return string.Empty;
        string name = Regex.Replace(self, @"[/\\|:*?\""<>{}]", "_");
        return name.Trim().SanitizeFileName();
    }

    public static string CleanFileName(this string? self)
    {
        return _cleanFileName(self);
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
    /// value is cut on a token boundary and stamped with a short, stable digest of
    /// the ORIGINAL value, so two different long titles that share a prefix can
    /// never resolve to the same folder. Deterministic — the same title always
    /// produces the same shortened form, so a later rescan reconstructs the
    /// identical path.
    /// </summary>
    public static string Shorten(this string? self, int maxLength = MaxTitleComponentLength)
    {
        if (string.IsNullOrEmpty(self) || self.Length <= maxLength)
            return self ?? string.Empty;

        string digest = _stableTitleDigest(self);
        int keep = Math.Max(1, maxLength - digest.Length - 1);

        string head = self[..keep];
        int boundary = head.LastIndexOfAny(TitleTokenBoundaries);
        if (boundary >= keep / 2)
            head = head[..boundary];

        return head.Trim('.', ' ', '-', '_') + "." + digest;
    }

    /// <summary>
    /// FNV-1a 32-bit digest as 8 lowercase hex chars. Non-cryptographic and stable
    /// across runs (unlike <see cref="string.GetHashCode()"/>); used purely to keep
    /// two different long titles from colliding on disk after truncation.
    /// </summary>
    private static string _stableTitleDigest(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash.ToString("x8");
    }

    public static string NormalizeForComparison(this string name)
    {
        name = name.Replace("&", "and");
        return Regex.Replace(name, @"[^a-zA-Z0-9]", "").ToLowerInvariant();
    }

    public static string? FindMatchingDirectory(
        IStorageDriver driver,
        string rootPath,
        string expectedFolderName
    )
    {
        if (!driver.DirectoryExists(rootPath))
            return null;

        string normalizedExpected = expectedFolderName.NormalizeForComparison();

        foreach (
            string dir in driver.EnumerateFileSystemEntries(
                rootPath,
                "*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            if (!driver.DirectoryExists(dir))
                continue;
            string dirName = Path.GetFileName(dir).OrEmpty();
            if (dirName.NormalizeForComparison() == normalizedExpected)
                return dir;
        }

        return null;
    }
}
