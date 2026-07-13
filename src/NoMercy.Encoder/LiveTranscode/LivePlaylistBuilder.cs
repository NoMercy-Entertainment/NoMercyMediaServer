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

namespace NoMercy.Encoder.LiveTranscode;

public interface ILivePlaylistBuilder
{
    string Build(LivePlaylistRequest request);
}

public record LivePlaylistRequest(
    string SessionId,
    IReadOnlyList<Segment> Segments,
    TimeSpan TargetSegmentDuration,
    bool IsComplete,
    string SegmentUrlTemplate,
    // Total runtime of the source. When known, the playlist lists every segment
    // for the whole duration up front (VOD) so the client shows a full-length
    // progress bar and can seek anywhere — the Plex/Jellyfin model — instead of
    // only the segments produced so far. Null falls back to the produced-only
    // EVENT playlist for callers that don't know the duration.
    TimeSpan? TotalDuration = null
);

/// <summary>
/// Pure builder that emits an HLS media playlist (.m3u8) from a runtime live
/// session. With a known <see cref="LivePlaylistRequest.TotalDuration"/> it
/// emits a whole-runtime <c>VOD</c> playlist with <c>EXT-X-ENDLIST</c> so the
/// client sees the full timeline immediately; segments are produced on demand
/// as the client requests them. Without a duration it falls back to the legacy
/// growing playlist: <c>EXT-X-PLAYLIST-TYPE:EVENT</c> while producing, <c>VOD</c>
/// with <c>EXT-X-ENDLIST</c> once the session finishes.
/// </summary>
public class LivePlaylistBuilder : ILivePlaylistBuilder
{
    public string Build(LivePlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SegmentUrlTemplate))
        {
            throw new ArgumentException("SegmentUrlTemplate must include {index}", nameof(request));
        }

        if (request.TotalDuration is { } total && total > TimeSpan.Zero)
            return BuildFullDurationVod(request, total);

        StringBuilder sb = new();
        int targetDurationSeconds = Math.Max(1, (int)Math.Ceiling(MaxSegmentDuration(request)));

        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:6");
        sb.AppendLine(
            $"#EXT-X-TARGETDURATION:{targetDurationSeconds.ToString(CultureInfo.InvariantCulture)}"
        );

        int mediaSequence = request.Segments.Count == 0 ? 0 : request.Segments[0].Index;
        sb.AppendLine(
            $"#EXT-X-MEDIA-SEQUENCE:{mediaSequence.ToString(CultureInfo.InvariantCulture)}"
        );

        sb.AppendLine(
            request.IsComplete ? "#EXT-X-PLAYLIST-TYPE:VOD" : "#EXT-X-PLAYLIST-TYPE:EVENT"
        );

        foreach (Segment segment in request.Segments)
        {
            double duration = segment.Duration.TotalSeconds;
            sb.AppendLine($"#EXTINF:{duration.ToString("F3", CultureInfo.InvariantCulture)},");
            sb.AppendLine(
                request.SegmentUrlTemplate.Replace(
                    "{index}",
                    segment.Index.ToString(CultureInfo.InvariantCulture)
                )
            );
        }

        if (request.IsComplete)
        {
            sb.AppendLine("#EXT-X-ENDLIST");
        }

        return sb.ToString();
    }

    private static string BuildFullDurationVod(LivePlaylistRequest request, TimeSpan total)
    {
        double segmentDuration =
            request.TargetSegmentDuration.TotalSeconds > 0
                ? request.TargetSegmentDuration.TotalSeconds
                : 6;
        double totalSeconds = total.TotalSeconds;
        int segmentCount = Math.Max(1, (int)Math.Ceiling(totalSeconds / segmentDuration));
        int targetDurationSeconds = Math.Max(1, (int)Math.Ceiling(segmentDuration));

        StringBuilder sb = new();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:6");
        sb.AppendLine(
            $"#EXT-X-TARGETDURATION:{targetDurationSeconds.ToString(CultureInfo.InvariantCulture)}"
        );
        sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        for (int index = 0; index < segmentCount; index++)
        {
            double start = index * segmentDuration;
            double duration = Math.Min(segmentDuration, totalSeconds - start);
            if (duration <= 0)
                duration = segmentDuration;

            sb.AppendLine($"#EXTINF:{duration.ToString("F3", CultureInfo.InvariantCulture)},");
            sb.AppendLine(
                request.SegmentUrlTemplate.Replace(
                    "{index}",
                    index.ToString(CultureInfo.InvariantCulture)
                )
            );
        }

        sb.AppendLine("#EXT-X-ENDLIST");
        return sb.ToString();
    }

    private static double MaxSegmentDuration(LivePlaylistRequest request)
    {
        double fromTarget = request.TargetSegmentDuration.TotalSeconds;
        if (request.Segments.Count == 0)
            return fromTarget > 0 ? fromTarget : 6;

        double longest = request.Segments.Max(s => s.Duration.TotalSeconds);
        return Math.Max(fromTarget, longest);
    }
}
