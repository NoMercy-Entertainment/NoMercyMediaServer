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

using NoMercy.Api.Controllers.File;
using Xunit;

namespace NoMercy.Tests.Api.Images;

/// <summary>
/// The image endpoint is anonymous and joins its route segments into a filesystem
/// path; a plain Replace("/","") missed Windows "..\" traversal. SanitizeSegment
/// reduces any value to a single, separator-free component.
/// </summary>
public class ImageRequestPathTests
{
    [Theory]
    [InlineData(["poster.jpg", "poster.jpg"])]
    [InlineData(["abc123.png", "abc123.png"])]
    [InlineData(["../../etc/passwd", "passwd"])]
    [InlineData(["..\\..\\windows\\win.ini", "win.ini"])]
    [InlineData(["a/b/c.png", "c.png"])]
    [InlineData(["a\\b\\c.png", "c.png"])]
    [InlineData(["..", ""])]
    [InlineData(["foo/..", ""])]
    [InlineData([null, ""])]
    [InlineData(["", ""])]
    public void SanitizeSegment_ReducesToFinalComponent(string? input, string expected)
    {
        Assert.Equal(expected, ImageRequestPath.SanitizeSegment(input));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\x")]
    [InlineData("a/b/c")]
    public void SanitizeSegment_OutputHasNoPathSeparators(string input)
    {
        string result = ImageRequestPath.SanitizeSegment(input);

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
    }
}
