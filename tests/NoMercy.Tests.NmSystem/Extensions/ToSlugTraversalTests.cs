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

namespace NoMercy.Tests.NmSystem.Extensions;

/// <summary>
/// The Album/Artist/Playlist cover-upload endpoints build their on-disk filename
/// as <c>{name.ToSlug()}.jpg</c>. ToSlug being traversal-safe is what makes those
/// writes safe from a path-traversal filename (CVE-2026-35031 class). Lock that:
/// no separator, drive-colon, dot-dot, or null byte may survive slugging.
/// </summary>
[Trait("Category", "Unit")]
public class ToSlugTraversalTests
{
    [Theory]
    [InlineData("../../evil")]
    [InlineData("..\\..\\evil.exe")]
    [InlineData("a/b/c")]
    [InlineData("....//....//etc/passwd")]
    [InlineData("con:$te/xt")]
    [InlineData("/absolute/path")]
    [InlineData("C:\\Windows\\System32")]
    public void ToSlug_StripsSeparatorsAndTraversal(string input)
    {
        string slug = input.ToSlug();

        Assert.DoesNotContain('/', slug);
        Assert.DoesNotContain('\\', slug);
        Assert.DoesNotContain(':', slug);
        Assert.DoesNotContain("..", slug);
        // Only slug-safe characters remain (lowercase alphanumerics, dash, underscore).
        Assert.Matches("^[a-z0-9_-]*$", slug);
    }

    [Fact]
    public void ToSlug_RemovesNullBytes()
    {
        Assert.DoesNotContain('\0', "movie\0title".ToSlug());
    }
}
