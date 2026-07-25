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

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.App.EmbeddedStaticAssets;
using Xunit;

namespace NoMercy.Tests.App.EmbeddedStaticAssets;

/// <summary>
/// REQUIREMENT: all three <c>UseEmbeddedStaticAssets</c> overloads must wire
/// the middleware into the pipeline with the SAME real behavior — the
/// options-object overload is the one production code calls with a
/// configure delegate (see <c>NoMercy.App.Program</c>), and the other two
/// overloads must reduce to it rather than diverging. These tests build a
/// real <see cref="Microsoft.AspNetCore.Builder.ApplicationBuilder"/> pipeline
/// (no TestServer needed — <c>IApplicationBuilder.Build()</c> already yields a
/// real <see cref="RequestDelegate"/>) and drive it with a real
/// <see cref="DefaultHttpContext"/>, so the assembly-resolution and
/// configure-delegate application are exercised for real, not mocked.
/// </summary>
public sealed class EmbeddedStaticAssetsExtensionsTests
{
    private static IApplicationBuilder CreateBuilder()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        return new ApplicationBuilder(services.BuildServiceProvider());
    }

    private static DefaultHttpContext CreateContext(string path, IServiceProvider services)
    {
        DefaultHttpContext context = new() { RequestServices = services };
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        MemoryStream stream = (MemoryStream)context.Response.Body;
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task UseEmbeddedStaticAssets_ExplicitAssemblyOverload_ServesRealEmbeddedAsset()
    {
        IApplicationBuilder builder = CreateBuilder();
        builder.UseEmbeddedStaticAssets(Assembly.GetExecutingAssembly());
        RequestDelegate pipeline = builder.Build();

        DefaultHttpContext context = CreateContext("/index.html", builder.ApplicationServices);
        await pipeline(context);

        ReadBody(context).Should().Contain("Fixture Index");
    }

    [Fact]
    public async Task UseEmbeddedStaticAssets_ConfigureActionOverload_AppliesInjectedScript()
    {
        IApplicationBuilder builder = CreateBuilder();
        builder.UseEmbeddedStaticAssets(
            options => options.InjectScripts.Add("/injected-by-configure-action.js"),
            Assembly.GetExecutingAssembly()
        );
        RequestDelegate pipeline = builder.Build();

        DefaultHttpContext context = CreateContext("/index.html", builder.ApplicationServices);
        await pipeline(context);

        ReadBody(context).Should().Contain("/injected-by-configure-action.js");
    }

    [Fact]
    public async Task UseEmbeddedStaticAssets_OptionsObjectOverload_UsesSuppliedOptions()
    {
        EmbeddedStaticAssetsOptions options = new();
        options.InjectMetaTags.Add("<meta name=\"from-options-object\" content=\"1\">");
        IApplicationBuilder builder = CreateBuilder();
        builder.UseEmbeddedStaticAssets(options, Assembly.GetExecutingAssembly());
        RequestDelegate pipeline = builder.Build();

        DefaultHttpContext context = CreateContext("/index.html", builder.ApplicationServices);
        await pipeline(context);

        ReadBody(context).Should().Contain("from-options-object");
    }

    [Fact]
    public async Task UseEmbeddedStaticAssets_CustomEmbeddedResourceRoot_ServesFromThatRoot()
    {
        IApplicationBuilder builder = CreateBuilder();
        builder.UseEmbeddedStaticAssets(Assembly.GetExecutingAssembly(), "wwwroot/pages");
        RequestDelegate pipeline = builder.Build();

        DefaultHttpContext context = CreateContext("/nested.html", builder.ApplicationServices);
        await pipeline(context);

        ReadBody(context).Should().Contain("Nested Fixture Page");
    }

    [Fact]
    public async Task UseEmbeddedStaticAssets_NoMatchingAsset_FallsThroughToNextMiddleware()
    {
        IApplicationBuilder builder = CreateBuilder();
        builder.UseEmbeddedStaticAssets(Assembly.GetExecutingAssembly(), "wwwroot/pages");
        bool[] reachedTerminal = [false];
        builder.Run(_ =>
        {
            reachedTerminal[0] = true;
            return Task.CompletedTask;
        });
        RequestDelegate pipeline = builder.Build();

        // "wwwroot/pages" is a real embedded scope, but it has no
        // missing-asset.txt — has an extension, so the SPA (index.html)
        // fallback must NOT kick in and the request must reach the terminal.
        DefaultHttpContext context = CreateContext(
            "/missing-asset.txt",
            builder.ApplicationServices
        );
        await pipeline(context);

        reachedTerminal[0].Should().BeTrue();
    }

    /// <summary>
    /// REQUIREMENT (real finding, not a live bug): the only production call
    /// site (<c>NoMercy.App.Program</c>) always passes an explicit assembly,
    /// so the documented "defaults to the entry assembly" convenience path is
    /// never exercised today. But it IS public API, and this pins its actual
    /// failure mode for anyone who calls it bare: <see cref="ManifestEmbeddedFileProvider"/>
    /// throws immediately at pipeline-construction time when the resolved
    /// assembly has no <c>GenerateEmbeddedFilesManifest</c> output — there is
    /// no graceful degrade. A future caller relying on the XML-doc default
    /// without an embedded manifest on their own entry assembly will crash
    /// app startup, not silently fall through.
    /// </summary>
    [Fact]
    public void UseEmbeddedStaticAssets_NoAssemblySpecified_ThrowsWhenResolvedAssemblyHasNoManifest()
    {
        IApplicationBuilder builder = CreateBuilder();

        Action act = () => builder.UseEmbeddedStaticAssets();

        act.Should().Throw<InvalidOperationException>();
    }
}
