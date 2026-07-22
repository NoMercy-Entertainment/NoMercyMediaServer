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
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NoMercy.Api.Controllers.V1.Dashboard.Encoder;
using NoMercy.Api.Controllers.V1.Encoder;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Static reflection tests — no DI, no HTTP, no database.
///
/// Purpose: guarantee the expected route set exists on both controllers and
/// that every legacy endpoint carries [Obsolete]. These tests catch future
/// regressions where someone adds a new endpoint without registering it on
/// both sides or forgets the Obsolete marker.
/// </summary>
[Trait(name: "Category", value: "Routes")]
public class EncoderProfilesController_Routes_Test
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static IEnumerable<MethodInfo> PublicActions(Type controller) =>
        controller
            .GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(predicate: m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>
    /// Finds an action method that carries the given HTTP verb attribute whose
    /// Template matches <paramref name="routeSuffix"/> (empty string for
    /// parameterless verbs like GET /).
    /// </summary>
    private static MethodInfo? FindAction(
        Type controller,
        Type httpVerbAttribute,
        string routeSuffix
    )
    {
        foreach (MethodInfo method in PublicActions(controller: controller))
        {
            HttpMethodAttribute? attr = method
                .GetCustomAttributes(attributeType: httpVerbAttribute, inherit: false)
                .Cast<HttpMethodAttribute>()
                .FirstOrDefault();

            if (attr is null)
                continue;

            // Null template means the route is the controller base (index).
            string template = attr.Template ?? string.Empty;
            if (
                string.Equals(
                    a: StripConstraints(template: template),
                    b: StripConstraints(template: routeSuffix),
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
                return method;
        }

        return null;
    }

    /// <summary>
    /// Drops route-parameter constraints so the route SET is compared by verb +
    /// path structure, not by constraint syntax. "{id:ulid}" → "{id}" — the
    /// endpoint still exists at /{id}; whether it's ulid-constrained is a routing
    /// detail this test doesn't guard.
    /// </summary>
    private static string StripConstraints(string template) =>
        Regex.Replace(input: template, pattern: @"\{(\w+):[^}]+\}", replacement: "{$1}");

    private static void AssertEndpointExists(
        Type controller,
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        MethodInfo? method = FindAction(controller: controller, httpVerbAttribute: httpVerb, routeSuffix: routeSuffix);
        Assert.True(
            condition: method is not null,
            userMessage: $"Missing endpoint on {controller.Name}: {description} (route suffix: \"{routeSuffix}\")"
        );
    }

    private static void AssertEndpointObsolete(
        Type controller,
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        MethodInfo? method = FindAction(controller: controller, httpVerbAttribute: httpVerb, routeSuffix: routeSuffix);
        Assert.True(
            condition: method is not null,
            userMessage: $"Endpoint not found on {controller.Name}: {description} — cannot check [Obsolete]"
        );

        ObsoleteAttribute? obsolete = method!.GetCustomAttribute<ObsoleteAttribute>();
        Assert.True(
            condition: obsolete is not null,
            userMessage: $"Endpoint {controller.Name}.{method.Name} ({description}) is missing [Obsolete]"
        );
    }

    // =========================================================================
    // EncoderProfilesController — expected full CRUD surface
    // =========================================================================

    [Theory]
    [InlineData(data: [typeof(HttpGetAttribute), "", "GET / (index)"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}", "GET /{id}"])]
    [InlineData(data: [typeof(HttpPostAttribute), "", "POST / (create)"])]
    [InlineData(data: [typeof(HttpGetAttribute), "tags", "GET /tags"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}/resolve", "GET /{id}/resolve"])]
    [InlineData(data: [typeof(HttpDeleteAttribute), "{id}", "DELETE /{id}"])]
    [InlineData(data: [typeof(HttpPostAttribute), "validate", "POST /validate"])]
    [InlineData(data: [typeof(HttpPostAttribute), "{id}/preview", "POST /{id}/preview"])]
    [InlineData(data: [typeof(HttpPutAttribute), "{id}", "PUT /{id}"])]
    [InlineData(data: [typeof(HttpPostAttribute), "{parentId}/clone", "POST /{parentId}/clone"])]
    [InlineData(data: [typeof(HttpPostAttribute), "import", "POST /import"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}/export", "GET /{id}/export"])]
    public void EncoderProfilesController_HasExpectedEndpoint(
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        AssertEndpointExists(controller: typeof(EncoderProfilesController), httpVerb: httpVerb, routeSuffix: routeSuffix, description: description);
    }

    // =========================================================================
    // EncodingPresetsController — every action must carry [Obsolete]
    // =========================================================================

    [Theory]
    [InlineData(data: [typeof(HttpGetAttribute), "", "GET / (list)"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}", "GET /{id}"])]
    [InlineData(data: [typeof(HttpPostAttribute), "", "POST / (create)"])]
    [InlineData(data: [typeof(HttpPutAttribute), "{id}", "PUT /{id}"])]
    [InlineData(data: [typeof(HttpGetAttribute), "tags", "GET /tags"])]
    [InlineData(data: [typeof(HttpPostAttribute), "{id}/clone", "POST /{id}/clone"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}/resolve", "GET /{id}/resolve"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}/export", "GET /{id}/export"])]
    [InlineData(data: [typeof(HttpPostAttribute), "import", "POST /import"])]
    [InlineData(data: [typeof(HttpPostAttribute), "import-url", "POST /import-url"])]
    [InlineData(data: [typeof(HttpPostAttribute), "validate", "POST /validate"])]
    [InlineData(data: [typeof(HttpPostAttribute), "preview", "POST /preview"])]
    [InlineData(data: [typeof(HttpDeleteAttribute), "{id}", "DELETE /{id}"])]
    public void EncodingPresetsController_EndpointIsObsolete(
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        AssertEndpointObsolete(
            controller: typeof(EncodingPresetsController),
            httpVerb: httpVerb,
            routeSuffix: routeSuffix,
            description: description
        );
    }
}
