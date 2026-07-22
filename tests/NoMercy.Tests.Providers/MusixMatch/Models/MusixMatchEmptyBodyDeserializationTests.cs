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

using Newtonsoft.Json;
using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Tests.Providers.MusixMatch.Models;

/// <summary>
/// Musixmatch's PHP backend serializes an empty macro body as <c>[]</c> instead of
/// <c>{}</c>. These deserialize the raw payload (not hand-built objects) so they
/// actually exercise the path that threw before the ObjectOrEmptyArrayConverter.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MusixMatchEmptyBodyDeserializationTests
{
    [Fact]
    public void MacroBodyAsEmptyArray_DeserializesWithoutThrowing_AndMapsToNull()
    {
        const string json = """
            {
              "message": {
                "header": { "status_code": 200 },
                "body": {
                  "macro_calls": {
                    "matcher.track.get":   { "message": { "header": { "status_code": 200 }, "body": [] } },
                    "track.lyrics.get":    { "message": { "header": { "status_code": 200 }, "body": [] } },
                    "track.snippet.get":   { "message": { "header": { "status_code": 200 }, "body": [] } },
                    "track.subtitles.get": { "message": { "header": { "status_code": 200 }, "body": [] } }
                  }
                }
              }
            }
            """;

        MusixMatchSubtitleGet? response = JsonConvert.DeserializeObject<MusixMatchSubtitleGet>(
            value: json
        );

        response.Should().NotBeNull();
        response!
            .Message!.Body!.MacroCalls!.TrackSubtitlesGet!.Message!.Body!.SubtitleList.Should()
            .BeEmpty();

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response: response);
        candidate.Should().BeNull();
    }

    [Fact]
    public void MacroBodyAsObject_StillDeserializesNormally()
    {
        const string json = """
            {
              "message": {
                "header": { "status_code": 200 },
                "body": {
                  "macro_calls": {
                    "track.subtitles.get": {
                      "message": { "header": { "status_code": 200 }, "body": { "subtitle_list": [ {} ] } }
                    }
                  }
                }
              }
            }
            """;

        MusixMatchSubtitleGet? response = JsonConvert.DeserializeObject<MusixMatchSubtitleGet>(
            value: json
        );

        response.Should().NotBeNull();
        response!
            .Message!.Body!.MacroCalls!.TrackSubtitlesGet!.Message!.Body!.SubtitleList.Should()
            .HaveCount(expected: 1);
    }
}
