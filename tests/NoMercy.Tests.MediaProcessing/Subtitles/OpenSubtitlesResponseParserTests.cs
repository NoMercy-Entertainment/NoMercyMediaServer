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
        string path = Path.Combine(AppContext.BaseDirectory, "Subtitles", "Fixtures", name);
        SubtitleSearchResponse? response = File.ReadAllText(path).FromXml<SubtitleSearchResponse>();

        response.Should().NotBeNull();
        return response!;
    }

    private static SubtitleSearchResponse WhatIfDutchResponse() =>
        LoadFixture("opensubtitles-search-whatif-s03e05-dut.xml");

    [Fact]
    public void Parse_FindsTheDutchSubtitleInARealResponse()
    {
        List<OpenSubtitlesSearchResult> results =
        [
            .. OpenSubtitlesResponseParser.Parse(WhatIfDutchResponse(), "title"),
        ];

        results.Should().ContainSingle();
        results[0].Language.Should().Be("dut");
    }

    [Fact]
    public void Parse_CarriesTheGzipDownloadLinkTheDownloadEndpointNeeds()
    {
        OpenSubtitlesSearchResult result = OpenSubtitlesResponseParser
            .Parse(WhatIfDutchResponse(), "title")
            .Single();

        result.SubDownloadLink.Should().NotBeNullOrWhiteSpace();
        result.SubDownloadLink.Should().StartWith("https://dl.opensubtitles.org/");
        result.SubDownloadLink.Should().EndWith(".gz");
        result.SubFormat.Should().Be("srt");
    }

    [Fact]
    public void Parse_MapsTheRankingFieldsTheAcquisitionFiltersDependOn()
    {
        OpenSubtitlesSearchResult result = OpenSubtitlesResponseParser
            .Parse(WhatIfDutchResponse(), "title")
            .Single();

        result.SubDownloadsCnt.Should().NotBeNullOrWhiteSpace();
        result.SubFileName.Should().NotBeNullOrWhiteSpace();
        result.MatchedBy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Parse_ReturnsNothingForANullResponse()
    {
        OpenSubtitlesResponseParser.Parse(null, "title").Should().BeEmpty();
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

        OpenSubtitlesResponseParser.Parse(response, "title").Should().BeEmpty();
    }
}
