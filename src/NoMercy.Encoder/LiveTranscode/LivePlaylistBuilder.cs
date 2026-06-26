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
    string SegmentUrlTemplate
);

/// <summary>
/// Pure builder that emits an HLS media playlist (.m3u8) from a runtime live
/// session. While the encoder is still producing segments we emit
/// <c>EXT-X-PLAYLIST-TYPE:EVENT</c>; once the session finishes we switch to
/// <c>VOD</c> and add <c>EXT-X-ENDLIST</c>.
/// </summary>
public class LivePlaylistBuilder : ILivePlaylistBuilder
{
    public string Build(LivePlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SegmentUrlTemplate))
        {
            throw new ArgumentException("SegmentUrlTemplate must include {index}", nameof(request));
        }

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

    private static double MaxSegmentDuration(LivePlaylistRequest request)
    {
        double fromTarget = request.TargetSegmentDuration.TotalSeconds;
        if (request.Segments.Count == 0)
            return fromTarget > 0 ? fromTarget : 6;

        double longest = request.Segments.Max(s => s.Duration.TotalSeconds);
        return Math.Max(fromTarget, longest);
    }
}
