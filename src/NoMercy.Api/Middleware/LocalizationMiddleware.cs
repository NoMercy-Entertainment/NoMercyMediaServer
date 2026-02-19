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
        string userLanguages = context.Request.Headers["Accept-Language"].ToString();

        // if the language string does not match the format "{language}-{country}" we add the uppercase version of the language
        if (!userLanguages.Contains("-")) userLanguages = userLanguages + "-" + userLanguages.ToUpper();

        string[]? firstLang = userLanguages.Split(',').FirstOrDefault()?.Split('-');

        if (firstLang is not null && firstLang.Length > 0)
            context.Request.Headers.AcceptLanguage = firstLang;
        else
            context.Request.Headers.AcceptLanguage = "en-US".Split('-');

        string language = firstLang?.FirstOrDefault() ?? "en";

        Localizer localizer = LocalizerCache.GetOrAdd(language, lang =>
        {
            Localizer newLocalizer = new();
            newLocalizer.LoadXML(ResourceAssembly, "Resources.I18N.xml", lang);
            return newLocalizer;
        });

        LocalizationHelper.GlobalLocalizer = localizer;

        await _next(context);
    }
}