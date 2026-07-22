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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Encoder.LiveTranscode;

public interface ILivePlaylistBuilder
{
    string Build(LivePlaylistRequest request);

    /// <summary>
    /// Emits the session's HLS master playlist: one video variant pointing at the
    /// live media playlist plus an <c>EXT-X-MEDIA</c> entry per pre-encoded audio
    /// rendition. Structurally identical to a finished NoMercy encode's master, so
    /// the player's native audio menu and track switching work with no changes.
    /// </summary>
    string BuildMaster(LiveMasterPlaylistRequest request);
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

public record LiveMasterPlaylistRequest(
    // Relative URI of the live video media playlist, resolved by the client
    // against the master's own URL (e.g. "playlist.m3u8").
    string VideoPlaylistUri,
    int Width,
    int Height,
    int BitrateKbps,
    IReadOnlyList<LiveAudioRendition> AudioRenditions
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
        if (string.IsNullOrWhiteSpace(value: request.SegmentUrlTemplate))
        {
            throw new ArgumentException(message: "SegmentUrlTemplate must include {index}", paramName: nameof(request));
        }

        if (request.TotalDuration is { } total && total > TimeSpan.Zero)
            return BuildFullDurationVod(request: request, total: total);

        StringBuilder sb = new();
        int targetDurationSeconds = Math.Max(val1: 1, val2: (int)Math.Ceiling(a: MaxSegmentDuration(request: request)));

        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(value: "#EXT-X-VERSION:6");
        sb.AppendLine(
            handler: $"#EXT-X-TARGETDURATION:{targetDurationSeconds.ToString(provider: CultureInfo.InvariantCulture)}"
        );

        int mediaSequence = request.Segments.Count == 0 ? 0 : request.Segments[index: 0].Index;
        sb.AppendLine(
            handler: $"#EXT-X-MEDIA-SEQUENCE:{mediaSequence.ToString(provider: CultureInfo.InvariantCulture)}"
        );

        sb.AppendLine(
            value: request.IsComplete ? "#EXT-X-PLAYLIST-TYPE:VOD" : "#EXT-X-PLAYLIST-TYPE:EVENT"
        );

        foreach (Segment segment in request.Segments)
        {
            double duration = segment.Duration.TotalSeconds;
            sb.AppendLine(handler: $"#EXTINF:{duration.ToString(format: "F3", provider: CultureInfo.InvariantCulture)},");
            sb.AppendLine(
                value: request.SegmentUrlTemplate.Replace(
                    oldValue: "{index}",
                    newValue: segment.Index.ToString(provider: CultureInfo.InvariantCulture)
                )
            );
        }

        if (request.IsComplete)
        {
            sb.AppendLine(value: "#EXT-X-ENDLIST");
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
        int segmentCount = Math.Max(val1: 1, val2: (int)Math.Ceiling(a: totalSeconds / segmentDuration));
        int targetDurationSeconds = Math.Max(val1: 1, val2: (int)Math.Ceiling(a: segmentDuration));

        StringBuilder sb = new();
        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(value: "#EXT-X-VERSION:6");
        sb.AppendLine(
            handler: $"#EXT-X-TARGETDURATION:{targetDurationSeconds.ToString(provider: CultureInfo.InvariantCulture)}"
        );
        sb.AppendLine(value: "#EXT-X-MEDIA-SEQUENCE:0");
        sb.AppendLine(value: "#EXT-X-PLAYLIST-TYPE:VOD");

        for (int index = 0; index < segmentCount; index++)
        {
            double start = index * segmentDuration;
            double duration = Math.Min(val1: segmentDuration, val2: totalSeconds - start);
            if (duration <= 0)
                duration = segmentDuration;

            sb.AppendLine(handler: $"#EXTINF:{duration.ToString(format: "F3", provider: CultureInfo.InvariantCulture)},");
            sb.AppendLine(
                value: request.SegmentUrlTemplate.Replace(
                    oldValue: "{index}",
                    newValue: index.ToString(provider: CultureInfo.InvariantCulture)
                )
            );
        }

        sb.AppendLine(value: "#EXT-X-ENDLIST");
        return sb.ToString();
    }

    private static double MaxSegmentDuration(LivePlaylistRequest request)
    {
        double fromTarget = request.TargetSegmentDuration.TotalSeconds;
        if (request.Segments.Count == 0)
            return fromTarget > 0 ? fromTarget : 6;

        double longest = request.Segments.Max(selector: s => s.Duration.TotalSeconds);
        return Math.Max(val1: fromTarget, val2: longest);
    }

    private const string AudioGroupId = "audio";

    public string BuildMaster(LiveMasterPlaylistRequest request)
    {
        bool hasAudioGroup = request.AudioRenditions.Count > 0;

        StringBuilder sb = new();
        sb.AppendLine(value: "#EXTM3U");
        sb.AppendLine(value: "#EXT-X-VERSION:6");
        sb.AppendLine(value: "#EXT-X-INDEPENDENT-SEGMENTS");
        sb.AppendLine();

        foreach (LiveAudioRendition rendition in request.AudioRenditions)
        {
            string name = Culture.EnglishLanguageName(code: rendition.Language);
            sb.AppendLine(
                handler: $"#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"{AudioGroupId}\",LANGUAGE=\"{rendition.Language}\","
                         + $"NAME=\"{name}\",AUTOSELECT=YES,DEFAULT={YesNo(value: rendition.IsDefault)},URI=\"{rendition.Uri}\""
            );
        }

        if (hasAudioGroup)
            sb.AppendLine();

        // Bandwidth is the video target plus a nominal audio allowance; the exact
        // value only guides the client's ABR, which is moot here with one variant.
        // CODECS is deliberately omitted: the live encoder's H.264 profile/level
        // varies, and a mismatched CODECS makes hls.js reject the master, whereas
        // a missing one lets it probe the first segment (the safer default the VOD
        // PlaylistGenerator also takes for copy-mode streams).
        int bandwidth = request.BitrateKbps * 1000 + 128_000;
        string audioAttr = hasAudioGroup ? $",AUDIO=\"{AudioGroupId}\"" : string.Empty;

        sb.AppendLine(
            handler: $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth.ToString(provider: CultureInfo.InvariantCulture)},"
                     + $"RESOLUTION={request.Width}x{request.Height},VIDEO-RANGE=SDR{audioAttr}"
        );
        sb.AppendLine(value: request.VideoPlaylistUri);

        return sb.ToString();
    }

    private static string YesNo(bool value) => value ? "YES" : "NO";
}
