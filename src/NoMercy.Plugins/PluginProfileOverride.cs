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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Plugins.Abstractions;
using EncoderMediaInfo = NoMercy.Encoder.Analysis.MediaInfo;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using PluginProfile = NoMercy.Plugins.Abstractions.EncodingProfile;

namespace NoMercy.Plugins;

/// <summary>
/// Wires <see cref="IEncoderPlugin.GetProfile"/> into the encoder's
/// <see cref="IProfileOverride"/> seam: the first encoder plugin that returns a
/// non-null profile for the analyzed source replaces the configured profile.
/// No plugins, or all returning null, keeps the configured profile unchanged.
///
/// The plugin surface (<see cref="PluginProfile"/>) is a flat public DTO kept
/// deliberately decoupled from the encoder's rich internal profile, so this class
/// owns the bridge in both directions: encoder MediaInfo → plugin MediaInfo to
/// call the hook, and the returned flat profile → a full encoder profile.
/// </summary>
public class PluginProfileOverride(IPluginManager pluginManager) : IProfileOverride
{
    public EncodingProfile Apply(EncodingProfile configured, EncoderMediaInfo media)
    {
        MediaInfo pluginMedia = ToPluginMediaInfo(media);

        foreach (IEncoderPlugin plugin in pluginManager.GetPluginsOfType<IEncoderPlugin>())
        {
            PluginProfile? pluginProfile = plugin.GetProfile(pluginMedia);
            if (pluginProfile is not null)
                return ToEncodingProfile(pluginProfile);
        }

        return configured;
    }

    private static MediaInfo ToPluginMediaInfo(EncoderMediaInfo media)
    {
        Encoder.Analysis.VideoStreamInfo? video =
            media.VideoStreams.Count > 0 ? media.VideoStreams[0] : null;
        Encoder.Analysis.AudioStreamInfo? audio =
            media.AudioStreams.Count > 0 ? media.AudioStreams[0] : null;

        return new()
        {
            FilePath = media.FilePath,
            VideoCodec = video?.Codec,
            AudioCodec = audio?.Codec,
            Width = video?.Width,
            Height = video?.Height,
            Bitrate = media.OverallBitRateKbps > 0 ? media.OverallBitRateKbps * 1000L : null,
            Duration = media.Duration,
            IsHdr = video?.IsHdr ?? false,
        };
    }

    private static EncodingProfile ToEncodingProfile(PluginProfile profile)
    {
        VideoCodecType videoCodec =
            CodecFamilyClassifier.ClassifyVideo(profile.VideoCodec) ?? VideoCodecType.H264;
        AudioCodecType audioCodec =
            CodecFamilyClassifier.ClassifyAudio(profile.AudioCodec) ?? AudioCodecType.Aac;

        return new(
            Ulid.NewUlid(),
            profile.Name,
            ParseContainer(profile.Container),
            new(
                Encoder.Profiles.StreamPolicy.Transcode,
                videoCodec,
                profile.Width,
                profile.Height,
                profile.VideoBitrate is > 0
                    ? Encoder.Profiles.RateControlMode.Vbr
                    : Encoder.Profiles.RateControlMode.Crf,
                profile.VideoBitrate is > 0 ? 0 : 23,
                profile.VideoBitrate ?? 0,
                null,
                null,
                "medium",
                Encoder.Profiles.CodecProfile.Auto,
                null,
                null,
                8,
                null,
                2,
                false,
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                profile.ExtraParameters.Count > 0
                    ? new Dictionary<string, string>(profile.ExtraParameters)
                    : null
            ),
            [
                new(
                    Encoder.Profiles.StreamPolicy.Transcode,
                    audioCodec,
                    profile.AudioBitrate ?? 0,
                    2,
                    48000,
                    [],
                    null,
                    null,
                    null,
                    ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    null
                ),
            ],
            []
        );
    }

    private static Encoder.Profiles.Container ParseContainer(string container) =>
        container.ToLowerInvariant() switch
        {
            "ts" or "mpegts" or "hls_ts" => Encoder.Profiles.Container.HlsTs,
            "m3u8" or "hls" or "fmp4" or "hls_fmp4" => Encoder.Profiles.Container.HlsFmp4,
            "mkv" or "matroska" => Encoder.Profiles.Container.Mkv,
            "dash" or "mpd" => Encoder.Profiles.Container.Dash,
            "mp3" => Encoder.Profiles.Container.Mp3,
            "flac" => Encoder.Profiles.Container.Flac,
            "ogg" or "oga" or "opus" => Encoder.Profiles.Container.Ogg,
            _ => Encoder.Profiles.Container.Mp4,
        };
}
