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
    [InlineData(data: ["poster.jpg", "poster.jpg"])]
    [InlineData(data: ["abc123.png", "abc123.png"])]
    [InlineData(data: ["../../etc/passwd", "passwd"])]
    [InlineData(data: ["..\\..\\windows\\win.ini", "win.ini"])]
    [InlineData(data: ["a/b/c.png", "c.png"])]
    [InlineData(data: ["a\\b\\c.png", "c.png"])]
    [InlineData(data: ["..", ""])]
    [InlineData(data: ["foo/..", ""])]
    [InlineData(data: [null, ""])]
    [InlineData(data: ["", ""])]
    public void SanitizeSegment_ReducesToFinalComponent(string? input, string expected)
    {
        Assert.Equal(expected: expected, actual: ImageRequestPath.SanitizeSegment(segment: input));
    }

    [Theory]
    [InlineData(data: "../../etc/passwd")]
    [InlineData(data: "..\\..\\x")]
    [InlineData(data: "a/b/c")]
    public void SanitizeSegment_OutputHasNoPathSeparators(string input)
    {
        string result = ImageRequestPath.SanitizeSegment(segment: input);

        Assert.DoesNotContain(expectedSubstring: "/", actualString: result);
        Assert.DoesNotContain(expectedSubstring: "\\", actualString: result);
    }
}
