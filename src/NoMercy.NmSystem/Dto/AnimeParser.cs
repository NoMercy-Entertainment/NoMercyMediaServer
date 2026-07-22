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

using System.Text.RegularExpressions;

namespace NoMercy.NmSystem.Dto;

public class AnimeParser
{
    private static readonly Regex NameRegex = new(
        pattern: @"^\[([^\s\[\]]*?)\](?:[\s_\.]+)?([^\[\]]+?)(?:[\s_\.]+)?-?(?:[\s_\.]+)?(?:(?:S(\d+))?(?:[\s_.]+)?-?(?:[\s_.]+)E?([0-9\.]+)(?:v[0-9]+)?(?:[\s_\.]+)?([^\(\[\]\)]+?)?(?:[\s_\.]+)?)?(?:[\(\[](.*?)[\]\)])?(?:[\s_\.]+)?(?:\[([a-fA-F0-9]+)\])\.([a-zA-Z]+)$"
    );

    /// <summary>
    /// This function parses video filenames to determine information about the series or movie they are a part of.
    /// </summary>
    /// <param name="filename">The filename to parse.</param>
    /// <returns>An object containing the following properties:
    /// - `Name`: The name of the series or movie.
    /// - `Season`: The season number.
    /// - `Episode`: The episode number.
    /// - `Title`: The title of the episode.
    /// - `ExtraInfo`: Extra information on the video, usually the quality.
    /// - `Checksum`: The official checksum of the video.
    /// - `Extension`: The extension of the video.
    /// - `Group`: The publishing group of the video, usually the fansubber.
    /// - `FileName`: The filename of the video.
    /// </returns>
    public static AnimeInfo ParseAnimeFilename(string filename)
    {
        Match match = NameRegex.Match(input: filename.Trim());
        if (!match.Success)
            return new() { FileName = filename };

        AnimeInfo info = new()
        {
            FileName = filename,
            Group = match.Groups[groupnum: 1].Value,
            Name = match.Groups[groupnum: 2].Value.Replace(oldValue: "_", newValue: " "),
            Season = match.Groups[groupnum: 3].Success ? int.Parse(s: match.Groups[groupnum: 3].Value) : null,
            Episode = match.Groups[groupnum: 4].Success ? int.Parse(s: match.Groups[groupnum: 4].Value) : null,
            Title = match.Groups[groupnum: 5].Success ? match.Groups[groupnum: 5].Value : null,
            ExtraInfo = match.Groups[groupnum: 6].Success ? match.Groups[groupnum: 6].Value : null,
            Checksum = match.Groups[groupnum: 7].Success ? match.Groups[groupnum: 7].Value : null,
            Extension = match.Groups[groupnum: 8].Success ? match.Groups[groupnum: 8].Value : null,
        };

        return info;
    }
}
