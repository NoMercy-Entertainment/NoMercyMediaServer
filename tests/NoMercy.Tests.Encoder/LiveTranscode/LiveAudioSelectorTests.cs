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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// LiveAudioSelector maps a viewer's language preference onto a source's audio
/// streams. The behaviour that matters: a library-preferred language beats the
/// file's own default track (the anime-defaults-to-Japanese case), the default
/// flag is the fallback, and ISO 639-2 stream tags (both /B and /T forms) match
/// the 639-1 codes libraries store.
/// </summary>
public class LiveAudioSelectorTests
{
    private static AudioStreamInfo Stream(int index, string? language, bool isDefault = false) =>
        new(
            Index: index,
            Codec: "aac",
            Channels: 2,
            SampleRate: 48000,
            BitRateKbps: 128,
            Language: language,
            IsDefault: isDefault,
            IsForced: false
        );

    [Fact]
    public void Select_PrefersLibraryLanguage_OverFileDefault()
    {
        // Anime: Japanese is the file's default (index 0), English is index 1.
        AudioStreamInfo[] streams = [Stream(index: 0, language: "jpn", isDefault: true), Stream(index: 1, language: "eng")];

        int index = LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: ["en"]);

        index
            .Should()
            .Be(expected: 1, because: "an English-configured library opens the English track, not the default");
    }

    [Fact]
    public void Select_MatchesBibliographicIso6392_Tag()
    {
        // ffmpeg tags Dutch as the /B form "dut"; the library stores "nl".
        AudioStreamInfo[] streams = [Stream(index: 0, language: "eng", isDefault: true), Stream(index: 1, language: "dut")];

        LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: ["nl"]).Should().Be(expected: 1);
    }

    [Fact]
    public void Select_HonoursPreferenceOrder()
    {
        AudioStreamInfo[] streams = [Stream(index: 0, language: "eng"), Stream(index: 1, language: "jpn"), Stream(index: 2, language: "spa")];

        // Spanish first in the preference list wins even though English is stream 0.
        LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: ["es", "en"]).Should().Be(expected: 2);
    }

    [Fact]
    public void Select_NoPreferredMatch_FallsBackToSourceDefault()
    {
        AudioStreamInfo[] streams = [Stream(index: 0, language: "jpn"), Stream(index: 1, language: "fra", isDefault: true)];

        LiveAudioSelector
            .Select(audioStreams: streams, preferredIso6391: ["en"])
            .Should()
            .Be(expected: 1, because: "no English track exists, so honour the file's default");
    }

    [Fact]
    public void Select_NoPreferredAndNoDefault_FallsBackToFirst()
    {
        AudioStreamInfo[] streams = [Stream(index: 0, language: "jpn"), Stream(index: 1, language: "kor")];

        LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: ["en"]).Should().Be(expected: 0);
    }

    [Fact]
    public void Select_EmptyPreferences_UsesSourceDefault()
    {
        AudioStreamInfo[] streams = [Stream(index: 0, language: "jpn"), Stream(index: 1, language: "eng", isDefault: true)];

        LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: []).Should().Be(expected: 1);
    }

    [Fact]
    public void Select_NoAudioStreams_ReturnsZero()
    {
        LiveAudioSelector.Select(audioStreams: [], preferredIso6391: ["en"]).Should().Be(expected: 0);
    }

    [Fact]
    public void Select_ExactCodeMatch_WhenTagAlreadyIso6391()
    {
        // Some muxers write the 639-1 code directly.
        AudioStreamInfo[] streams = [Stream(index: 0, language: "ja"), Stream(index: 1, language: "en")];

        LiveAudioSelector.Select(audioStreams: streams, preferredIso6391: ["en"]).Should().Be(expected: 1);
    }
}
