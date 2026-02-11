using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using NoMercy.Api.Middleware;
using NoMercy.NmSystem;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Unit")]
public class LocalizationMiddlewareTests
{
    [Fact]
    public void ApplicationConfiguration_HasSingleUseRequestLocalizationCall()
    {
        string sourceFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "NoMercy.Server", "AppConfig", "ApplicationConfiguration.cs");

        string source = File.ReadAllText(Path.GetFullPath(sourceFile));

        int count = Regex.Matches(source, @"UseRequestLocalization\s*\(").Count;

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InvokeAsync_SetsGlobalLocalizer_ForRequestLanguage()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers["Accept-Language"] = "nl-NL";

        await middleware.InvokeAsync(context);

        Assert.NotNull(LocalizationHelper.GlobalLocalizer);
        Assert.Equal("nl", LocalizationHelper.GlobalLocalizer.TargetLanguage);
    }

    [Fact]
    public async Task InvokeAsync_SetsLocalizer_WhenNoAcceptLanguageHeader()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        Assert.NotNull(LocalizationHelper.GlobalLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_ReusesCachedLocalizer_ForSameLanguage()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);

        DefaultHttpContext context1 = new();
        context1.Request.Headers["Accept-Language"] = "de-DE";
        await middleware.InvokeAsync(context1);
        I18N.DotNet.ILocalizer firstLocalizer = LocalizationHelper.GlobalLocalizer;

        DefaultHttpContext context2 = new();
        context2.Request.Headers["Accept-Language"] = "de-DE";
        await middleware.InvokeAsync(context2);
        I18N.DotNet.ILocalizer secondLocalizer = LocalizationHelper.GlobalLocalizer;

        Assert.Same(firstLocalizer, secondLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_CreatesDifferentLocalizer_ForDifferentLanguage()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);

        DefaultHttpContext context1 = new();
        context1.Request.Headers["Accept-Language"] = "fr-FR";
        await middleware.InvokeAsync(context1);
        I18N.DotNet.ILocalizer frLocalizer = LocalizationHelper.GlobalLocalizer;

        DefaultHttpContext context2 = new();
        context2.Request.Headers["Accept-Language"] = "es-ES";
        await middleware.InvokeAsync(context2);
        I18N.DotNet.ILocalizer esLocalizer = LocalizationHelper.GlobalLocalizer;

        Assert.NotSame(frLocalizer, esLocalizer);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        bool nextCalled = false;
        LocalizationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = new();
        context.Request.Headers["Accept-Language"] = "en-US";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SetsAcceptLanguageHeader_WithLanguageParts()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers["Accept-Language"] = "nl-NL,en-US;q=0.9";

        await middleware.InvokeAsync(context);

        string[] acceptLanguage = context.Request.Headers.AcceptLanguage.ToArray();
        Assert.Contains("nl", acceptLanguage);
        Assert.Contains("NL", acceptLanguage);
    }

    [Fact]
    public async Task InvokeAsync_HandlesLanguageWithoutRegion()
    {
        LocalizationMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers["Accept-Language"] = "nl";

        await middleware.InvokeAsync(context);

        Assert.Equal("nl", LocalizationHelper.GlobalLocalizer.TargetLanguage);
    }
}
