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
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Dashboard.Media;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Database.Models.Libraries;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Reflection-only tests for <see cref="InboxController"/>.
///
/// Verifies:
///   1. Every expected route + verb exists on InboxController.
///   2. InboxAssignRequest binds from a representative JSON payload (contract test).
/// </summary>
[Trait(name: "Category", value: "Routes")]
public class InboxController_Routes_Test
{
    // -------------------------------------------------------------------------
    // Helpers — mirror EncoderProfilesController_Routes_Test
    // -------------------------------------------------------------------------

    private static IEnumerable<MethodInfo> PublicActions(Type controller) =>
        controller
            .GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(predicate: method => method.GetCustomAttributes<HttpMethodAttribute>().Any());

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

    // -------------------------------------------------------------------------
    // Route surface assertions
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: [typeof(HttpGetAttribute), "", "GET / (list)"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}", "GET /{id}"])]
    [InlineData(data: [typeof(HttpGetAttribute), "{id}/matches", "GET /{id}/matches"])]
    [InlineData(data: [typeof(HttpPostAttribute), "{id}/assign", "POST /{id}/assign"])]
    [InlineData(data: [typeof(HttpPostAttribute), "{id}/dismiss", "POST /{id}/dismiss"])]
    [InlineData(data: [typeof(HttpDeleteAttribute), "{id}", "DELETE /{id}"])]
    public void InboxController_HasExpectedEndpoint(
        Type httpVerb,
        string routeSuffix,
        string description
    )
    {
        AssertEndpointExists(controller: typeof(InboxController), httpVerb: httpVerb, routeSuffix: routeSuffix, description: description);
    }

    // -------------------------------------------------------------------------
    // InboxAssignRequest contract test
    // -------------------------------------------------------------------------

    [Fact]
    public void InboxAssignRequest_DeserializesFromClientJson()
    {
        string json = """
            {
              "type": "movie",
              "match": {
                "provider": "tmdb",
                "external_id": "603",
                "title": "The Matrix",
                "year": 1999,
                "poster_path": "/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg",
                "score": 0.97
              },
              "target_library_id": "01HZXY7ABCDEF0123456789012",
              "target_folder_id":  "01HZXY7ABCDEF0123456789013",
              "target_profile_id": "01HZXY7ABCDEF0123456789014"
            }
            """;

        InboxAssignRequest? request = JsonConvert.DeserializeObject<InboxAssignRequest>(value: json);

        Assert.NotNull(@object: request);
        Assert.Equal(expected: "movie", actual: request!.Type);
        Assert.NotNull(@object: request.Match);
        Assert.Equal(expected: "tmdb", actual: request.Match.Provider);
        Assert.Equal(expected: "603", actual: request.Match.ExternalId);
        Assert.Equal(expected: "The Matrix", actual: request.Match.Title);
        Assert.Equal(expected: 1999, actual: request.Match.Year);
        Assert.Equal(expected: 0.97, actual: request.Match.Score, precision: 5);
        Assert.Equal(expected: Ulid.Parse(base32: "01HZXY7ABCDEF0123456789012"), actual: request.TargetLibraryId);
        Assert.Equal(expected: Ulid.Parse(base32: "01HZXY7ABCDEF0123456789013"), actual: request.TargetFolderId);
        Assert.Equal(expected: Ulid.Parse(base32: "01HZXY7ABCDEF0123456789014"), actual: request.TargetProfileId);
    }

    [Fact]
    public void InboxItemDto_ProjectsFromInboxItem()
    {
        Ulid itemId = Ulid.NewUlid();

        InboxItem item = new()
        {
            Id = itemId,
            SourcePath = "inbox/The Matrix (1999).mkv",
            DriverId = Ulid.NewUlid(),
            DetectedType = "movie",
            Confidence = "high",
            Status = "NeedsReview",
            Candidates =
            [
                new()
                {
                    Provider = "tmdb",
                    ExternalId = "603",
                    Title = "The Matrix",
                    Year = 1999,
                    Score = 0.98,
                },
            ],
        };

        InboxItemDto dto = new(item: item);

        Assert.Equal(expected: itemId.ToString(), actual: dto.Id);
        Assert.Equal(expected: "inbox/The Matrix (1999).mkv", actual: dto.SourcePath);
        Assert.Equal(expected: "movie", actual: dto.DetectedType);
        Assert.Equal(expected: "high", actual: dto.Confidence);
        Assert.Equal(expected: "NeedsReview", actual: dto.Status);
        Assert.Single(collection: dto.Candidates);
        Assert.Equal(expected: "The Matrix", actual: dto.Candidates[0].Title);
        Assert.Null(@object: dto.SelectedMatch);
        Assert.Null(@object: dto.TargetLibraryId);
    }
}
