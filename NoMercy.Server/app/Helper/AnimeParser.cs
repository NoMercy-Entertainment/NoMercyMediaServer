using System.Text.RegularExpressions;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace NoMercy.Server.app.Helper;

public class AnimeParser
{
    private static readonly Regex NameRegex = new(@"^\[([^\s\[\]]*?)\](?:[\s_\.]+)?([^\[\]]+?)(?:[\s_\.]+)?-?(?:[\s_\.]+)?(?:(?:S(\d+))?(?:[\s_.]+)?-?(?:[\s_.]+)E?([0-9\.]+)(?:v[0-9]+)?(?:[\s_\.]+)?([^\(\[\]\)]+?)?(?:[\s_\.]+)?)?(?:[\(\[](.*?)[\]\)])?(?:[\s_\.]+)?(?:\[([a-fA-F0-9]+)\])\.([a-zA-Z]+)$");

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
        var match = NameRegex.Match(filename.Trim());
        if (!match.Success)
            return new AnimeInfo { FileName = filename };

        var info = new AnimeInfo
        {
            FileName = filename,
            Group = match.Groups[1].Value,
            Name = match.Groups[2].Value.Replace("_", " "),
            Season = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null,
            Episode = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : null,
            Title = match.Groups[5].Success ? match.Groups[5].Value : null,
            ExtraInfo = match.Groups[6].Success ? match.Groups[6].Value : null,
            Checksum = match.Groups[7].Success ? match.Groups[7].Value : null,
            Extension = match.Groups[8].Success ? match.Groups[8].Value : null
        };

        return info;
    }
}

public class AnimeInfo
{
    public string FileName { get; set; }
    public string Group { get; set; }
    public string Name { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? Title { get; set; }
    public string? ExtraInfo { get; set; }
    public string? Checksum { get; set; }
    public string? Extension { get; set; }
}