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

using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.NmSystem.Extensions;

/// <summary>
/// The Album/Artist/Playlist cover-upload endpoints build their on-disk filename
/// as <c>{name.ToSlug()}.jpg</c>. ToSlug being traversal-safe is what makes those
/// writes safe from a path-traversal filename (CVE-2026-35031 class). Lock that:
/// no separator, drive-colon, dot-dot, or null byte may survive slugging.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ToSlugTraversalTests
{
    [Theory]
    [InlineData(data: "../../evil")]
    [InlineData(data: "..\\..\\evil.exe")]
    [InlineData(data: "a/b/c")]
    [InlineData(data: "....//....//etc/passwd")]
    [InlineData(data: "con:$te/xt")]
    [InlineData(data: "/absolute/path")]
    [InlineData(data: "C:\\Windows\\System32")]
    public void ToSlug_StripsSeparatorsAndTraversal(string input)
    {
        string slug = input.ToSlug();

        Assert.DoesNotContain(expected: '/', collection: slug);
        Assert.DoesNotContain(expected: '\\', collection: slug);
        Assert.DoesNotContain(expected: ':', collection: slug);
        Assert.DoesNotContain(expectedSubstring: "..", actualString: slug);
        // Only slug-safe characters remain (lowercase alphanumerics, dash, underscore).
        Assert.Matches(expectedRegexPattern: "^[a-z0-9_-]*$", actualString: slug);
    }

    [Fact]
    public void ToSlug_RemovesNullBytes()
    {
        Assert.DoesNotContain(expected: '\0', collection: "movie\0title".ToSlug());
    }
}
