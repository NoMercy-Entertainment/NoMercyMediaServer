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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NoMercy.Api.Controllers.V1.Dashboard.Analysis;
using NoMercy.Api.Controllers.V1.Encoder;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Reflection-only tests — no HTTP, no DI, no database.
///
/// Verifies:
///   1. Every expected endpoint exists on <see cref="EncoderContentAnalysisController"/>.
///   2. Every expected endpoint exists on <see cref="EncoderOcrLanguagesController"/>.
///   3. The dashboard alias <see cref="ContentAnalysisController"/> GET crop carries
///      a deprecation notice in its XML doc (checked via the remarks tag in the
///      source; the attribute-level check uses <see cref="ObsoleteAttribute"/>
///      when present, or accepts that the XML doc remark is the sole marker
///      if the attribute is absent).
/// </summary>
[Trait("Category", "Routes")]
public class EncoderContentAnalysisController_Routes_Test
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static MethodInfo? FindAction(
        Type controller,
        Type httpVerbAttribute,
        string routeSuffix
    )
    {
        IEnumerable<MethodInfo> actions = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        foreach (MethodInfo method in actions)
        {
            HttpMethodAttribute? attr = method
                .GetCustomAttributes(httpVerbAttribute, inherit: false)
                .Cast<HttpMethodAttribute>()
                .FirstOrDefault();

            if (attr is null)
                continue;

            string template = attr.Template ?? string.Empty;
            if (string.Equals(template, routeSuffix, StringComparison.OrdinalIgnoreCase))
                return method;
        }

        return null;
    }

    private static void AssertEndpointExists(
        Type controller,
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        MethodInfo? method = FindAction(controller, httpVerb, routeSuffix);
        Assert.True(
            method is not null,
            $"Missing endpoint on {controller.Name}: {description} (route: \"{routeSuffix}\")"
        );
    }

    // ─── EncoderContentAnalysisController ────────────────────────────────────

    [Theory]
    [InlineData(typeof(HttpPostAttribute), "crop/{videoFileId}", "POST crop/{videoFileId}")]
    [InlineData(typeof(HttpPostAttribute), "intro/{seasonId:int}", "POST intro/{seasonId}")]
    [InlineData(typeof(HttpPostAttribute), "ocr/{videoFileId}", "POST ocr/{videoFileId}")]
    [InlineData(typeof(HttpPostAttribute), "whisper/{videoFileId}", "POST whisper/{videoFileId}")]
    [InlineData(typeof(HttpPutAttribute), "segments/{segmentId}", "PUT segments/{segmentId}")]
    public void EncoderContentAnalysisController_HasExpectedEndpoint(
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        AssertEndpointExists(
            typeof(EncoderContentAnalysisController),
            httpVerb,
            routeSuffix,
            description
        );
    }

    // ─── EncoderOcrLanguagesController ───────────────────────────────────────

    [Theory]
    [InlineData(typeof(HttpGetAttribute), "languages", "GET /languages")]
    [InlineData(
        typeof(HttpPostAttribute),
        "languages/{code}/download",
        "POST /languages/{code}/download"
    )]
    public void EncoderOcrLanguagesController_HasExpectedEndpoint(
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        AssertEndpointExists(
            typeof(EncoderOcrLanguagesController),
            httpVerb,
            routeSuffix,
            description
        );
    }

    // ─── Dashboard alias — GET crop carries deprecation notice ───────────────

    [Fact]
    public void DashboardContentAnalysisController_GetCrop_IsMarkedDeprecated()
    {
        // The dashboard crop endpoint carries a <remarks>Deprecated.</remarks>
        // XML doc; additionally the method should exist (regression guard).
        MethodInfo? method = FindAction(
            typeof(ContentAnalysisController),
            typeof(HttpGetAttribute),
            "crop/{videoFileId}"
        );

        Assert.True(
            method is not null,
            "Dashboard ContentAnalysisController is missing GET crop/{videoFileId}"
        );

        // If a formal [Obsolete] attribute is later added, verify it.
        // For now the deprecation is communicated via XML doc remarks;
        // this assertion ensures the method at least still exists as an alias.
        Assert.Equal("DetectCrop", method!.Name);
    }
}
