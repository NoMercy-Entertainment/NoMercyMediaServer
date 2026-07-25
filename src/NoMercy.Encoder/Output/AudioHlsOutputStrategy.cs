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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Output;

/// <summary>
/// Audio-only HLS output. Produces <c>audio.m3u8</c> and
/// <c>audio_NNN.aac</c> segments — no video, no subtitles, no master
/// playlist. The music player (hls.js) loads <c>audio.m3u8</c> directly.
/// </summary>
public class AudioHlsOutputStrategy : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.AudioHls;

    private const int DefaultSegmentSeconds = 6;
    private const string DefaultAudioCodec = "aac";
    private const string DefaultAacProfile = "aac_low";

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        int segmentDuration =
            plan.SegmentDurationSeconds > 0 ? plan.SegmentDurationSeconds : DefaultSegmentSeconds;

        AudioOutputPlan? audio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

        string audioCodec =
            audio?.Action == StreamAction.Copy ? "copy"
            : !string.IsNullOrEmpty(audio?.EncoderName) ? audio.EncoderName
            : DefaultAudioCodec;

        string playlistPath = "audio.m3u8";
        string segmentPattern = "audio_%03d.aac";

        Dictionary<string, string> extraFlags = new()
        {
            ["-f"] = "hls",
            ["-hls_time"] = segmentDuration.ToString(),
            ["-hls_playlist_type"] = "vod",
            ["-hls_flags"] = "independent_segments",
            ["-hls_segment_filename"] = segmentPattern,
        };

        // Force AAC LC profile for broadest HLS compatibility when transcoding to
        // AAC. -profile:a aac_low is an AAC-only AVOption; libmp3lame / eac3 / etc.
        // reject "aac_low" and refuse to start, so it must only be set for an AAC
        // encoder — a valid MP3/E-AC-3 HLS profile would otherwise fail to encode.
        bool isAac =
            audioCodec.Contains("aac", StringComparison.OrdinalIgnoreCase)
            || audioCodec.Equals("libfdk_aac", StringComparison.OrdinalIgnoreCase);
        if (audioCodec != "copy" && isAac)
            extraFlags["-profile:a"] = DefaultAacProfile;

        if (audio is { Action: StreamAction.Transcode } && !string.IsNullOrEmpty(audio.AudioFilter))
            extraFlags["-af"] = audio.AudioFilter;

        List<string> mapStreams = [];
        if (audio is not null)
            mapStreams.Add(audio.MapLabel);

        builder.AddOutput(
            new(
                playlistPath,
                AudioCodec: audioCodec,
                AudioBitrateKbps: audio?.Action == StreamAction.Transcode
                    ? (audio.BitrateKbps > 0 ? audio.BitrateKbps : 128)
                    : null,
                AudioChannels: audio is not null ? audio.Channels.ToString() : "2",
                AudioSampleRate: audio?.SampleRate,
                MapStreams: mapStreams.ToArray(),
                ExtraFlags: extraFlags
            )
        );
    }

    public Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    )
    {
        // Audio HLS playlists are self-contained — no master playlist needed.
        // The music player loads audio.m3u8 directly. Nothing to rename.
        return Task.CompletedTask;
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
