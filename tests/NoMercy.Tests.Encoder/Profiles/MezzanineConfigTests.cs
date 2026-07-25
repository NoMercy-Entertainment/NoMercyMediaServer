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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class MezzanineConfigTests
{
    [Fact]
    public void Default_IsVisuallyLosslessHevc()
    {
        MezzanineConfig mezzanine = new();

        mezzanine.Codec.Should().Be("hevc");
        mezzanine.Crf.Should().Be(12, "visually lossless by default");
    }

    [Fact]
    public void EncodingProfile_Mezzanine_DefaultsNull_AndIsSettable()
    {
        EncodingProfile noMezzanine = new(
            Ulid.NewUlid(),
            "p",
            Container.HlsTs,
            null,
            [],
            []
        );

        noMezzanine.Mezzanine.Should().BeNull("default = no mezzanine, unchanged behaviour");

        EncodingProfile withMezzanine = noMezzanine with
        {
            Mezzanine = new("ffv1", 0),
        };

        withMezzanine.Mezzanine.Should().NotBeNull();
        withMezzanine.Mezzanine!.Codec.Should().Be("ffv1");
    }
}
