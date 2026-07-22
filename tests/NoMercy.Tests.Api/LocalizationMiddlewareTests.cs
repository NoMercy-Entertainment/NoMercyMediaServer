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
using I18N.DotNet;
using Microsoft.AspNetCore.Http;
using NoMercy.Api.Middleware;
using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait(name: "Category", value: "Unit")]
public class LocalizationMiddlewareTests
{
    [Theory]
    [InlineData(data: ["en-US,nl;q=0.9", "en-US"])]
    [InlineData(data: ["nl;q=1.0,en;q=0.5", "nl"])]
    [InlineData(data: ["nl-NL,nl;q=0.9,en;q=0.8", "nl-NL"])]
    [InlineData(data: ["en;q=0.8,nl;q=0.9", "nl"])]
    [InlineData(data: ["*,nl;q=1.0", "nl"])]
    [InlineData(data: ["", "en-US"])]
    public void ParseBestLanguage_PicksHighestQualityWeight(string header, string expected)
    {
        Assert.Equal(expected: expected, actual: LocalizationMiddleware.ParseBestLanguage(acceptLanguageHeader: header));
    }

    [Fact]
    public void ApplicationConfiguration_HasSingleUseRequestLocalizationCall()
    {
        string sourceFile = FindRepoFile(
            relativePath: Path.Combine(path1: "src", path2: "NoMercy.Service", path3: "Configuration", path4: "ApplicationConfiguration.cs")
        );

        string source = File.ReadAllText(path: sourceFile);

        int count = Regex.Matches(input: source, pattern: @"UseRequestLocalization\s*\(").Count;

        Assert.Equal(expected: 1, actual: count);
    }

    // Walk up from the test assembly instead of a fixed ".." chain — the output
    // directory depth changes under a redirected BaseOutputPath.
    private static string FindRepoFile(string relativePath)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            dir = Path.GetDirectoryName(path: dir)!;
        }

        throw new FileNotFoundException(
            message: $"Could not locate {relativePath} above {AppContext.BaseDirectory}"
        );
    }

    [Fact]
    public async Task InvokeAsync_SetsGlobalLocalizer_ForRequestLanguage()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers[key: "Accept-Language"] = "nl-NL";

        await middleware.InvokeAsync(context: context);

        Assert.NotNull(@object: LocalizationHelper.GlobalLocalizer);
        Assert.Equal(expected: "nl", actual: LocalizationHelper.GlobalLocalizer.TargetLanguage);
    }

    [Fact]
    public async Task InvokeAsync_SetsLocalizer_WhenNoAcceptLanguageHeader()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context: context);

        Assert.NotNull(@object: LocalizationHelper.GlobalLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_ReusesCachedLocalizer_ForSameLanguage()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);

        DefaultHttpContext context1 = new();
        context1.Request.Headers[key: "Accept-Language"] = "de-DE";
        await middleware.InvokeAsync(context: context1);
        ILocalizer firstLocalizer = LocalizationHelper.GlobalLocalizer;

        DefaultHttpContext context2 = new();
        context2.Request.Headers[key: "Accept-Language"] = "de-DE";
        await middleware.InvokeAsync(context: context2);
        ILocalizer secondLocalizer = LocalizationHelper.GlobalLocalizer;

        Assert.Same(expected: firstLocalizer, actual: secondLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_CreatesDifferentLocalizer_ForDifferentLanguage()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);

        DefaultHttpContext context1 = new();
        context1.Request.Headers[key: "Accept-Language"] = "fr-FR";
        await middleware.InvokeAsync(context: context1);
        ILocalizer frLocalizer = LocalizationHelper.GlobalLocalizer;

        DefaultHttpContext context2 = new();
        context2.Request.Headers[key: "Accept-Language"] = "es-ES";
        await middleware.InvokeAsync(context: context2);
        ILocalizer esLocalizer = LocalizationHelper.GlobalLocalizer;

        Assert.NotSame(expected: frLocalizer, actual: esLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        bool nextCalled = false;
        LocalizationMiddleware middleware = new(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = new();
        context.Request.Headers[key: "Accept-Language"] = "en-US";

        await middleware.InvokeAsync(context: context);

        Assert.True(condition: nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SetsAcceptLanguageHeader_WithLanguageParts()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers[key: "Accept-Language"] = "nl-NL,en-US;q=0.9";

        await middleware.InvokeAsync(context: context);

        string?[] acceptLanguage = context.Request.Headers.AcceptLanguage.ToArray();
        Assert.Contains(expected: "nl", collection: acceptLanguage);
        Assert.Contains(expected: "NL", collection: acceptLanguage);
    }

    [Fact]
    public async Task InvokeAsync_HandlesLanguageWithoutRegion()
    {
        LocalizationMiddleware middleware = new(next: _ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers[key: "Accept-Language"] = "nl";

        await middleware.InvokeAsync(context: context);

        Assert.Equal(expected: "nl", actual: LocalizationHelper.GlobalLocalizer.TargetLanguage);
    }
}
