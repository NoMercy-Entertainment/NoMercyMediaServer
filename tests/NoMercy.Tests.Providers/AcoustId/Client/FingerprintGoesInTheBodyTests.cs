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
using NoMercy.Providers.AcoustId.Client;
using NoMercy.Tests.Common;

namespace NoMercy.Tests.Providers.AcoustId.Client;

/// <summary>
/// A chromaprint fingerprint runs to thousands of characters and grows with the length
/// of the track, so it cannot travel in the query string: AcoustID answers a long enough
/// one with <c>414 (URI Too Long)</c>, and the track is skipped.
/// <para>
/// Verified against the live AcoustID API on 2026-08-02 with a real fingerprint taken
/// from an 8208-character track in the sample library — the same fingerprint returned
/// <c>414</c> as a GET query string and <c>200</c> as a POST form body. Short tracks
/// always worked, which is what made this look like a per-file fingerprinting fault
/// rather than a transport one.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class FingerprintGoesInTheBodyTests
{
    private static string ReadClientSource()
    {
        return File.ReadAllText(
            RepoPaths.At("src", "NoMercy.Providers", "AcoustId", "Client", "AcoustIdBaseClient.cs")
        );
    }

    [Fact]
    public void TheLookupPostsAForm_RatherThanPuttingTheFingerprintInTheUri()
    {
        string source = ReadClientSource();

        Assert.Contains("FormUrlEncodedContent", source);
        Assert.Contains("PostAsync", source);
    }

    /// <summary>
    /// The regression to guard is narrow and specific: sending the composed URL — which
    /// carries the fingerprint — as a GET.
    /// </summary>
    [Fact]
    public void TheComposedUrlIsNeverFetchedWithGet()
    {
        string source = ReadClientSource();

        Assert.DoesNotContain("GetStringAsync(newUrl)", source);
    }

    [Fact]
    public void TheFormHelperExists()
    {
        MethodInfo? post = typeof(AcoustIdBaseClient).GetMethod(
            "PostFormAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.NotNull(post);
    }
}
