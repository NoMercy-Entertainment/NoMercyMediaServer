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
            index,
            "aac",
            2,
            48000,
            128,
            language,
            isDefault,
            false
        );

    [Fact]
    public void Select_PrefersLibraryLanguage_OverFileDefault()
    {
        // Anime: Japanese is the file's default (index 0), English is index 1.
        AudioStreamInfo[] streams = [Stream(0, "jpn", true), Stream(1, "eng")];

        int index = LiveAudioSelector.Select(streams, ["en"]);

        index
            .Should()
            .Be(1, "an English-configured library opens the English track, not the default");
    }

    [Fact]
    public void Select_MatchesBibliographicIso6392_Tag()
    {
        // ffmpeg tags Dutch as the /B form "dut"; the library stores "nl".
        AudioStreamInfo[] streams = [Stream(0, "eng", true), Stream(1, "dut")];

        LiveAudioSelector.Select(streams, ["nl"]).Should().Be(1);
    }

    [Fact]
    public void Select_HonoursPreferenceOrder()
    {
        AudioStreamInfo[] streams = [Stream(0, "eng"), Stream(1, "jpn"), Stream(2, "spa")];

        // Spanish first in the preference list wins even though English is stream 0.
        LiveAudioSelector.Select(streams, ["es", "en"]).Should().Be(2);
    }

    [Fact]
    public void Select_NoPreferredMatch_FallsBackToSourceDefault()
    {
        AudioStreamInfo[] streams = [Stream(0, "jpn"), Stream(1, "fra", true)];

        LiveAudioSelector
            .Select(streams, ["en"])
            .Should()
            .Be(1, "no English track exists, so honour the file's default");
    }

    [Fact]
    public void Select_NoPreferredAndNoDefault_FallsBackToFirst()
    {
        AudioStreamInfo[] streams = [Stream(0, "jpn"), Stream(1, "kor")];

        LiveAudioSelector.Select(streams, ["en"]).Should().Be(0);
    }

    [Fact]
    public void Select_EmptyPreferences_UsesSourceDefault()
    {
        AudioStreamInfo[] streams = [Stream(0, "jpn"), Stream(1, "eng", true)];

        LiveAudioSelector.Select(streams, []).Should().Be(1);
    }

    [Fact]
    public void Select_NoAudioStreams_ReturnsZero()
    {
        LiveAudioSelector.Select([], ["en"]).Should().Be(0);
    }

    [Fact]
    public void Select_ExactCodeMatch_WhenTagAlreadyIso6391()
    {
        // Some muxers write the 639-1 code directly.
        AudioStreamInfo[] streams = [Stream(0, "ja"), Stream(1, "en")];

        LiveAudioSelector.Select(streams, ["en"]).Should().Be(1);
    }
}
