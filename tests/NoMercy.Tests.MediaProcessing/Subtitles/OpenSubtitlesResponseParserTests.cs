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

using FluentAssertions;
using NoMercy.Encoder.Subtitles;
using NoMercy.MediaProcessing.Subtitles;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.OpenSubtitles.Models;

namespace NoMercy.Tests.MediaProcessing.Subtitles;

/// <summary>
/// Drives the parser with a real SearchSubtitles response captured from api.opensubtitles.org
/// (session token redacted). The envelope nests every result inside its "data" member, so a
/// parser reading the param level returns nothing over a response full of subtitles.
/// </summary>
public class OpenSubtitlesResponseParserTests
{
    private static SubtitleSearchResponse LoadFixture(string name)
    {
        string path = Path.Combine(path1: AppContext.BaseDirectory, path2: "Subtitles", path3: "Fixtures", path4: name);
        SubtitleSearchResponse? response = File.ReadAllText(path: path).FromXml<SubtitleSearchResponse>();

        response.Should().NotBeNull();
        return response!;
    }

    private static SubtitleSearchResponse WhatIfDutchResponse() =>
        LoadFixture(name: "opensubtitles-search-whatif-s03e05-dut.xml");

    [Fact]
    public void Parse_FindsTheDutchSubtitleInARealResponse()
    {
        List<OpenSubtitlesSearchResult> results = OpenSubtitlesResponseParser
            .Parse(response: WhatIfDutchResponse(), matchedBy: "title")
            .ToList();

        results.Should().ContainSingle();
        results[index: 0].Language.Should().Be(expected: "dut");
    }

    [Fact]
    public void Parse_CarriesTheGzipDownloadLinkTheDownloadEndpointNeeds()
    {
        OpenSubtitlesSearchResult result = OpenSubtitlesResponseParser
            .Parse(response: WhatIfDutchResponse(), matchedBy: "title")
            .Single();

        result.SubDownloadLink.Should().NotBeNullOrWhiteSpace();
        result.SubDownloadLink.Should().StartWith(expected: "https://dl.opensubtitles.org/");
        result.SubDownloadLink.Should().EndWith(expected: ".gz");
        result.SubFormat.Should().Be(expected: "srt");
    }

    [Fact]
    public void Parse_MapsTheRankingFieldsTheAcquisitionFiltersDependOn()
    {
        OpenSubtitlesSearchResult result = OpenSubtitlesResponseParser
            .Parse(response: WhatIfDutchResponse(), matchedBy: "title")
            .Single();

        result.SubDownloadsCnt.Should().NotBeNullOrWhiteSpace();
        result.SubFileName.Should().NotBeNullOrWhiteSpace();
        result.MatchedBy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Parse_ReturnsNothingForANullResponse()
    {
        OpenSubtitlesResponseParser.Parse(response: null, matchedBy: "title").Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReturnsNothingWhenTheEnvelopeCarriesNoDataMember()
    {
        SubtitleSearchResponse response = """
            <?xml version="1.0" encoding="utf-8"?>
            <methodResponse><params><param><value><struct>
              <member><name>status</name><value><string>200 OK</string></value></member>
              <member><name>seconds</name><value><double>0.01</double></value></member>
            </struct></value></param></params></methodResponse>
            """.FromXml<SubtitleSearchResponse>()!;

        OpenSubtitlesResponseParser.Parse(response: response, matchedBy: "title").Should().BeEmpty();
    }
}
