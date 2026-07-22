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

using System.Collections.Concurrent;
using System.Reflection;
using I18N.DotNet;
using Microsoft.AspNetCore.Http;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Middleware;

public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, Localizer> LocalizerCache = new();
    private static readonly Assembly ResourceAssembly = typeof(LocalizationMiddleware).Assembly;

    public LocalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string userLanguages = context.Request.Headers[key: "Accept-Language"].ToString();

        // Empty / wildcard header — fall straight to en-US so the cache key
        // is never built from an empty/garbage prefix and downstream code
        // sees a real locale.
        if (string.IsNullOrWhiteSpace(value: userLanguages) || userLanguages == "*")
        {
            context.Request.Headers.AcceptLanguage = "en-US".Split(separator: '-');
            LocalizationHelper.GlobalLocalizer = LocalizerCache.GetOrAdd(key: "en", valueFactory: LoadLocalizer);
            await _next(context: context);
            return;
        }

        // Pick the highest quality-weighted language tag per RFC 7231 rather
        // than blindly taking the first comma-separated entry.
        string bestLanguage = ParseBestLanguage(acceptLanguageHeader: userLanguages);

        // if the language string does not match the format "{language}-{country}"
        // we add the uppercase version of the language
        if (!bestLanguage.Contains(value: "-"))
            bestLanguage = bestLanguage + "-" + bestLanguage.ToUpper();

        string[] langParts = bestLanguage.Split(separator: '-');

        context.Request.Headers.AcceptLanguage =
            langParts.Length > 0 && !string.IsNullOrEmpty(value: langParts[0])
                ? langParts
                : "en-US".Split(separator: '-');

        string language = langParts.FirstOrDefault() ?? "en";
        if (string.IsNullOrEmpty(value: language))
            language = "en";

        Localizer localizer = LocalizerCache.GetOrAdd(key: language, valueFactory: LoadLocalizer);

        LocalizationHelper.GlobalLocalizer = localizer;

        await _next(context: context);
    }

    /// <summary>
    /// Selects the best language tag from an Accept-Language header by RFC 7231
    /// quality weight (q=). Falls back to "en-US" when the header is empty or
    /// only contains the "*" wildcard. Ties keep header order.
    /// </summary>
    public static string ParseBestLanguage(string acceptLanguageHeader)
    {
        if (string.IsNullOrEmpty(value: acceptLanguageHeader))
            return "en-US";

        return acceptLanguageHeader
                .Split(separator: ',', options: StringSplitOptions.RemoveEmptyEntries)
                .Select(selector: part =>
                {
                    ReadOnlySpan<char> p = part.AsSpan().Trim();
                    int qIdx = p.IndexOf(value: ";q=", comparisonType: StringComparison.OrdinalIgnoreCase);
                    string lang = (qIdx >= 0 ? p[..qIdx] : p).ToString().Trim();
                    double weight =
                        qIdx >= 0
                        && double.TryParse(
                            s: p[(qIdx + 3)..],
                            style: System.Globalization.NumberStyles.Float,
                            provider: System.Globalization.CultureInfo.InvariantCulture,
                            result: out double q
                        )
                            ? q
                            : 1.0;
                    return (lang, weight);
                })
                .Where(predicate: x => x.lang != "*")
                .OrderByDescending(keySelector: x => x.weight)
                .Select(selector: x => x.lang)
                .FirstOrDefault()
            ?? "en-US";
    }

    private static Localizer LoadLocalizer(string lang)
    {
        Localizer newLocalizer = new();
        newLocalizer.LoadXML(assembly: ResourceAssembly, resourceName: "Resources.I18N.xml", language: lang);
        return newLocalizer;
    }
}
