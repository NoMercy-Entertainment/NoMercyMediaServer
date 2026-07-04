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

using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Dvd;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait("Category", "Unit")]
public class DvdDiscSourceTests
{
    [Fact]
    public void ClassifyProtection_NullStderr_ReturnsNull()
    {
        string stderr = "";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_CssAuthenticationFailed_ReturnsCssProtection()
    {
        string stderr = "libdvdcss: css authentication failed";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("CSS");
        result.Message.Should().Contain("CSS");
    }

    [Fact]
    public void ClassifyProtection_CouldNotGetKeyForAnyTitle_ReturnsCssProtection()
    {
        string stderr = "libdvdcss: could not get a key for any title";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("CSS");
    }

    [Fact]
    public void ClassifyProtection_RegionCodeMismatch_ReturnsRegionLockProtection()
    {
        string stderr = "libdvdread: region code mismatch";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("RegionLock");
        result.Message.Should().Contain("region");
    }

    [Fact]
    public void ClassifyProtection_CaseSensitivityIgnored()
    {
        string stderr = "CSS AUTHENTICATION FAILED";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("CSS");
    }

    [Fact]
    public void ClassifyProtection_CssFailureInMultilineStderr_Detected()
    {
        string stderr = """
            libdvdcss: opening /dev/dvd
            libdvdcss: css authentication failed
            libdvdread: error - could not read block 100
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("CSS");
    }

    [Fact]
    public void ClassifyProtection_RegionCodeInMultilineStderr_Detected()
    {
        string stderr = """
            libdvdread: opening DVD...
            libdvdread: region code mismatch
            Cannot read disc
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("RegionLock");
    }

    [Fact]
    public void ClassifyProtection_NoProtectionMarkers_ReturnsNull()
    {
        string stderr = """
            libdvdread: opening /dev/dvd
            libdvdread: reading VMG
            Playback initialized
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_CouldNotGetKeyAnyTitle_ContainsHelpfulMessage()
    {
        string stderr = "Could not get a key for any title";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Message.Should().Contain("key");
        result.Message.Should().Contain("libdvdcss");
    }

    [Fact]
    public void ClassifyProtection_RegionMessageSuggestsFix()
    {
        string stderr = "region code mismatch";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Message.Should().Contain("region");
        result.Message.Should().Contain("drive");
    }

    [Fact]
    public void ClassifyProtection_PrefersCssOverRegion()
    {
        string stderr = """
            libdvdcss: css authentication failed
            libdvdread: region code mismatch
            """;

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("CSS");
    }

    [Fact]
    public void ClassifyProtection_ChecksExactErrorPatterns()
    {
        string stderr = "authentication issue";

        DiscProtection? result = DvdDiscSource.ClassifyProtection(stderr);

        result.Should().BeNull();
    }
}
