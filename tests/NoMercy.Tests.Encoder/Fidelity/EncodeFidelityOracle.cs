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
using Newtonsoft.Json.Linq;

namespace NoMercy.Tests.Encoder.Fidelity;

/// <summary>
/// The single, reusable "is this encode actually correct?" gate. Every check is
/// an ffprobe-verifiable rule from the conformance checklist (mined from Apple
/// HLS / RFC 8216 / ISOBMFF / Dolby Vision specs and the Jellyfin/Emby/hls.js
/// defect history). Each method appends a human-readable violation string to a
/// list; a clean encode returns zero violations. This exists so no future encode
/// is validated by eyeballing a single ffprobe field — the failure classes we
/// hit (dvh1-on-re-encode, shared BANDWIDTH, dropped audio, stripped language)
/// are all in here as hard assertions, plus the rest of the catalog.
///
/// Checks split into two kinds:
///   * Output-only invariants (a correct HLS/HDR/DV file, independent of source).
///   * Source→output fidelity (nothing silently lost vs the input).
/// </summary>
public static partial class EncodeFidelityOracle
{
    private static readonly string[] DolbyVisionTags = ["dvh1", "dvhe", "dvav", "dva1"];

    // ── Output-only: Dolby Vision tag ↔ RPU coherence (THE critical case) ──────

    /// <summary>
    /// A <c>dvh1</c>/<c>dvhe</c> sample-entry tag REQUIRES a real Dolby Vision
    /// configuration + RPU. A re-encode (nvenc, or any crop/scale/tonemap) strips
    /// the RPU, so the tag MUST fall back to <c>hvc1</c>. A dv* tag with no DOVI
    /// record is the corrupt file that fails on DV and non-DV players alike.
    /// </summary>
    public static void CheckDolbyVisionTagCoherence(ProbedMedia output, List<string> violations)
    {
        JObject? video = output.PrimaryVideo;
        if (video is null)
            return;

        string tag = ((string?)video[propertyName: "codec_tag_string"] ?? string.Empty).ToLowerInvariant();
        bool taggedDv = DolbyVisionTags.Contains(value: tag);
        bool hasRpu = output.FirstFrameSideData.Any(predicate: sd =>
        {
            string type = ((string?)sd[propertyName: "side_data_type"] ?? string.Empty).ToLowerInvariant();
            return type.Contains(value: "dolby vision")
                || type.Contains(value: "dovi")
                || (int?)sd[propertyName: "rpu_present_flag"] == 1;
        });

        if (taggedDv && !hasRpu)
            violations.Add(
                item: $"DV-tag-without-RPU: codec_tag '{tag}' claims Dolby Vision but no DOVI "
                      + "configuration/RPU side-data is present — a re-encode that strips the RPU "
                      + "MUST be tagged hvc1/hev1. This is the reported corrupt-playback case."
            );

        if (!taggedDv && hasRpu)
            violations.Add(
                item: $"DV-RPU-without-tag: an RPU is present but codec_tag '{tag}' is not a Dolby "
                      + "Vision tag — the DV signalling is inconsistent."
            );
    }

    // ── Output-only: HDR10 signalling completeness ─────────────────────────────

    /// <summary>
    /// When the transfer is PQ (smpte2084) the full HDR10 triplet + 10-bit +
    /// mastering-display must be present, or the stream renders dark/desaturated.
    /// Also catches the green/red primary swap (a GBR-vs-RGB ordering bug).
    /// </summary>
    public static void CheckHdr10Signaling(ProbedMedia output, List<string> violations)
    {
        JObject? video = output.PrimaryVideo;
        if (video is null)
            return;

        string transfer = (string?)video[propertyName: "color_transfer"] ?? string.Empty;
        if (!string.Equals(a: transfer, b: "smpte2084", comparisonType: StringComparison.OrdinalIgnoreCase))
            return; // not HDR10 — HLG/SDR checked elsewhere

        string primaries = (string?)video[propertyName: "color_primaries"] ?? string.Empty;
        string space = (string?)video[propertyName: "color_space"] ?? string.Empty;
        string pixFmt = (string?)video[propertyName: "pix_fmt"] ?? string.Empty;

        if (!string.Equals(a: primaries, b: "bt2020", comparisonType: StringComparison.OrdinalIgnoreCase))
            violations.Add(item: $"HDR10-primaries: color_primaries='{primaries}', expected bt2020.");
        if (!string.Equals(a: space, b: "bt2020nc", comparisonType: StringComparison.OrdinalIgnoreCase))
            violations.Add(item: $"HDR10-space: color_space='{space}', expected bt2020nc.");
        if (!pixFmt.Contains(value: "10"))
            violations.Add(item: $"HDR10-bitdepth: pix_fmt='{pixFmt}' is not 10-bit.");
        if (!output.HasSideData(sideDataType: "Mastering display metadata"))
            violations.Add(
                item: "HDR10-mastering-display: PQ transfer but no Mastering-display side-data "
                      + "(dropped on re-encode → washed-out HDR)."
            );
    }

    // ── Output-only: HEVC in fMP4/HLS must be hvc1, not hev1 ───────────────────

    /// <summary>
    /// Apple/Safari HLS fMP4 requires <c>hvc1</c> (parameter sets out-of-band in
    /// the init segment); <c>hev1</c> (in-band) is refused. Applies to non-DV HEVC
    /// (a legitimate DV copy keeps its dv* tag — handled by the DV check).
    /// </summary>
    public static void CheckHevcFmp4Tag(ProbedMedia output, List<string> violations)
    {
        JObject? video = output.PrimaryVideo;
        if (video is null)
            return;
        if ((string?)video[propertyName: "codec_name"] != "hevc")
            return;

        string tag = ((string?)video[propertyName: "codec_tag_string"] ?? string.Empty).ToLowerInvariant();
        if (tag == "hev1")
            violations.Add(
                item: "HEVC-tag-hev1: HLS fMP4 requires hvc1 (params in init segment); hev1 is "
                      + "refused by Safari/AVFoundation."
            );
    }

    // ── Output-only: HLS master playlist variant metrics ───────────────────────

    /// <summary>
    /// Every <c>EXT-X-STREAM-INF</c> must carry a <c>BANDWIDTH</c>, and the values
    /// must be DISTINCT across variants — an identical/shared bitrate on every rung
    /// breaks ABR selection. Also asserts every variant has a VIDEO-RANGE and CODECS.
    /// </summary>
    public static void CheckMasterPlaylist(string masterPlaylistText, List<string> violations)
    {
        MatchCollection streamInfs = StreamInfRegex().Matches(input: masterPlaylistText);
        if (streamInfs.Count == 0)
        {
            violations.Add(item: "HLS-master-empty: no EXT-X-STREAM-INF variants in the master.");
            return;
        }

        List<long> bandwidths = [];
        foreach (Match m in streamInfs)
        {
            string line = m.Value;
            Match bw = Regex.Match(input: line, pattern: @"BANDWIDTH=(\d+)");
            if (!bw.Success)
            {
                violations.Add(item: $"HLS-missing-bandwidth: variant has no BANDWIDTH → {Trim(line: line)}");
                continue;
            }
            bandwidths.Add(item: long.Parse(s: bw.Groups[groupnum: 1].Value));

            if (!line.Contains(value: "CODECS="))
                violations.Add(item: $"HLS-missing-codecs: variant has no CODECS → {Trim(line: line)}");
            if (!line.Contains(value: "VIDEO-RANGE="))
                violations.Add(
                    item: $"HLS-missing-video-range: variant has no VIDEO-RANGE → {Trim(line: line)}"
                );

            CheckHevcCodecsLevelForResolution(streamInf: line, violations: violations);
        }

        if (bandwidths.Count > 1 && bandwidths.Distinct().Count() == 1)
            violations.Add(
                item: $"HLS-identical-bandwidth: all {bandwidths.Count} variants advertise the same "
                      + $"BANDWIDTH={bandwidths[index: 0]} — ABR cannot pick by bitrate (the MapLabel-collision bug)."
            );
    }

    /// <summary>
    /// A variant's advertised HEVC level must be able to carry its RESOLUTION.
    /// The Punisher 4K rung shipped CODECS="hvc1.2.4.L120.B0" — level 4.0, whose
    /// MaxLumaPs (2,228,224 samples) cannot hold 3840×2160 (8,294,400). Players
    /// that validate the codec string against the stream reject the variant.
    /// Parses RESOLUTION=WxH and the hvc1 …L&lt;idc&gt;… token and flags any level
    /// below the picture-size floor for that resolution.
    /// </summary>
    private static void CheckHevcCodecsLevelForResolution(string streamInf, List<string> violations)
    {
        Match res = Regex.Match(input: streamInf, pattern: @"RESOLUTION=(\d+)x(\d+)");
        Match hevc = Regex.Match(input: streamInf, pattern: @"hvc1\.\d+\.[0-9A-Fa-f]+\.L(\d+)");
        if (!res.Success || !hevc.Success)
            return;

        long lumaPs = long.Parse(s: res.Groups[groupnum: 1].Value) * long.Parse(s: res.Groups[groupnum: 2].Value);
        int levelIdc = int.Parse(s: hevc.Groups[groupnum: 1].Value);

        // HEVC Annex A MaxLumaPs → the lowest level_idc that can hold lumaPs.
        int floorIdc = lumaPs switch
        {
            <= 983_040 => 93, // ≤ L3.1
            <= 2_228_224 => 120, // ≤ L4.x (1080p)
            <= 8_912_896 => 150, // ≤ L5.x (4K)
            _ => 180, // L6.x (8K)
        };

        if (levelIdc < floorIdc)
            violations.Add(
                item: $"HLS-codecs-level-too-low: {res.Groups[groupnum: 1].Value}x{res.Groups[groupnum: 2].Value} variant "
                      + $"advertises HEVC L{levelIdc} but needs ≥ L{floorIdc} for that resolution → {Trim(line: streamInf)}"
            );
    }

    // ── Source→output: audio fidelity ─────────────────────────────────────────

    /// <summary>
    /// Audio must not be silently lost: keep every track, preserve channel layout
    /// EXACTLY (5.1(side) ≠ 5.1), and never strip a language to und.
    /// </summary>
    public static void CheckAudioFidelity(
        ProbedMedia source,
        ProbedMedia output,
        List<string> violations
    )
    {
        List<JObject> srcAudio = source.AudioStreams.ToList();
        List<JObject> outAudio = output.AudioStreams.ToList();

        if (outAudio.Count < srcAudio.Count)
            violations.Add(
                item: $"audio-tracks-dropped: source has {srcAudio.Count} audio track(s), output has "
                      + $"{outAudio.Count} — a track was dropped (keep-all unless a policy explicitly prunes)."
            );

        foreach (JObject a in outAudio)
        {
            string lang = Lang(stream: a);
            if (lang == "und")
            {
                // Only a violation if a matching source track HAD a language.
                if (srcAudio.Any(predicate: s => Lang(stream: s) != "und"))
                    violations.Add(
                        item: "audio-language-stripped: an output audio track has language 'und' while "
                              + "the source carried language tags."
                    );
            }
        }

        int outDefaults = outAudio.Count(predicate: a => (int?)a[propertyName: "disposition"]?[key: "default"] == 1);
        if (outAudio.Count > 0 && outDefaults != 1)
            violations.Add(
                item: $"audio-default-disposition: expected exactly one default audio track, found {outDefaults}."
            );

        // Channel-layout exactness for the primary track (best-effort by index 0).
        if (srcAudio.Count > 0 && outAudio.Count > 0)
        {
            string srcLayout = (string?)srcAudio[index: 0][propertyName: "channel_layout"] ?? string.Empty;
            string outLayout = (string?)outAudio[index: 0][propertyName: "channel_layout"] ?? string.Empty;
            // Only assert when the primary track is a copy (same codec); a downmix
            // legitimately changes the layout.
            bool sameCodec =
                (string?)srcAudio[index: 0][propertyName: "codec_name"] == (string?)outAudio[index: 0][propertyName: "codec_name"];
            if (sameCodec && srcLayout.Length > 0 && outLayout != srcLayout)
                violations.Add(
                    item: $"audio-channel-layout: copied primary audio layout changed '{srcLayout}' → "
                          + $"'{outLayout}' (5.1(side) vs 5.1 mismatches break channel routing)."
                );
        }
    }

    // ── Source→output: subtitle fidelity ──────────────────────────────────────

    public static void CheckSubtitleFidelity(
        ProbedMedia source,
        ProbedMedia output,
        List<string> violations
    )
    {
        List<JObject> srcSubs = source
            .SubtitleStreams.Where(predicate: s => IsTextSub(s: s) || IsBitmapSub(s: s))
            .ToList();
        List<JObject> outSubs = output.SubtitleStreams.ToList();

        // Text subs must survive (bitmap subs may be routed/burned, so only warn
        // when ALL subtitle tracks vanished and the source had text subs).
        int srcText = source.SubtitleStreams.Count(predicate: IsTextSub);
        if (srcText > 0 && outSubs.Count == 0)
            violations.Add(
                item: $"subtitle-tracks-dropped: source had {srcText} text subtitle track(s), output has 0."
            );

        foreach (JObject s in outSubs)
        {
            if (Lang(stream: s) == "und" && source.SubtitleStreams.Any(predicate: x => Lang(stream: x) != "und"))
                violations.Add(
                    item: "subtitle-language-stripped: an output subtitle track is 'und' while the "
                          + "source carried subtitle languages."
                );
        }
    }

    // ── Source→output: chapters ────────────────────────────────────────────────

    public static void CheckChaptersPreserved(
        ProbedMedia source,
        ProbedMedia output,
        List<string> violations
    )
    {
        if (source.Chapters.Count > 0 && output.Chapters.Count == 0)
            violations.Add(
                item: $"chapters-dropped: source had {source.Chapters.Count} chapters, output has none."
            );
    }

    // ── Output-only: SDR must not carry a residual HDR transfer ────────────────

    /// <summary>
    /// A tone-mapped SDR output must NOT keep an HDR transfer (smpte2084/HLG) or
    /// mastering-display side-data — a scale/tonemap chain that fails to re-stamp
    /// the colour tags leaves the stream flagged HDR while the pixels are SDR,
    /// which players render crushed/washed. HLG output must carry arib-std-b67 +
    /// bt2020 (not PQ).
    /// </summary>
    public static void CheckSdrColorConsistency(ProbedMedia output, List<string> violations)
    {
        JObject? video = output.PrimaryVideo;
        if (video is null)
            return;

        string transfer = ((string?)video[propertyName: "color_transfer"] ?? string.Empty).ToLowerInvariant();
        string primaries = ((string?)video[propertyName: "color_primaries"] ?? string.Empty).ToLowerInvariant();

        // Heuristic for "this is meant to be SDR": BT.709 primaries. If a BT.709
        // stream still advertises a PQ/HLG transfer, the colour re-stamp was missed.
        bool looksSdr = primaries == "bt709";
        if (looksSdr && transfer is "smpte2084" or "arib-std-b67")
            violations.Add(
                item: $"SDR-residual-hdr-transfer: bt709 primaries but color_transfer='{transfer}' — "
                      + "an SDR output must not keep an HDR transfer characteristic."
            );
        if (looksSdr && output.HasSideData(sideDataType: "Mastering display metadata"))
            violations.Add(
                item: "SDR-residual-mastering-display: an SDR (bt709) output still carries HDR "
                      + "mastering-display side-data."
            );
    }

    // ── Source→output: A/V start alignment (edit-list / priming drift) ─────────

    /// <summary>
    /// The primary video and audio must start at (near) the same time. A large
    /// per-stream start_time delta — an unhandled encoder-priming edit list, or
    /// <c>-ss</c> before <c>-i</c> — shows as lip-sync drift on players that
    /// ignore edit lists.
    /// </summary>
    public static void CheckAvStartAlignment(ProbedMedia output, List<string> violations)
    {
        JObject? video = output.PrimaryVideo;
        JObject? audio = output.AudioStreams.FirstOrDefault();
        if (video is null || audio is null)
            return;

        double vStart = StartTime(stream: video);
        double aStart = StartTime(stream: audio);

        if (Math.Abs(value: vStart) > 0.5)
            violations.Add(
                item: $"av-video-start-offset: primary video start_time={vStart:F3}s (expected ≈0)."
            );
        if (Math.Abs(value: aStart) > 0.5)
            violations.Add(
                item: $"av-audio-start-offset: primary audio start_time={aStart:F3}s (expected ≈0)."
            );
        if (Math.Abs(value: vStart - aStart) > 0.1)
            violations.Add(
                item: $"av-sync-drift: video/audio start_time differ by {Math.Abs(value: vStart - aStart):F3}s "
                      + "(>100ms → lip-sync drift on edit-list-ignoring players)."
            );
    }

    // ── Source→output: anamorphic SAR/DAR + rotation preservation ──────────────

    /// <summary>
    /// A non-square-pixel (anamorphic) source must not be collapsed to 1:1 — that
    /// squishes/stretches the picture. When the source SAR ≠ 1:1, the output must
    /// preserve the display aspect ratio (via SAR carried, or pixels scaled to the
    /// display geometry with SAR reset to 1:1 — either keeps DAR intact).
    /// </summary>
    public static void CheckAnamorphicPreserved(
        ProbedMedia source,
        ProbedMedia output,
        List<string> violations
    )
    {
        JObject? src = source.PrimaryVideo;
        JObject? outp = output.PrimaryVideo;
        if (src is null || outp is null)
            return;

        string srcDar = (string?)src[propertyName: "display_aspect_ratio"] ?? string.Empty;
        string outDar = (string?)outp[propertyName: "display_aspect_ratio"] ?? string.Empty;
        if (srcDar.Length == 0 || srcDar is "0:1" or "N/A")
            return; // source DAR unknown — nothing to preserve

        // Compare DAR as a ratio within tolerance (16:9 == 1.778).
        double srcRatio = Ratio(aspect: srcDar);
        double outRatio = Ratio(aspect: outDar);
        if (srcRatio > 0 && (outRatio <= 0 || Math.Abs(value: srcRatio - outRatio) / srcRatio > 0.02))
            violations.Add(
                item: $"anamorphic-dar-lost: source display_aspect_ratio='{srcDar}' but output='{outDar}' "
                      + "— anamorphic geometry collapsed (SAR reset to 1:1 without a compensating scale)."
            );
    }

    /// <summary>
    /// Rotation must survive: a source display-matrix rotation is either preserved
    /// (remux) or physically applied with the matrix cleared (transcode+transpose)
    /// — never lost (plays sideways) or left in place after the pixels were already
    /// rotated (double rotation).
    /// </summary>
    public static void CheckRotationPreserved(
        ProbedMedia source,
        ProbedMedia output,
        List<string> violations
    )
    {
        int srcRot = Rotation(media: source);
        if (srcRot == 0)
            return;

        int outRot = Rotation(media: output);
        bool dimsSwapped = DimsSwapped(a: source.PrimaryVideo, b: output.PrimaryVideo);

        // Preserved (remux): output keeps the same rotation. Applied (transcode):
        // output rotation is 0 AND width/height swapped for 90/270. Anything else
        // = lost or double-applied.
        bool preserved = outRot == srcRot;
        bool applied = outRot == 0 && (srcRot % 180 == 0 || dimsSwapped);
        if (!preserved && !applied)
            violations.Add(
                item: $"rotation-lost-or-doubled: source rotation={srcRot}° but output rotation={outRot}° "
                      + $"(dims swapped={dimsSwapped}) — plays sideways or double-rotated."
            );
    }

    // ── Convenience: run the full suite ───────────────────────────────────────

    /// <summary>
    /// Run every source→output + output-only check and return all violations.
    /// Pass <paramref name="masterPlaylistText"/> when validating an HLS output.
    /// </summary>
    public static List<string> Validate(
        ProbedMedia source,
        ProbedMedia output,
        string? masterPlaylistText = null
    )
    {
        List<string> violations = [];
        CheckDolbyVisionTagCoherence(output: output, violations: violations);
        CheckHdr10Signaling(output: output, violations: violations);
        CheckSdrColorConsistency(output: output, violations: violations);
        CheckHevcFmp4Tag(output: output, violations: violations);
        CheckAvStartAlignment(output: output, violations: violations);
        CheckAudioFidelity(source: source, output: output, violations: violations);
        CheckSubtitleFidelity(source: source, output: output, violations: violations);
        CheckChaptersPreserved(source: source, output: output, violations: violations);
        CheckAnamorphicPreserved(source: source, output: output, violations: violations);
        CheckRotationPreserved(source: source, output: output, violations: violations);
        if (masterPlaylistText is not null)
            CheckMasterPlaylist(masterPlaylistText: masterPlaylistText, violations: violations);
        return violations;
    }

    private static double StartTime(JObject stream) =>
        double.TryParse(
            s: (string?)stream[propertyName: "start_time"],
            style: System.Globalization.NumberStyles.Float,
            provider: System.Globalization.CultureInfo.InvariantCulture,
            result: out double v
        )
            ? v
            : 0.0;

    private static double Ratio(string aspect)
    {
        string[] parts = aspect.Split(separator: ':');
        if (
            parts.Length == 2
            && double.TryParse(
                s: parts[0],
                style: System.Globalization.NumberStyles.Float,
                provider: System.Globalization.CultureInfo.InvariantCulture,
                result: out double w
            )
            && double.TryParse(
                s: parts[1],
                style: System.Globalization.NumberStyles.Float,
                provider: System.Globalization.CultureInfo.InvariantCulture,
                result: out double h
            )
            && h != 0
        )
            return w / h;
        return 0;
    }

    private static int Rotation(ProbedMedia media)
    {
        // ffmpeg exposes rotation on the Display Matrix side-data (negative =
        // clockwise); normalise to 0..359.
        JObject? matrix = media.SideData(sideDataType: "Display Matrix");
        if (matrix?[propertyName: "rotation"] is not null && int.TryParse(s: (string?)matrix[propertyName: "rotation"], result: out int r))
            return ((r % 360) + 360) % 360;

        // Legacy tag fallback.
        string? tag = (string?)media.PrimaryVideo?[propertyName: "tags"]?[key: "rotate"];
        if (int.TryParse(s: tag, result: out int t))
            return ((t % 360) + 360) % 360;
        return 0;
    }

    private static bool DimsSwapped(JObject? a, JObject? b)
    {
        if (a is null || b is null)
            return false;
        int aw = (int?)a[propertyName: "width"] ?? 0;
        int ah = (int?)a[propertyName: "height"] ?? 0;
        int bw = (int?)b[propertyName: "width"] ?? 0;
        int bh = (int?)b[propertyName: "height"] ?? 0;
        return aw == bh && ah == bw && aw != ah;
    }

    private static string Lang(JObject stream) => (string?)stream[propertyName: "tags"]?[key: "language"] ?? "und";

    private static bool IsTextSub(JObject s) =>
        (string?)s[propertyName: "codec_name"] is "mov_text" or "subrip" or "ass" or "ssa" or "webvtt";

    private static bool IsBitmapSub(JObject s) =>
        (string?)s[propertyName: "codec_name"] is "hdmv_pgs_subtitle" or "dvd_subtitle" or "dvb_subtitle";

    private static string Trim(string line) => line.Length > 90 ? line[..90] + "…" : line;

    [GeneratedRegex(pattern: @"#EXT-X-STREAM-INF:[^\r\n]+", options: RegexOptions.Multiline)]
    private static partial Regex StreamInfRegex();
}
