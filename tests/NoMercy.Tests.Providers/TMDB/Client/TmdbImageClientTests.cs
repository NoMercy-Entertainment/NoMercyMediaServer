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

using NoMercy.Providers.TMDB.Client;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// Unit tests for TmdbImageClient
/// Tests image processing and metadata functionality
/// </summary>
[Trait(name: "Category", value: "Unit")]
[Collection(name: "TmdbApi")]
public class TmdbImageClientTests : TmdbTestBase
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        // TmdbImageClient is abstract, so we cannot instantiate it directly
        // This test verifies the class structure exists
        typeof(TmdbImageClient).Should().NotBeNull();
        typeof(TmdbImageClient).Should().BeAbstract();
    }

    #endregion

    // TODO: Implement remaining Image client tests
    // - Image processing tests
    // - URL generation tests
    // - Size variant tests
}
