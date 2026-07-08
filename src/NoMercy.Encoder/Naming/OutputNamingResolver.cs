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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Naming;

public class OutputNamingResolver(IMediaKeyResolver mediaKeys) : IOutputNamingResolver
{
    private const int MaxSlugChars = 24;

    // ------------------------------------------------------------------
    // Bundle-level resolution
    // ------------------------------------------------------------------

    public BundleLayout Resolve(MediaItemRef media, EncodingProfile profile)
    {
        string mediaKey = mediaKeys.ForMedia(media.Type, media.Id);
        string slug = Slugify(profile.Name);
        bool single = IsSingleFileContainer(profile.Container);

        string singleExt = SingleFileExtension(profile.Container);
        string yearSuffix = media.Year is not null ? $".({media.Year})" : string.Empty;
        string singleName = single
            ? $"{media.Title}{yearSuffix}.NoMercy.{singleExt}"
            : string.Empty;

        string bundleDir = single ? string.Empty : $"encodes/{slug}";
        string masterName = single ? string.Empty : $"{mediaKey}_master.m3u8";
        string manifestPath = single
            ? $"{TrimExt(singleName)}.manifest.json"
            : $"{bundleDir}/manifest.json";
        string reconstructionPath = single
            ? $"{TrimExt(singleName)}.reconstruction.json"
            : $"{bundleDir}/reconstruction.json";

        return new(
            mediaKey,
            slug,
            single,
            bundleDir,
            masterName,
            manifestPath,
            reconstructionPath,
            singleName,
            PresetId: profile.Id.ToString(),
            PresetName: profile.Name,
            ContainerString: ContainerToString(profile.Container)
        );
    }

    // ------------------------------------------------------------------
    // Per-output path helpers
    // ------------------------------------------------------------------

    public string VideoVariantPath(BundleLayout layout, string label, string filename) =>
        $"{layout.BundleDirectory}/video/{label}/{layout.MediaKey}_{label}_{filename}";

    public string VideoSegmentPath(BundleLayout layout, string label, int seq) =>
        $"{layout.BundleDirectory}/video/{label}/{layout.MediaKey}_{label}_{seq:D5}.m4s";

    public string AudioPlaylistPath(BundleLayout layout, string language, string codec) =>
        $"{layout.BundleDirectory}/audio/{language}-{codec}/{layout.MediaKey}_{language}_{codec}.m3u8";

    public string AudioInitPath(BundleLayout layout, string language, string codec) =>
        $"{layout.BundleDirectory}/audio/{language}-{codec}/{layout.MediaKey}_{language}_{codec}_init.mp4";

    public string AudioSegmentPath(BundleLayout layout, string language, string codec, int seq) =>
        $"{layout.BundleDirectory}/audio/{language}-{codec}/{layout.MediaKey}_{language}_{codec}_{seq:D5}.m4s";

    public string SubtitlePath(BundleLayout layout, string language, string extension) =>
        $"{layout.BundleDirectory}/subs/{layout.MediaKey}_{language}.{extension}";

    public string DerivativePath(BundleLayout layout, string filename) =>
        $"{layout.BundleDirectory}/{filename}";

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static string Slugify(string name)
    {
        string lower = name.ToLowerInvariant();
        char[] chars = new char[lower.Length];
        int j = 0;
        char prev = '\0';
        foreach (char c in lower)
        {
            char next = char.IsLetterOrDigit(c) ? c : '-';
            if (next == '-' && prev == '-')
                continue;
            chars[j++] = next;
            prev = next;
        }
        string slug = new string(chars, 0, j).Trim('-');
        // Trim trailing dashes again after truncation so a name that splits
        // mid-separator (e.g. "Long Name Foo" cut at char 24) doesn't leave a
        // dangling "long-name-" slug.
        return slug.Length <= MaxSlugChars ? slug : slug[..MaxSlugChars].TrimEnd('-');
    }

    private static bool IsSingleFileContainer(Container c) =>
        c
            is Container.Mkv
                or Container.Mp4
                or Container.Mp3
                or Container.Aac
                or Container.Flac
                or Container.Ogg
                or Container.Mka
                or Container.Mks;

    private static string SingleFileExtension(Container c) =>
        c switch
        {
            Container.Mkv => "mkv",
            Container.Mp4 => "mp4",
            Container.Mp3 => "mp3",
            Container.Aac => "aac",
            Container.Flac => "flac",
            Container.Ogg => "ogg",
            Container.Mka => "mka",
            Container.Mks => "mks",
            _ => string.Empty,
        };

    private static string TrimExt(string name)
    {
        int dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[..dot];
    }

    private static string ContainerToString(Container c) =>
        c switch
        {
            Container.Mkv => "mkv",
            Container.Mp4 => "mp4",
            Container.HlsTs => "hls-ts",
            Container.HlsFmp4 => "hls-fmp4",
            Container.Dash => "dash",
            Container.Mp3 => "mp3",
            Container.Aac => "aac",
            Container.Flac => "flac",
            Container.Ogg => "ogg",
            Container.Mka => "mka",
            Container.Mks => "mks",
            Container.AudioHlsTs => "audio-hls-ts",
            Container.AudioHlsFmp4 => "audio-hls-fmp4",
            _ => c.ToString().ToLowerInvariant(),
        };
}
