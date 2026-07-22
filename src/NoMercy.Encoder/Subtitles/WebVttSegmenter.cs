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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Slices a WebVTT file into per-HLS-segment .vtt files.
/// Each segment covers [N*duration, (N+1)*duration). Cues that straddle a
/// segment boundary are duplicated into both segments (per the HLS subtitle
/// spec, RFC 8216 §3.5).
/// </summary>
public sealed class WebVttSegmenter
{
    private static readonly Regex TimestampLineRx = new(
        pattern: @"^(?:(\d+):)?(\d{2}):(\d{2})\.(\d{3})\s+-->\s+(?:(\d+):)?(\d{2}):(\d{2})\.(\d{3})",
        options: RegexOptions.Compiled
    );

    /// <summary>
    /// Parse <paramref name="vttFilePath"/> and emit one
    /// <see cref="WebVttSegment"/> per segment window up to the last cue end.
    /// </summary>
    public IReadOnlyList<WebVttSegment> Slice(
        string vttFilePath,
        TimeSpan segmentDuration,
        IStorage storage
    )
    {
        if (segmentDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName: nameof(segmentDuration), message: "Must be positive.");

        string raw = Encoding.UTF8.GetString(bytes: storage.Read(path: vttFilePath));
        return SliceContent(vttContent: raw, segmentDuration: segmentDuration);
    }

    /// <summary>
    /// Same as <see cref="Slice(string,TimeSpan)"/> but works on an already-
    /// loaded string — used by tests without a real file.
    /// </summary>
    public IReadOnlyList<WebVttSegment> SliceContent(string vttContent, TimeSpan segmentDuration)
    {
        if (segmentDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName: nameof(segmentDuration), message: "Must be positive.");

        (string header, List<ParsedCue> cues) = Parse(vttContent: vttContent);

        if (cues.Count == 0)
            return
            [
                new(
                    Index: 0,
                    Content: BuildSegment(originalHeader: header, cues: [], segmentDuration: segmentDuration),
                    StartTime: TimeSpan.Zero,
                    EndTime: segmentDuration
                ),
            ];

        TimeSpan totalDuration = cues.Max(selector: c => c.End);
        int segmentCount = (int)Math.Ceiling(a: totalDuration / segmentDuration);
        if (segmentCount == 0)
            segmentCount = 1;

        List<WebVttSegment> result = new(capacity: segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            TimeSpan segStart = segmentDuration * i;
            TimeSpan segEnd = segmentDuration * (i + 1);

            List<ParsedCue> overlapping = cues.Where(predicate: c => c.Start < segEnd && c.End > segStart)
                .ToList();

            string content = BuildSegment(originalHeader: header, cues: overlapping, segmentDuration: segmentDuration);
            result.Add(item: new(Index: i, Content: content, StartTime: segStart, EndTime: segEnd));
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    private static (string header, List<ParsedCue> cues) Parse(string vttContent)
    {
        string[] lines = vttContent.ReplaceLineEndings(replacementText: "\n").Split(separator: '\n');

        StringBuilder headerSb = new();
        List<ParsedCue> cues = [];

        int idx = 0;

        // First line must be "WEBVTT" (possibly with a description after a space/tab).
        if (idx < lines.Length && lines[idx].StartsWith(value: "WEBVTT", comparisonType: StringComparison.Ordinal))
        {
            headerSb.AppendLine(value: lines[idx]);
            idx++;
        }

        // Collect header block lines until the first blank line.
        while (idx < lines.Length && !string.IsNullOrWhiteSpace(value: lines[idx]))
        {
            headerSb.AppendLine(value: lines[idx]);
            idx++;
        }

        // Parse cue blocks.
        while (idx < lines.Length)
        {
            // Skip blank lines between blocks.
            while (idx < lines.Length && string.IsNullOrWhiteSpace(value: lines[idx]))
                idx++;

            if (idx >= lines.Length)
                break;

            // Optional cue identifier (not a timestamp line).
            string? cueId = null;
            if (!TimestampLineRx.IsMatch(input: lines[idx]))
            {
                cueId = lines[idx];
                idx++;
            }

            if (idx >= lines.Length)
                break;

            // Timestamp line.
            if (!TimestampLineRx.IsMatch(input: lines[idx]))
            {
                idx++;
                continue;
            }

            string timestampLine = lines[idx];
            idx++;

            // Payload lines until blank or EOF.
            StringBuilder payload = new();
            while (idx < lines.Length && !string.IsNullOrWhiteSpace(value: lines[idx]))
            {
                if (payload.Length > 0)
                    payload.AppendLine();
                payload.Append(value: lines[idx]);
                idx++;
            }

            if (!TryParseTimestamps(line: timestampLine, start: out TimeSpan start, end: out TimeSpan end))
                continue;

            cues.Add(item: new(Id: cueId, TimestampLine: timestampLine, Start: start, End: end, Payload: payload.ToString()));
        }

        return (headerSb.ToString().TrimEnd(), cues);
    }

    private static bool TryParseTimestamps(string line, out TimeSpan start, out TimeSpan end)
    {
        start = end = TimeSpan.Zero;
        Match m = TimestampLineRx.Match(input: line);
        if (!m.Success)
            return false;

        start = ParseTs(m: m, hourGroupIndex: 1);
        end = ParseTs(m: m, hourGroupIndex: 5);
        return true;
    }

    private static TimeSpan ParseTs(Match m, int hourGroupIndex)
    {
        int hours = m.Groups[groupnum: hourGroupIndex].Success
            ? int.Parse(s: m.Groups[groupnum: hourGroupIndex].Value, provider: CultureInfo.InvariantCulture)
            : 0;
        int minutes = int.Parse(s: m.Groups[groupnum: hourGroupIndex + 1].Value, provider: CultureInfo.InvariantCulture);
        int seconds = int.Parse(s: m.Groups[groupnum: hourGroupIndex + 2].Value, provider: CultureInfo.InvariantCulture);
        int ms = int.Parse(s: m.Groups[groupnum: hourGroupIndex + 3].Value, provider: CultureInfo.InvariantCulture);
        return new(days: 0, hours: hours, minutes: minutes, seconds: seconds, milliseconds: ms);
    }

    // ------------------------------------------------------------------
    // Output
    // ------------------------------------------------------------------

    private static string BuildSegment(
        string originalHeader,
        List<ParsedCue> cues,
        TimeSpan segmentDuration
    )
    {
        StringBuilder sb = new();

        // If the original header already contains X-TIMESTAMP-MAP leave it;
        // otherwise inject the standard HLS timestamp map.
        bool hasTimestampMap = originalHeader.Contains(
            value: "X-TIMESTAMP-MAP",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );

        if (!string.IsNullOrWhiteSpace(value: originalHeader))
        {
            sb.AppendLine(value: originalHeader);
        }
        else
        {
            sb.AppendLine(value: "WEBVTT");
        }

        if (!hasTimestampMap)
        {
            sb.AppendLine(value: "X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
        }

        foreach (ParsedCue cue in cues)
        {
            sb.AppendLine();
            if (cue.Id is not null)
                sb.AppendLine(value: cue.Id);
            sb.AppendLine(value: cue.TimestampLine);
            sb.AppendLine(value: cue.Payload);
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    // ------------------------------------------------------------------
    // Inner types
    // ------------------------------------------------------------------

    private sealed record ParsedCue(
        string? Id,
        string TimestampLine,
        TimeSpan Start,
        TimeSpan End,
        string Payload
    );
}

/// <summary>
/// One per-segment WebVTT output produced by <see cref="WebVttSegmenter"/>.
/// </summary>
public sealed record WebVttSegment(int Index, string Content, TimeSpan StartTime, TimeSpan EndTime);
