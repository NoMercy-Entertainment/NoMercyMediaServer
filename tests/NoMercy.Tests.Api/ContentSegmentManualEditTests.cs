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
using NoMercy.Api.Controllers.V1.Encoder;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Reflection-based tests that verify the manual-segment-edit endpoint
/// contract without spinning up a database or HTTP server.
///
/// Behavioural guarantees (source flip, auto-skip) are exercised in the
/// encoder test project via the <c>ContentSegmentPersistenceTests</c> suite.
/// </summary>
[Trait(name: "Category", value: "ContentSegments")]
public class ContentSegmentManualEditTests
{
    private static MethodInfo? FindAction(
        Type controller,
        Type httpVerbAttribute,
        string routeSuffix
    )
    {
        foreach (
            MethodInfo method in controller
                .GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(predicate: m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
        )
        {
            HttpMethodAttribute? attr = method
                .GetCustomAttributes(attributeType: httpVerbAttribute, inherit: false)
                .Cast<HttpMethodAttribute>()
                .FirstOrDefault();

            if (attr is null)
                continue;

            string template = attr.Template ?? string.Empty;
            if (string.Equals(a: template, b: routeSuffix, comparisonType: StringComparison.OrdinalIgnoreCase))
                return method;
        }

        return null;
    }

    [Fact]
    public void EditSegment_EndpointExists_OnEncoderContentAnalysisController()
    {
        MethodInfo? method = FindAction(
            controller: typeof(EncoderContentAnalysisController),
            httpVerbAttribute: typeof(HttpPutAttribute),
            routeSuffix: "segments/{segmentId}"
        );

        Assert.True(
            condition: method is not null,
            userMessage: "PUT segments/{segmentId} is missing from EncoderContentAnalysisController"
        );
    }

    [Fact]
    public void EditSegmentRequest_HasStartAndEndSecondsProperties()
    {
        // Verifies the DTO that the endpoint binds from the request body.
        Type dto = typeof(EditSegmentRequest);

        Assert.NotNull(@object: dto.GetProperty(name: nameof(EditSegmentRequest.StartSeconds)));
        Assert.NotNull(@object: dto.GetProperty(name: nameof(EditSegmentRequest.EndSeconds)));
    }
}
