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

        string tag = ((string?)video["codec_tag_string"] ?? string.Empty).ToLowerInvariant();
        bool taggedDv = DolbyVisionTags.Contains(tag);
        bool hasRpu = output.FirstFrameSideData.Any(sd =>
        {
            string type = ((string?)sd["side_data_type"] ?? string.Empty).ToLowerInvariant();
            return type.Contains("dolby vision")
                || type.Contains("dovi")
                || (int?)sd["rpu_present_flag"] == 1;
        });

        if (taggedDv && !hasRpu)
            violations.Add(
                $"DV-tag-without-RPU: codec_tag '{tag}' claims Dolby Vision but no DOVI "
                    + "configuration/RPU side-data is present — a re-encode that strips the RPU "
                    + "MUST be tagged hvc1/hev1. This is the reported corrupt-playback case."
            );

        if (!taggedDv && hasRpu)
            violations.Add(
                $"DV-RPU-without-tag: an RPU is present but codec_tag '{tag}' is not a Dolby "
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

        string transfer = (string?)video["color_transfer"] ?? string.Empty;
        if (!string.Equals(transfer, "smpte2084", StringComparison.OrdinalIgnoreCase))
            return; // not HDR10 — HLG/SDR checked elsewhere

        string primaries = (string?)video["color_primaries"] ?? string.Empty;
        string space = (string?)video["color_space"] ?? string.Empty;
        string pixFmt = (string?)video["pix_fmt"] ?? string.Empty;

        if (!string.Equals(primaries, "bt2020", StringComparison.OrdinalIgnoreCase))
            violations.Add($"HDR10-primaries: color_primaries='{primaries}', expected bt2020.");
        if (!string.Equals(space, "bt2020nc", StringComparison.OrdinalIgnoreCase))
            violations.Add($"HDR10-space: color_space='{space}', expected bt2020nc.");
        if (!pixFmt.Contains("10"))
            violations.Add($"HDR10-bitdepth: pix_fmt='{pixFmt}' is not 10-bit.");
        if (!output.HasSideData("Mastering display metadata"))
            violations.Add(
                "HDR10-mastering-display: PQ transfer but no Mastering-display side-data "
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
        if ((string?)video["codec_name"] != "hevc")
            return;

        string tag = ((string?)video["codec_tag_string"] ?? string.Empty).ToLowerInvariant();
        if (tag == "hev1")
            violations.Add(
                "HEVC-tag-hev1: HLS fMP4 requires hvc1 (params in init segment); hev1 is "
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
        MatchCollection streamInfs = StreamInfRegex().Matches(masterPlaylistText);
        if (streamInfs.Count == 0)
        {
            violations.Add("HLS-master-empty: no EXT-X-STREAM-INF variants in the master.");
            return;
        }

        List<long> bandwidths = [];
        foreach (Match m in streamInfs)
        {
            string line = m.Value;
            Match bw = Regex.Match(line, @"BANDWIDTH=(\d+)");
            if (!bw.Success)
            {
                violations.Add($"HLS-missing-bandwidth: variant has no BANDWIDTH → {Trim(line)}");
                continue;
            }
            bandwidths.Add(long.Parse(bw.Groups[1].Value));

            if (!line.Contains("CODECS="))
                violations.Add($"HLS-missing-codecs: variant has no CODECS → {Trim(line)}");
            if (!line.Contains("VIDEO-RANGE="))
                violations.Add(
                    $"HLS-missing-video-range: variant has no VIDEO-RANGE → {Trim(line)}"
                );
        }

        if (bandwidths.Count > 1 && bandwidths.Distinct().Count() == 1)
            violations.Add(
                $"HLS-identical-bandwidth: all {bandwidths.Count} variants advertise the same "
                    + $"BANDWIDTH={bandwidths[0]} — ABR cannot pick by bitrate (the MapLabel-collision bug)."
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
                $"audio-tracks-dropped: source has {srcAudio.Count} audio track(s), output has "
                    + $"{outAudio.Count} — a track was dropped (keep-all unless a policy explicitly prunes)."
            );

        foreach (JObject a in outAudio)
        {
            string lang = Lang(a);
            if (lang == "und")
            {
                // Only a violation if a matching source track HAD a language.
                if (srcAudio.Any(s => Lang(s) != "und"))
                    violations.Add(
                        "audio-language-stripped: an output audio track has language 'und' while "
                            + "the source carried language tags."
                    );
            }
        }

        int outDefaults = outAudio.Count(a => (int?)a["disposition"]?["default"] == 1);
        if (outAudio.Count > 0 && outDefaults != 1)
            violations.Add(
                $"audio-default-disposition: expected exactly one default audio track, found {outDefaults}."
            );

        // Channel-layout exactness for the primary track (best-effort by index 0).
        if (srcAudio.Count > 0 && outAudio.Count > 0)
        {
            string srcLayout = (string?)srcAudio[0]["channel_layout"] ?? string.Empty;
            string outLayout = (string?)outAudio[0]["channel_layout"] ?? string.Empty;
            // Only assert when the primary track is a copy (same codec); a downmix
            // legitimately changes the layout.
            bool sameCodec =
                (string?)srcAudio[0]["codec_name"] == (string?)outAudio[0]["codec_name"];
            if (sameCodec && srcLayout.Length > 0 && outLayout != srcLayout)
                violations.Add(
                    $"audio-channel-layout: copied primary audio layout changed '{srcLayout}' → "
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
            .SubtitleStreams.Where(s => IsTextSub(s) || IsBitmapSub(s))
            .ToList();
        List<JObject> outSubs = output.SubtitleStreams.ToList();

        // Text subs must survive (bitmap subs may be routed/burned, so only warn
        // when ALL subtitle tracks vanished and the source had text subs).
        int srcText = source.SubtitleStreams.Count(IsTextSub);
        if (srcText > 0 && outSubs.Count == 0)
            violations.Add(
                $"subtitle-tracks-dropped: source had {srcText} text subtitle track(s), output has 0."
            );

        foreach (JObject s in outSubs)
        {
            if (Lang(s) == "und" && source.SubtitleStreams.Any(x => Lang(x) != "und"))
                violations.Add(
                    "subtitle-language-stripped: an output subtitle track is 'und' while the "
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
                $"chapters-dropped: source had {source.Chapters.Count} chapters, output has none."
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
        CheckDolbyVisionTagCoherence(output, violations);
        CheckHdr10Signaling(output, violations);
        CheckHevcFmp4Tag(output, violations);
        CheckAudioFidelity(source, output, violations);
        CheckSubtitleFidelity(source, output, violations);
        CheckChaptersPreserved(source, output, violations);
        if (masterPlaylistText is not null)
            CheckMasterPlaylist(masterPlaylistText, violations);
        return violations;
    }

    private static string Lang(JObject stream) => (string?)stream["tags"]?["language"] ?? "und";

    private static bool IsTextSub(JObject s) =>
        (string?)s["codec_name"] is "mov_text" or "subrip" or "ass" or "ssa" or "webvtt";

    private static bool IsBitmapSub(JObject s) =>
        (string?)s["codec_name"] is "hdmv_pgs_subtitle" or "dvd_subtitle" or "dvb_subtitle";

    private static string Trim(string line) => line.Length > 90 ? line[..90] + "…" : line;

    [GeneratedRegex(@"#EXT-X-STREAM-INF:[^\r\n]+", RegexOptions.Multiline)]
    private static partial Regex StreamInfRegex();
}
