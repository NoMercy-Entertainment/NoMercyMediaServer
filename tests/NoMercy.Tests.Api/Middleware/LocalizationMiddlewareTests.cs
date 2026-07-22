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

using Microsoft.AspNetCore.Http;
using NoMercy.Api.Middleware;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

/// <summary>
/// Coverage for LocalizationMiddleware: the empty/wildcard Accept-Language
/// fallback to "en-US", the RFC 7231 quality-weighted language selection, and
/// the "language without a country" normalization (e.g. "nl" -> "nl-NL").
/// GlobalLocalizer is a process-wide static the middleware sets on every
/// request — asserting immediately after InvokeAsync is safe since nothing
/// else mutates it between the two.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class LocalizationMiddlewareTests
{
    private static async Task<HttpContext> InvokeAsync(string? acceptLanguage)
    {
        DefaultHttpContext context = new();
        if (acceptLanguage is not null)
            context.Request.Headers.AcceptLanguage = acceptLanguage;

        bool nextCalled = false;
        LocalizationMiddleware middleware = new(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context: context);

        nextCalled.Should().BeTrue();
        return context;
    }

    // The middleware sets AcceptLanguage to a string[] of "{language}-{country}".Split('-')
    // — ASP.NET Core stores that as TWO separate header values, so StringValues.ToString()
    // comma-joins them ("nl,NL") rather than reassembling the hyphen. Assert the sequence
    // instead of the joined string so the test states the actual multi-value contract.
    [Fact]
    public async Task MissingHeader_FallsBackToEnUs()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: null);

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["en", "US"]);
    }

    [Fact]
    public async Task WildcardHeader_FallsBackToEnUs()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "*");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["en", "US"]);
    }

    [Fact]
    public async Task WhitespaceHeader_FallsBackToEnUs()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "   ");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["en", "US"]);
    }

    [Fact]
    public async Task LanguageWithoutCountry_AppendsUppercasedLanguageAsCountry()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "nl");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["nl", "NL"]);
    }

    [Fact]
    public async Task LanguageWithCountry_PassesThroughUnchanged()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "fr-CA");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["fr", "CA"]);
    }

    [Fact]
    public async Task MultipleLanguages_HighestQualityWins_EvenWhenListedSecond()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "fr;q=0.5, en;q=0.9");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["en", "EN"]);
    }

    [Fact]
    public async Task MultipleLanguages_NoQualityValues_FirstListedWins()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "de, es");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["de", "DE"]);
    }

    [Fact]
    public async Task WildcardAmongRealLanguages_IsExcludedFromSelection()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "*;q=0.9, nl;q=0.1");

        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["nl", "NL"]);
    }

    [Fact]
    public async Task MalformedQualityValue_TreatedAsDefaultWeightOne()
    {
        HttpContext context = await InvokeAsync(acceptLanguage: "nl;q=not-a-number, fr;q=0.1");

        // "nl"'s unparsable q defaults to weight 1.0 and must still outrank fr's 0.1.
        context.Request.Headers.AcceptLanguage.Should().Equal(expected: ["nl", "NL"]);
    }

    // =========================================================================
    // ParseBestLanguage — the pure static helper directly
    // =========================================================================

    [Fact]
    public void ParseBestLanguage_EmptyString_ReturnsEnUs()
    {
        LocalizationMiddleware.ParseBestLanguage(acceptLanguageHeader: "").Should().Be(expected: "en-US");
    }

    [Fact]
    public void ParseBestLanguage_TiesKeepHeaderOrder()
    {
        LocalizationMiddleware.ParseBestLanguage(acceptLanguageHeader: "de;q=0.8, it;q=0.8").Should().Be(expected: "de");
    }

    [Fact]
    public void ParseBestLanguage_TrimsSurroundingWhitespace()
    {
        LocalizationMiddleware.ParseBestLanguage(acceptLanguageHeader: "  nl  ").Should().Be(expected: "nl");
    }
}
